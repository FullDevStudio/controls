// VideoText: the mask and overlay are SVG built in C#. This module only does the things markup cannot —
// set the muted *property* (the attribute is just defaultMuted, so toggling it after load changes
// nothing), kick playback where autoplay was refused, report the element's size for the marquee, and
// measure the glyphs so auto-fit uses real widths instead of an estimate.

export function start(video, container, dotnet, muted) {
    applyMuted(video, muted);

    // a marquee's travel is in pixels, so .NET needs the element's size (and to hear when it changes)
    if (container && dotnet && typeof ResizeObserver !== 'undefined') {
        const observer = new ResizeObserver(entries => {
            const box = entries[entries.length - 1].contentRect;
            dotnet.invokeMethodAsync('OnResize', box.width, box.height).catch(() => { });
        });
        observer.observe(container);
        observers.set(container, observer);
    }
}

const observers = new WeakMap();

export function stop(container, video) {
    const observer = container && observers.get(container);
    if (observer) {
        observer.disconnect();
        observers.delete(container);
    }

    // A removed <video> is paused, but some engines keep its decoder, buffers and connection until it is
    // collected. Dropping the source and reloading releases them now.
    if (video) {
        try {
            video.pause();
            video.removeAttribute('src');
            video.load();
        } catch { }
    }
}

export function setMuted(video, muted) {
    applyMuted(video, muted);
}

function applyMuted(video, muted) {
    if (!video) return;
    video.defaultMuted = muted;
    video.muted = muted;

    const attempt = video.play();
    if (attempt && attempt.catch) {
        attempt.catch(() => {
            // Browsers refuse audible autoplay until the user has interacted with the page. A decorative
            // loop that stays frozen is worse than a silent one, so fall back to muted and play anyway.
            if (!video.muted) {
                video.muted = true;
                video.play().catch(() => { });
            }
        });
    }
}

// Widths of each line at 100px, as plain numbers (an array of primitives survives trimming; an array of
// DTOs does not). Loads the face first: fonts.ready alone does not, because a web font nothing on the page
// uses yet is never fetched, and the fit would be measured in the fallback.
export async function measure(lines, family, weight, style) {
    const font = `${style || 'normal'} ${weight || 'normal'} 100px ${family || 'sans-serif'}`;
    try {
        if (document.fonts) {
            await document.fonts.load(font, lines.join(''));
            await document.fonts.ready;
        }
    } catch { }

    const canvas = measure.canvas || (measure.canvas = document.createElement('canvas'));
    const ctx = canvas.getContext('2d');
    ctx.font = font;
    return lines.map(l => ctx.measureText(l).width);
}
