const instances = new Map();

export function init(trackEl, dotNetRef) {
    const state = { trackEl, dotNetRef, dragging: false, isUpper: false };

    const onPointerMove = (e) => {
        if (!state.dragging) return;
        e.preventDefault();
        const rect = trackEl.getBoundingClientRect();
        const percent = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
        dotNetRef.invokeMethodAsync('OnDragUpdate', percent, state.isUpper);
    };

    const onPointerUp = () => {
        if (state.dragging)
            suppressNextClick(trackEl);
        state.dragging = false;
        document.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('pointerup', onPointerUp);
        document.removeEventListener('pointercancel', onPointerUp);
        dotNetRef.invokeMethodAsync('OnDragEnd');
    };

    trackEl.addEventListener('pointerdown', (e) => {
        const thumb = e.target.closest('.shiny-rs-thumb');
        if (thumb) {
            state.dragging = true;
            state.isUpper = thumb.getAttribute('data-thumb') === 'upper';
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnDragStart');
            document.addEventListener('pointermove', onPointerMove);
            document.addEventListener('pointerup', onPointerUp);
            document.addEventListener('pointercancel', onPointerUp);
        }
    });

    state.onPointerMove = onPointerMove;
    state.onPointerUp = onPointerUp;
    instances.set(trackEl, state);
}

// Letting go of a thumb still produces a click, and when the pointer is no longer over the thumb (it
// was held back by MinimumRange, or ran off the end) that click lands on the track, whose handler jumps
// the nearer thumb to the release point. The drag already decided where the thumbs go; swallow it.
function suppressNextClick(el) {
    const swallow = ev => {
        ev.stopPropagation();
        ev.preventDefault();
        el.removeEventListener('click', swallow, true);
    };
    el.addEventListener('click', swallow, true);

    // pointercancel and some touch stacks produce no click; don't leave it armed for the next tap.
    setTimeout(() => el.removeEventListener('click', swallow, true), 400);
}

export function getClickPercent(trackEl, clientX) {
    const rect = trackEl.getBoundingClientRect();
    return Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
}

export function dispose(trackEl) {
    const state = instances.get(trackEl);
    if (state) {
        document.removeEventListener('pointermove', state.onPointerMove);
        document.removeEventListener('pointerup', state.onPointerUp);
        document.removeEventListener('pointercancel', state.onPointerUp);
        instances.delete(trackEl);
    }
}
