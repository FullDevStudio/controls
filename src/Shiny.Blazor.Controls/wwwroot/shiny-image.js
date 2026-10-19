// Progress for an image download, which the DOM alone cannot give you.
//
// An <img> element reports "started" and "finished" and nothing in between - there is no progress
// event for it in any browser. The only way to know that 40% of a photo has arrived is to fetch it
// yourself, read the body as a stream, and count the bytes. That is all this module does: fetch,
// pump, report, then hand back a blob URL for the <img> to display.
//
// The trade is CORS. A plain <img> may load a cross-origin image with no server cooperation at all;
// fetch() may not. So every failure here is treated as "let the browser do it instead" rather than
// as an error - the image still appears, just without a percentage.
//
// Loads are shared per URL. Twelve images bound to one avatar make one fetch and share one blob:
// a later caller joins the fetch in flight (and is told how far it has got), or, once it has
// finished, gets the same blob URL straight away. The blob is reference-counted and revoked when the
// last image showing it lets go. Nothing is kept after that - the browser's own HTTP cache, which
// revalidates with ETag/Last-Modified and lives on disk, is what makes the next visit cheap.

const MIN_REPORT_INTERVAL_MS = 100;
const MIN_REPORT_DELTA = 0.01;

/** url -> { controller, promise, listeners: Map<requestId, dotNetRef>, received, total, blobUrl, refs, done } */
const shared = new Map();

/** requestId -> url, for abort. */
const requests = new Map();

/** blob URL -> shared key, so revoke can find the entry it belongs to. */
const owners = new Map();

/**
 * Stops listening for one load. The fetch itself is only cancelled when nobody else is waiting on
 * it - one image scrolling away must not take the picture from the other eleven.
 */
export function abort(requestId) {
    const url = requests.get(requestId);
    requests.delete(requestId);

    const entry = url && shared.get(url);
    if (!entry) {
        return;
    }

    entry.listeners.delete(requestId);

    if (!entry.done && entry.listeners.size === 0) {
        entry.controller?.abort();
        shared.delete(url);
    }
}

/**
 * Lets go of a blob URL. A shared one is revoked when its last holder lets go; any other is revoked
 * at once. Called for every URL this module handed out, or the tab leaks one per image.
 */
export function revoke(url) {
    if (typeof url !== 'string' || !url.startsWith('blob:')) {
        return;
    }

    const key = owners.get(url);
    const entry = key && shared.get(key);

    if (!entry || entry.blobUrl !== url) {
        URL.revokeObjectURL(url);
        return;
    }

    if (--entry.refs > 0) {
        return;
    }

    owners.delete(url);
    shared.delete(key);
    URL.revokeObjectURL(url);
}

/** Takes a reference on a blob already held for this key, or returns null. */
export function acquire(key) {
    const entry = key && shared.get(key);
    if (!entry || !entry.done || !entry.blobUrl) {
        return null;
    }

    entry.refs++;
    return entry.blobUrl;
}

/**
 * Wraps raw bytes from C# in a blob URL - the path taken when a custom downloader fetched them.
 * With a key the blob is shared like a fetched one, and a second image for the same key reuses it.
 */
export function createBlobUrl(bytes, contentType, key) {
    const existing = acquire(key);
    if (existing) {
        return existing;
    }

    const blob = contentType ? new Blob([bytes], { type: contentType }) : new Blob([bytes]);
    const url = URL.createObjectURL(blob);

    if (key) {
        shared.set(key, { controller: null, promise: null, listeners: new Map(), received: 0, total: 0, blobUrl: url, refs: 1, done: true });
        owners.set(url, key);
    }
    return url;
}

/**
 * Resolves true once the URL has decoded as an image, false if it cannot.
 *
 * This exists instead of Blazor's @onload/@onerror on the <img>. The load and error events do not
 * bubble, so they do not survive Blazor's delegated event plumbing reliably - and a component that
 * silently never learns its image arrived stays on the spinner forever. Decoding here is also
 * strictly cheaper than it looks: the URL is either a local blob or already sitting in the HTTP
 * cache from the fetch that produced it.
 */
export function preload(url) {
    return new Promise(resolve => {
        if (!url) {
            resolve(false);
            return;
        }

        const img = new Image();
        img.onload = () => resolve(true);
        img.onerror = () => resolve(false);
        img.src = url;
    });
}

