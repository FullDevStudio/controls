// Pointer-driven card drag for KanbanView.
//
// Pointer events rather than HTML5 drag-and-drop: DnD never fires on touch, and a board is the one
// control people most expect to drag on a phone. Pointer events are uniform across mouse, touch and
// pen.
//
// The hit testing deliberately never crosses into .NET while the finger is down. Resolving a drop
// target means reading element rectangles, and routing that through interop puts a render pass and a
// round trip between the pointer moving and the insertion line following it - which is precisely the
// lag that reads as a broken board. .NET is asked once, at drag start, which lanes will accept this
// card; everything after that is measured here.
//
// Touch is not the mouse with a finger. On a phone the board itself scrolls sideways, and the browser
// decides at touch-down whether a gesture is a scroll - so a card cannot just start dragging on the
// first move the way it does under a mouse: the board pans, and the browser cancels the pointer. A
// finger picks a card up by pressing and holding it (LONG_PRESS_MS); moving first is a scroll. Once
// held, a non-passive touchmove listener keeps the browser from taking the gesture back.

const LANE_SEPARATOR = '';
const DRAG_THRESHOLD = 5;
const AUTOSCROLL_ZONE = 56;
const AUTOSCROLL_STEP = 14;
const LONG_PRESS_MS = 350;
const TOUCH_SLOP = 8;

const instances = new WeakMap();

export function attach(root, dotnet) {
    if (!root || instances.has(root)) {
        return;
    }

    const state = {
        root,
        dotnet,
        indicator: root.querySelector('[data-kanban-drop]'),
        pending: null,
        drag: null,
        listeners: [],
    };

    const on = (target, name, fn, options) => {
        target.addEventListener(name, fn, options);
        state.listeners.push([target, name, fn, options]);
    };

    on(root, 'pointerdown', e => onPointerDown(state, e));
    on(window, 'pointermove', e => onPointerMove(state, e));
    on(window, 'pointerup', e => onPointerUp(state, e));
    on(window, 'pointercancel', () => cancelDrag(state));

    // Only ever cancelled while a card is held or carried - every other touch scrolls as normal.
    on(root, 'touchmove', e => {
        if (state.drag || state.pending?.armed) {
            e.preventDefault();
        }
    }, { passive: false });

    // A long-press would otherwise open the callout / context menu over the card being picked up.
    on(root, 'contextmenu', e => {
        if (state.drag || state.pending?.armed) {
            e.preventDefault();
        }
    });

    instances.set(root, state);
}


export function detach(root) {
    const state = root && instances.get(root);
    if (!state) {
        return;
    }

    cancelDrag(state);
    state.listeners.forEach(([target, name, fn, options]) => target.removeEventListener(name, fn, options));
    instances.delete(root);
}


export function scrollToCard(root, cardId) {
    const card = root?.querySelector(`[data-kanban-card="${CSS.escape(cardId)}"]`);
    card?.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
}


// ---------------------------------------------------------------- press

function onPointerDown(state, e) {
    // Only the primary button, and never on something that is already interactive - a card carrying
    // its own button or link has to stay clickable.
    if (e.button !== 0 || e.target.closest('button, a, input, textarea, select')) {
        return;
    }

    const card = e.target.closest('[data-kanban-card]');
    if (!card || card.dataset.kanbanLocked === 'true') {
        return;
    }

    clearPending(state);
    const touch = e.pointerType === 'touch';
    state.pending = {
        card,
        cardId: card.getAttribute('data-kanban-card'),
        startX: e.clientX,
        startY: e.clientY,
        pointerId: e.pointerId,
        touch,
        armed: !touch,
        timer: touch ? setTimeout(() => armPending(state), LONG_PRESS_MS) : 0,
    };
}


function armPending(state) {
    const p = state.pending;
    if (!p) {
        return;
    }

    p.armed = true;
    p.timer = 0;
    p.card.classList.add('is-pressed');
    navigator.vibrate?.(10);
}


function clearPending(state) {
    const p = state.pending;
    state.pending = null;
    if (p) {
        clearTimeout(p.timer);
        p.card.classList.remove('is-pressed');
    }
}


// ---------------------------------------------------------------- move

