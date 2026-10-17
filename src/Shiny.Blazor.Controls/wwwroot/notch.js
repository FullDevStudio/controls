// NotchView for Shiny.Blazor.Controls.
//
// CSS cannot transition to or from `width: auto`, so the surface's size is measured here and written as
// pixels, and the stylesheet's transition does the animating. The content is laid out at its own
// natural size (width: max-content, capped to the space available) and never at the surface's, so it
// does not re-wrap while the surface springs around it. Mirrors NotchView.ComputeNotchSize on MAUI.

const states = new WeakMap();
const AUTO_RADIUS_CAP = 28;

export function init(root, dotnet, opts) {
    if (!root) return;

    // Already wired: treat it as an update. Re-initialising would start from an unknown size, report a
    // "change" back to .NET, and the re-render that report causes would land here again.
    if (states.has(root)) {
        update(root, opts);
        return;
    }
    const state = { dotnet, opts, observer: null, raf: 0, width: -1, height: -1, observed: null };

    if (typeof ResizeObserver === 'function') {
        state.observer = new ResizeObserver(() => schedule(root, state));
        if (root.parentElement) state.observer.observe(root.parentElement);
    }

    states.set(root, state);

    // First placement is instant: a notch that is there when the page opens should not grow into place.
    root.classList.add('is-instant');
    measure(root, state);
    void root.offsetWidth;
    root.classList.remove('is-instant');
}

export function update(root, opts) {
    const state = states.get(root);
    if (!state) return;
    state.opts = opts;
    schedule(root, state);
}

export function refresh(root) {
    const state = states.get(root);
    if (state) schedule(root, state);
}

export function dispose(root) {
    const state = states.get(root);
    if (!state) return;
    state.observer?.disconnect();
    cancelAnimationFrame(state.raf);
    states.delete(root);
}

function schedule(root, state) {
    if (state.raf) return;
    state.raf = requestAnimationFrame(() => {
        state.raf = 0;
        measure(root, state);
    });
}

function px(value) {
    const n = parseFloat(value);
    return Number.isFinite(n) ? n : 0;
}

function measure(root, state) {
    const content = root.querySelector(':scope > .shiny-notch__content');
    const o = state.opts || {};

    // Blazor replaces the content node when its key changes; observe whichever one is current.
    if (state.observer && content && state.observed !== content) {
        if (state.observed) state.observer.unobserve(state.observed);
        state.observer.observe(content);
        state.observed = content;
    }

    const cs = getComputedStyle(root);
    const insetX = px(cs.paddingLeft) + px(cs.paddingRight) + px(cs.borderLeftWidth) + px(cs.borderRightWidth);
    const insetY = px(cs.paddingTop) + px(cs.paddingBottom) + px(cs.borderTopWidth) + px(cs.borderBottomWidth);

    const available = root.parentElement ? root.parentElement.clientWidth : Infinity;
    let widthCap = o.maxWidth > 0 ? Math.min(o.maxWidth, available || Infinity) : (available || Infinity);
    const heightCap = o.maxHeight > 0 ? o.maxHeight : Infinity;
    const minW = o.minWidth || 0;
    const minH = o.minHeight || 0;
    if (widthCap < minW) widthCap = minW;

    let w = minW;
    let h = minH;

    if (content) {
        content.style.maxWidth = Number.isFinite(widthCap) ? Math.max(0, widthCap - insetX) + 'px' : '';
        const cw = content.offsetWidth;
        const ch = content.offsetHeight;
        if (cw > 0 && ch > 0) {
            w = clamp(cw + insetX, minW, widthCap);
            h = clamp(ch + insetY, minH, heightCap);
        }
    }

    const r = radius(o.cornerRadius, w, h);
    root.style.borderRadius = o.notch ? `0 0 ${r}px ${r}px` : `${r}px`;

    if (Math.abs(w - state.width) < 0.5 && Math.abs(h - state.height) < 0.5) return;

    state.width = w;
    state.height = h;
    root.style.width = w + 'px';
    root.style.height = h + 'px';
    state.dotnet?.invokeMethodAsync('OnJsSizeChanged', w, h).catch(() => { });
}

function radius(requested, w, h) {
    const limit = Math.max(0, Math.min(w, h) / 2);
    const r = requested < 0 ? Math.min(h / 2, AUTO_RADIUS_CAP) : requested;
    return Math.min(Math.max(r, 0), limit);
}

function clamp(v, min, max) {
    return max < min ? min : Math.min(Math.max(v, min), max);
}
