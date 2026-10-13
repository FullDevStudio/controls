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