async function onPointerMove(state, e) {
    if (state.pending && !state.drag) {
        const dx = e.clientX - state.pending.startX;
        const dy = e.clientY - state.pending.startY;

        // A finger that moves before the long-press fires is scrolling the board - let it.
        if (!state.pending.armed) {
            if (Math.hypot(dx, dy) > TOUCH_SLOP) {
                clearPending(state);
            }
            return;
        }

        // A threshold, not an immediate start: without it every click on a card is a one-pixel drag,
        // and the card never receives the click at all.
        if (Math.hypot(dx, dy) < DRAG_THRESHOLD) {
            return;
        }

        await beginDrag(state, e);
    }

    if (!state.drag) {
        return;
    }

    e.preventDefault();

    state.drag.pointer = { x: e.clientX, y: e.clientY };
    follow(state);
}


// The card under the pointer, and the drop line, for wherever the pointer and the board are now.
function follow(state) {
    const d = state.drag;
    // the card lives inside the board's scroll content, so a board that auto-scrolled has carried it
    // away from the finger - put the scrolled distance back
    const sx = state.root.scrollLeft - d.scrollLeft;
    const sy = state.root.scrollTop - d.scrollTop;
    d.card.style.transform = `translate(${d.pointer.x - d.startX + sx}px, ${d.pointer.y - d.startY + sy}px)`;

    // past the board's edge counts as its edge lane: on a phone the finger is often off the board
    // entirely while the next lane scrolls in, and dropping there must not land nowhere
    const r = state.root.getBoundingClientRect();
    const x = Math.max(r.left + 1, Math.min(r.right - 1, d.pointer.x));
    const y = Math.max(r.top + 1, Math.min(r.bottom - 1, d.pointer.y));
    resolveTarget(state, x, y);
}


// Keeps scrolling while the pointer rests near an edge. Moving the pointer is not required - on a
// phone you park the card at the edge and wait for the next lane to come in.
function autoScrollLoop(state) {
    const d = state.drag;
    if (!d) {
        return;
    }

    if (autoScroll(state, d.pointer.x, d.pointer.y)) {
        follow(state);
    }
    d.frame = requestAnimationFrame(() => autoScrollLoop(state));
}


async function beginDrag(state, e) {
    const p = state.pending;
    state.pending = null;
    clearTimeout(p.timer);

    let allowed = [];
    try {
        allowed = await state.dotnet.invokeMethodAsync('OnDragStartJs', p.cardId) ?? [];
    } catch {
        // The circuit went away mid-gesture. Nothing to drag into.
        return;
    }

    state.drag = {
        card: p.card,
        cardId: p.cardId,
        startX: p.startX,
        startY: p.startY,
        allowed: new Set(allowed),
        target: null,
        blocked: null,
        pointer: { x: e.clientX, y: e.clientY },
        scrollLeft: state.root.scrollLeft,
        scrollTop: state.root.scrollTop,
        // measured before the card moves: a translated card widens the scroll area itself, and an
        // edge scroll that chased that ran on forever, carrying every lane away from the pointer
        maxLeft: state.root.scrollWidth - state.root.clientWidth,
        maxTop: state.root.scrollHeight - state.root.clientHeight,
        frame: 0,
    };

    p.card.classList.remove('is-pressed');
    p.card.classList.add('is-dragging');
    state.root.classList.add('is-dragging');
    document.body.style.userSelect = 'none';
    state.drag.frame = requestAnimationFrame(() => autoScrollLoop(state));
}


function resolveTarget(state, x, y) {
    const d = state.drag;
    d.target = null;
    d.blocked = null;

    const cell = cellAt(state, x, y);
    if (!cell) {
        hideIndicator(state);
        return;
    }

    const columnId = cell.getAttribute('data-kanban-column');
    const swimlaneId = cell.getAttribute('data-kanban-swimlane') ?? '';

    if (!d.allowed.has(`${columnId}${LANE_SEPARATOR}${swimlaneId}`)) {
        // Remembered so a release here can still be reported as refused (WIP limit, locked lane...)
        // rather than ending in silence.
        d.blocked = { columnId, swimlaneId };
        hideIndicator(state);
        return;
    }

    const cards = [...cell.querySelectorAll('[data-kanban-card]')];
    let index = cards.length;

    for (let i = 0; i < cards.length; i++) {
        const r = cards[i].getBoundingClientRect();

        // The dragged card has been translated out from under itself, so measuring where it is now
        // would put the line wherever the finger is. Its original slot is what counts, and PlanMove
        // on the .NET side is what takes it back out of the count.
        if (cards[i] === d.card) {
            continue;
        }

        if (y < r.top + r.height / 2) {
            index = i;
            break;
        }
    }

    d.target = { columnId, swimlaneId, index };
    showIndicator(state, cell, cards, index);
}