/**
 * Streams an image, reporting progress, and returns a blob URL - shared with every other load of
 * the same URL.
 *
 * Returns { url, contentLength, deferToBrowser, error }. `deferToBrowser` means the caller should
 * put the original URL straight into the <img> - the usual cause is a cross-origin server that
 * sends no CORS headers, which blocks fetch but not <img>. A returned `url` is a reference the
 * caller owns and must hand back through revoke().
 */
export async function load(dotNetRef, url, requestId) {
    let entry = shared.get(url);
    if (!entry) {
        entry = fetchShared(url);
        shared.set(url, entry);
    }

    entry.listeners.set(requestId, dotNetRef);
    requests.set(requestId, url);

    // A late joiner would otherwise sit at 0% until the next chunk, which on a nearly finished
    // download can be never.
    if (!entry.done && entry.received > 0) {
        dotNetRef.invokeMethodAsync('OnProgress', entry.received, entry.total);
    }

    try {
        const result = await entry.promise;

        if (!entry.listeners.delete(requestId)) {
            return { url: null, contentLength: 0, deferToBrowser: false, error: 'aborted' };
        }
        if (result.url) {
            entry.refs++;
        }
        return result;
    }
    finally {
        requests.delete(requestId);
    }
}

function fetchShared(url) {
    const entry = {
        controller: new AbortController(),
        promise: null,
        listeners: new Map(),
        received: 0,
        total: 0,
        blobUrl: null,
        refs: 0,
        done: false
    };

    entry.promise = (async () => {
        const result = await fetchOnce(url, entry);
        entry.done = true;

        // Only a blob is worth keeping for the next caller. A failure is forgotten, so the next
        // image to ask tries again instead of inheriting this one's error.
        if (!result.url) {
            if (shared.get(url) === entry) {
                shared.delete(url);
            }
        }
        else if (entry.listeners.size === 0) {
            // Everyone who asked has gone. Nobody will ever revoke this, so it goes now.
            URL.revokeObjectURL(result.url);
            if (shared.get(url) === entry) {
                shared.delete(url);
            }
            return { url: null, contentLength: 0, deferToBrowser: false, error: 'aborted' };
        }
        else {
            entry.blobUrl = result.url;
            owners.set(result.url, url);
        }
        return result;
    })();

    return entry;
}

async function fetchOnce(url, entry) {
    try {
        const response = await fetch(url, { signal: entry.controller.signal, credentials: 'same-origin' });

        if (!response.ok) {
            return { url: null, contentLength: 0, deferToBrowser: false, error: `HTTP ${response.status}` };
        }

        const header = response.headers.get('Content-Length');
        const total = header ? parseInt(header, 10) : 0;
        entry.total = total;

        // No readable body (older browsers, or an opaque response) means nothing to count, so fall
        // straight through to a normal <img> load rather than buffering blind.
        if (!response.body || !response.body.getReader) {
            return { url: null, contentLength: total, deferToBrowser: true, error: null };
        }

        const reader = response.body.getReader();
        const chunks = [];
        let lastReport = 0;
        let lastPercent = -1;

        // Reporting every chunk means a render pass per network packet. One percent or 100ms -
        // whichever comes first - is smooth to the eye and leaves the interop channel alone.
        const report = () => {
            const now = performance.now();
            const percent = total > 0 ? entry.received / total : -1;

            if (now - lastReport < MIN_REPORT_INTERVAL_MS && (percent < 0 || percent - lastPercent < MIN_REPORT_DELTA)) {
                return;
            }

            lastReport = now;
            lastPercent = percent;
            for (const listener of entry.listeners.values()) {
                listener.invokeMethodAsync('OnProgress', entry.received, total);
            }
        };

        for (;;) {
            const { done, value } = await reader.read();
            if (done) {
                break;
            }

            chunks.push(value);
            entry.received += value.length;
            report();
        }

        const blob = new Blob(chunks, { type: response.headers.get('Content-Type') || '' });
        return { url: URL.createObjectURL(blob), contentLength: entry.received, deferToBrowser: false, error: null };
    }
    catch (ex) {
        if (ex && ex.name === 'AbortError') {
            return { url: null, contentLength: 0, deferToBrowser: false, error: 'aborted' };
        }

        // Almost always CORS. The browser can still render this image through a plain <img>, so
        // losing the percentage is the whole cost of the failure.
        return { url: null, contentLength: 0, deferToBrowser: true, error: String(ex) };
    }
}
