/*
    The browser half of BrowserOfficeDocumentStore.

    Where the File System Access API exists (Chromium), files are real files: Browse is the system open
    dialog, Save writes back over the file it came from, Save As is the system save dialog, and the
    handles are kept in IndexedDB so the recent list reopens files after a reload. Everywhere else
    (Safari, Firefox) Browse is a file input and every save is a download; those files are only
    reopenable for the life of the page.

    Every result crosses back to .NET as a JSON string, never an object: a trimmed WebAssembly publish
    drops the members of an interop DTO it cannot see being used.
*/

const DB = 'shiny-office-files';
const STORE = 'handles';
const RECENT = 'shiny-office:recent';
const MAX_RECENT = 20;

const live = new Map(); // id -> FileSystemFileHandle | File
const born = new Map(); // id -> when it was handed out, so a file not yet in the recent list survives a prune
const GRACE_MS = 60000;

const hasPicker = () => typeof window.showOpenFilePicker === 'function';

function newId() {
    return (crypto.randomUUID && crypto.randomUUID()) || (Date.now().toString(36) + Math.random().toString(36).slice(2));
}

function openDb() {
    return new Promise((resolve, reject) => {
        if (!window.indexedDB) {
            resolve(null);
            return;
        }
        const request = indexedDB.open(DB, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(STORE);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function idb(mode, work) {
    const db = await openDb().catch(() => null);
    if (!db)
        return undefined;

    return new Promise((resolve, reject) => {
        let tx, request;
        try {
            tx = db.transaction(STORE, mode);
            request = work(tx.objectStore(STORE));
        } catch (error) {
            db.close();
            reject(error);
            return;
        }
        tx.oncomplete = () => { db.close(); resolve(request?.result); };
        tx.onerror = () => { db.close(); reject(tx.error); };
        // A quota failure aborts without an error event on some browsers; never leave the db open.
        tx.onabort = () => { db.close(); reject(tx.error); };
    });
}

async function remember(id, handle) {
    live.set(id, handle);
    born.set(id, Date.now());

    // Only a real handle survives a reload; a File from an input is gone with the page.
    if (typeof FileSystemFileHandle !== 'undefined' && handle instanceof FileSystemFileHandle)
        await idb('readwrite', s => s.put(handle, id)).catch(() => { });
}

async function lookup(id) {
    if (live.has(id))
        return live.get(id);

    const handle = await idb('readonly', s => s.get(id)).catch(() => undefined);
    if (handle)
        live.set(id, handle);

    return handle;
}

async function permitted(handle, mode) {
    if (typeof handle.queryPermission !== 'function')
        return true;

    if (await handle.queryPermission({ mode }) === 'granted')
        return true;

    return await handle.requestPermission({ mode }) === 'granted';
}

function pickerTypes(description, mime, extension) {
    return [{ description, accept: { [mime]: [extension] } }];
}

function isAbort(error) {
    return error && (error.name === 'AbortError' || error.name === 'NotAllowedError');
}

function result(id, name) {
    return JSON.stringify({ id, name });
}

function inputPick(extension, mime) {
    return new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = `${extension},${mime}`;
        input.style.display = 'none';
        document.body.appendChild(input);

        let settled = false;
        const done = file => {
            if (settled) return;
            settled = true;
            window.removeEventListener('focus', onFocus);
            input.remove();
            resolve(file);
        };

        // Browsers without the cancel event (Safari before 16.4) say nothing when the dialog is
        // dismissed: the window getting focus back with no file chosen is the only sign, and without
        // it the input stays in the page and the promise never settles.
        const onFocus = () => setTimeout(() => done(input.files && input.files[0] || null), 1000);

        input.addEventListener('change', () => done(input.files && input.files[0] || null), { once: true });
        input.addEventListener('cancel', () => done(null), { once: true });
        if (!('oncancel' in input))
            window.addEventListener('focus', onFocus, { once: true });
        input.click();
    });
}

/** Browse. Resolves to {id, name} as JSON, or null when the user cancelled. */
export async function pickOpen(description, mime, extension) {
    try {
        if (hasPicker()) {
            const [handle] = await window.showOpenFilePicker({ types: pickerTypes(description, mime, extension), multiple: false });
            const id = newId();
            await remember(id, handle);
            return result(id, handle.name);
        }

        const file = await inputPick(extension, mime);
        if (!file)
            return null;

        const id = newId();
        await remember(id, file);
        return result(id, file.name);
    } catch (error) {
        if (isAbort(error))
            return null;
        throw error;
    }
}

/** The file's bytes, as a Blob — returned raw so .NET receives it as an IJSStreamReference. */
export async function read(id) {
    const handle = await lookup(id);
    if (!handle)
        throw new Error('That file is no longer available. Open it again from Browse.');

    if (handle instanceof File)
        return handle;

    if (!await permitted(handle, 'read'))
        throw new Error('Permission to read the file was refused.');

    return await handle.getFile();
}

/**
    Writes a file: over `id` when it is a writable handle, otherwise to a new one the user names in the
    save dialog, or as a download where there is no dialog. Resolves to {id, name} as JSON, or null when
    the user cancelled.
*/
export async function save(id, name, description, mime, extension, streamRef) {
    const bytes = await streamRef.arrayBuffer();

    try {
        let handle = id ? await lookup(id) : null;
        const writable = handle && typeof handle.createWritable === 'function';

        if (!writable && typeof window.showSaveFilePicker === 'function') {
            handle = await window.showSaveFilePicker({ suggestedName: name, types: pickerTypes(description, mime, extension) });
            id = newId();
        }

        if (handle && typeof handle.createWritable === 'function') {
            if (!await permitted(handle, 'readwrite'))
                throw new Error('Permission to save the file was refused.');

            const stream = await handle.createWritable();
            await stream.write(bytes);
            await stream.close();
            await remember(id, handle);
            return result(id, handle.name);
        }
    } catch (error) {
        if (isAbort(error))
            return null;
        throw error;
    }

    // No File System Access API: a download, which is also what the next Save will be.
    const url = URL.createObjectURL(new Blob([bytes], { type: mime }));
    const a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 60000);

    // A save over a downloaded file replaces it under the same id: a new id per Save kept a full copy
    // of the document in memory for every press, and filled the recent list with the same file.
    const downloaded = id && live.get(id) instanceof File ? id : newId();
    live.set(downloaded, new File([bytes], name, { type: mime }));
    born.set(downloaded, Date.now());
    await prune(readRecent());
    return result(downloaded, name);
}

function readRecent() {
    try {
        return JSON.parse(localStorage.getItem(RECENT) || '[]');
    } catch {
        return [];
    }
}

/** Puts a file at the top of the recent list. */
export async function touch(id, name, app) {
    const entries = readRecent().filter(x => x.id !== id);
    entries.unshift({ id, name, app, lastOpened: new Date().toISOString(), persistent: !(live.get(id) instanceof File) });
    const kept = entries.slice(0, MAX_RECENT);

    try {
        localStorage.setItem(RECENT, JSON.stringify(kept));
    } catch {
        // Storage full or blocked: the recent list is a convenience.
    }

    await prune(kept);
}

/**
    Lets go of every file the recent list no longer names - the bytes of a download, a handle in
    IndexedDB - except those handed out moments ago, which an editor may be about to read or touch.
*/
async function prune(entries) {
    const keep = new Set(entries.map(x => x.id));
    const now = Date.now();
    const kept = id => keep.has(id) || now - (born.get(id) ?? 0) < GRACE_MS;

    for (const id of [...live.keys()]) {
        if (!kept(id)) {
            live.delete(id);
            born.delete(id);
        }
    }

    await idb('readwrite', s => {
        const keys = s.getAllKeys();
        keys.onsuccess = () => {
            for (const key of keys.result) {
                if (!kept(key))
                    s.delete(key);
            }
        };
        return keys;
    }).catch(() => { });
}

/** The recent list for an app, newest first, as JSON — only files that can still be opened. */
export async function recent(app) {
    const entries = readRecent().filter(x => x.app === app);
    const available = [];

    for (const entry of entries) {
        if (live.has(entry.id) || (entry.persistent && await lookup(entry.id)))
            available.push({ id: entry.id, name: entry.name, lastOpened: entry.lastOpened });
    }

    return JSON.stringify(available);
}