function cellAt(state, x, y) {
    const cells = [...state.root.querySelectorAll('[data-kanban-cell]')];

    let nearest = null;
    let best = Infinity;

    for (const cell of cells) {
        const r = cell.getBoundingClientRect();

        if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) {
            return cell;
        }

        // Dragging into the gutter between two columns should still land somewhere: a drop that
        // silently does nothing because the finger was four pixels wide of a column is
        // indistinguishable from a board that is broken.
        const dx = Math.max(r.left - x, 0, x - r.right);
        const dy = Math.max(r.top - y, 0, y - r.bottom);
        const distance = Math.hypot(dx, dy);

        if (distance < best) {
            best = distance;
            nearest = cell;
        }
    }

    return best <= 120 ? nearest : null;
}


function showIndicator(state, cell, cards, index) {
    const indicator = state.indicator;
    if (!indicator) {
        return;
    }

    const rootRect = state.root.getBoundingClientRect();
    const cellRect = cell.getBoundingClientRect();
    const styles = getComputedStyle(cell);
    const padLeft = parseFloat(styles.paddingLeft) || 0;
    const padRight = parseFloat(styles.paddingRight) || 0;
    const padTop = parseFloat(styles.paddingTop) || 0;
    const gap = parseFloat(styles.rowGap) || 0;

    const others = cards.filter(c => c !== state.drag.card);
    let top;

    if (index < others.length) {
        top = others[index].getBoundingClientRect().top - gap / 2;
    } else if (others.length > 0) {
        top = others[others.length - 1].getBoundingClientRect().bottom + gap / 2;
    } else {
        top = cellRect.top + padTop;
    }

    indicator.style.left = `${cellRect.left - rootRect.left + state.root.scrollLeft + padLeft}px`;
    indicator.style.top = `${top - rootRect.top + state.root.scrollTop}px`;
    indicator.style.width = `${cellRect.width - padLeft - padRight}px`;
    indicator.hidden = false;
}


function hideIndicator(state) {
    if (state.indicator) {
        state.indicator.hidden = true;
    }
}


// ---------------------------------------------------------------- auto-scroll

function autoScroll(state, x, y) {
    const r = state.root.getBoundingClientRect();

    const dx = nudge(x - r.left, r.width);
    const dy = nudge(y - r.top, r.height);

    if (!dx && !dy) {
        return false;
    }

    const d = state.drag;
    const left = state.root.scrollLeft, top = state.root.scrollTop;
    const toLeft = Math.max(0, Math.min(d.maxLeft, left + dx));
    const toTop = Math.max(0, Math.min(d.maxTop, top + dy));
    if (toLeft === left && toTop === top) {
        return false;
    }

    state.root.scrollTo(toLeft, toTop);
    return true;
}


function nudge(position, extent) {
    if (extent <= 0) {
        return 0;
    }
    // Faster the deeper into the edge zone, so a finger resting just inside it can stop on a lane
    // rather than flying to the end of the board; beyond the edge is full speed.
    if (position < AUTOSCROLL_ZONE) {
        return -Math.max(1, Math.round(AUTOSCROLL_STEP * Math.min(1, (AUTOSCROLL_ZONE - position) / AUTOSCROLL_ZONE)));
    }
    if (position > extent - AUTOSCROLL_ZONE) {
        return Math.max(1, Math.round(AUTOSCROLL_STEP * Math.min(1, (position - (extent - AUTOSCROLL_ZONE)) / AUTOSCROLL_ZONE)));
    }
    return 0;
}


// ---------------------------------------------------------------- release

function onPointerUp(state) {
    clearPending(state);

    const d = state.drag;
    if (!d) {
        return;
    }

    const target = d.target;
    const blocked = d.blocked;
    endDrag(state);

    if (target) {
        state.dotnet.invokeMethodAsync('OnDropJs', d.cardId, target.columnId, target.swimlaneId, target.index);
    }
    else if (blocked) {
        // .NET re-evaluates the lane, refuses the move and raises OnDropRejected with the reason.
        state.dotnet.invokeMethodAsync('OnDropJs', d.cardId, blocked.columnId, blocked.swimlaneId, 0);
    }
}


function cancelDrag(state) {
    clearPending(state);
    if (state.drag) {
        endDrag(state);
    }
}


function endDrag(state) {
    const d = state.drag;
    state.drag = null;

    if (d) {
        cancelAnimationFrame(d.frame);
        d.card.style.transform = '';
        d.card.classList.remove('is-dragging');
    }

    state.root.classList.remove('is-dragging');
    document.body.style.userSelect = '';
    hideIndicator(state);
}
