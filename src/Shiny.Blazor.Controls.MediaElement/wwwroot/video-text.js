// VideoText: the mask itself is pure CSS (an SVG data URL built in C#). This module only does the two
// things markup cannot — set the muted *property* (the attribute is just defaultMuted, so toggling it
// after load changes nothing) and kick playback where autoplay was refused — plus measure the glyphs so
// auto-fit uses real widths instead of an estimate.

export function start(video, muted) {
    applyMuted(video, muted);
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
// DTOs does not). Waits for web fonts so a late-loading face doesn't leave the fit measured in a fallback.
export async function measure(lines, family, weight, style) {
    try {
        if (document.fonts && document.fonts.ready) await document.fonts.ready;
    } catch { }

    const canvas = measure.canvas || (measure.canvas = document.createElement('canvas'));
    const ctx = canvas.getContext('2d');
    ctx.font = `${style || 'normal'} ${weight || 'normal'} 100px ${family || 'sans-serif'}`;
    return lines.map(l => ctx.measureText(l).width);
}
