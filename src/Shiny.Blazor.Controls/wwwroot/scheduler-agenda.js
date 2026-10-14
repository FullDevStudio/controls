// Pointer-event drag/resize for SchedulerAgendaView.
//
// Deliberately NOT HTML5 drag-and-drop (which tree-view.js uses): a tree drop is a discrete
// target-and-zone decision, an agenda drag is a continuous positional one. HTML5 DnD gives no
// reliable coordinates during dragover, does not fire at all for touch on mobile Safari/Chrome
// Android, and its drag image is a browser ghost that cannot be snapped to a grid.
//
// Render-loop discipline: no pointermove ever round-trips to .NET. The live preview is pure DOM
// mutation here; .NET is invoked once on arm and once on commit (plus per snap boundary, only when
// the component opted into PerPosition validation).
//
// The preview clamps approximately - .NET re-runs AgendaGeometry on commit and is authoritative.

const instances = new Map();

const SLOP = 8;              // px of travel that hands the gesture back to the scroller
const EDGE = 48;             // px from the viewport edge that starts auto-scroll
const AUTO_STEP = 8;         // px per frame
const MINUTES_PER_DAY = 1440;

const KIND_MOVE = 0;
const KIND_RESIZE_START = 1;
const KIND_RESIZE_END = 2;

// Mirrors AgendaGeometry.SnapMinutes: halves away from zero, granularity clamped to 1..60.
// (JS Math.round breaks halves toward +Infinity, which would make dragging up and down asymmetric.)
function snapMinutes(value, granularity) {
    const g = Math.min(60, Math.max(1, granularity || 15));
    const snapped = Math.sign(value) * Math.round(Math.abs(value) / g) * g;
    return snapped === 0 ? 0 : snapped;
}

function pxPerMinute(state) {
    return (state.options.timeSlotHeight || 60) / 60;
}

function columns(state) {
    return state.root.querySelectorAll('.shiny-agenda-daycol');
}


export function init(root, dotNetRef, options) {
    dispose(root);

    const state = { root, dotNetRef, options, listeners: [], drag: null };
    instances.set(root, state);

    const on = (name, fn) => {
        root.addEventListener(name, fn);
        state.listeners.push([name, fn]);
    };

    on('pointerdown', e => onPointerDown(state, e));
    on('pointermove', e => onPointerMove(state, e));
    on('pointerup', e => onPointerUp(state, e));
    on('pointercancel', () => cancel(state));
    return true;
}


export function update(root, options) {
    const state = instances.get(root);
    if (state) state.options = options;
}


export function dispose(root) {
    const state = instances.get(root);
    if (!state) return;
    cancel(state);
    state.listeners.forEach(([name, fn]) => root.removeEventListener(name, fn));
    instances.delete(root);
}


function onPointerDown(state, e) {
    if (state.drag || !(e.target instanceof Element)) return;
    if (e.button !== undefined && e.button > 0) return;

    const el = e.target.closest('.shiny-agenda-event[data-draggable="true"]');
    if (!el) return;

    const grip = e.target.closest('.shiny-agenda-grip');
    const o = state.options;

    let kind = KIND_MOVE;
    if (grip) kind = grip.classList.contains('shiny-agenda-grip--start') ? KIND_RESIZE_START : KIND_RESIZE_END;

    if (kind === KIND_MOVE ? !o.allowDrag : !o.allowResize) return;

    const column = el.closest('.shiny-agenda-daycol');
    const scroller = state.root.querySelector('.shiny-agenda-day');
    if (!column || !scroller) return;

    const drag = {
        el, kind, scroller, column,
        eventId: el.dataset.eventId,
        sourceDayIndex: parseInt(column.dataset.dayIndex, 10) || 0,
        columnWidth: column.getBoundingClientRect().width,
        pointerId: e.pointerId,
        originX: e.clientX,
        originY: e.clientY,
        originScrollTop: scroller.scrollTop,
        deltaMinutes: 0,
        dayDelta: 0,
        armed: false,
        // Only true while the activation delay is still counting down. Movement abandons the
        // gesture in that window and nowhere else - once the arm call is in flight the pointer is
        // expected to be moving, and abandoning there would break every mouse drag.
        awaitingDelay: false,
        guide: null,
        timer: null,
        raf: null,
        lastEvent: e,
        validating: false,
        validatedKey: null
    };
    state.drag = drag;

    // A mouse drag on a desktop calendar is expected to be instantaneous; only touch is ambiguous
    // with a scroll, so only touch waits out the long press.
    const delay = e.pointerType === 'mouse' ? 0 : (o.activationDelayMs ?? 350);
    if (delay <= 0) {
        arm(state, drag);
    }
    else {
        drag.awaitingDelay = true;
        drag.timer = setTimeout(() => arm(state, drag), delay);
    }
}


async function arm(state, drag) {
    drag.awaitingDelay = false;
    if (state.drag !== drag || drag.armed) return;

    let allowed = false;
    try {
        allowed = await state.dotNetRef.invokeMethodAsync('OnDragArm', drag.eventId);
    }
    catch {
        allowed = false;
    }

    // Released, or handed to the scroller, while the arm call was in flight.
    if (state.drag !== drag) return;
    if (!allowed) {
        state.drag = null;
        return;
    }

    drag.armed = true;
    drag.originalStyle = drag.el.getAttribute('style') ?? '';
    drag.baseTop = drag.el.offsetTop;
    drag.baseHeight = drag.el.offsetHeight;
    drag.originScrollTop = drag.scroller.scrollTop;
    drag.el.classList.add('is-dragging');

    try { drag.el.setPointerCapture(drag.pointerId); } catch { /* pointer already gone */ }

    // Catch up to wherever the pointer got to while the arm call was in flight.
    recompute(state, drag, drag.lastEvent);
    render(state, drag);
}


function onPointerMove(state, e) {
    const drag = state.drag;
    if (!drag) return;
    drag.lastEvent = e;

    if (!drag.moved &&
        (Math.abs(e.clientX - drag.originX) > SLOP || Math.abs(e.clientY - drag.originY) > SLOP))
        drag.moved = true;

    if (!drag.armed) {
        // Moved before the long press elapsed - this was a scroll all along. Bail silently.
        if (drag.awaitingDelay &&
            (Math.abs(e.clientX - drag.originX) > SLOP || Math.abs(e.clientY - drag.originY) > SLOP)) {
            clearTimeout(drag.timer);
            state.drag = null;
        }
        return;
    }

    e.preventDefault();
    recompute(state, drag, e);
    render(state, drag);
    autoScroll(state, drag);
}


function recompute(state, drag, e) {
    const o = state.options;

    // Track the finger in *content* coordinates: auto-scroll moves the timeline under a stationary
    // pointer, and the target time has to keep moving with it.
    const rawY = (e.clientY - drag.originY) + (drag.scroller.scrollTop - drag.originScrollTop);
    drag.deltaMinutes = snapMinutes(rawY / pxPerMinute(state), o.snapMinutes);

    drag.dayDelta = 0;
    if (drag.kind === KIND_MOVE && o.allowCrossDay && o.days > 1 && drag.columnWidth > 0) {
        const raw = Math.round((e.clientX - drag.originX) / drag.columnWidth);
        drag.dayDelta = Math.max(-drag.sourceDayIndex, Math.min(o.days - 1 - drag.sourceDayIndex, raw));
    }

    if (o.perPositionValidation)
        validate(state, drag);
}


// The slower path: one interop call per snap boundary crossed, never per frame.
async function validate(state, drag) {
    const key = `${drag.kind}:${drag.deltaMinutes}:${drag.dayDelta}`;
    if (drag.validating || drag.validatedKey === key) return;

    drag.validating = true;
    try {
        const ok = await state.dotNetRef.invokeMethodAsync(
            'OnDragValidate', drag.eventId, drag.kind, drag.deltaMinutes, drag.dayDelta);

        if (state.drag !== drag) return;
        drag.validatedKey = key;
        drag.rejected = !ok;
        drag.el.classList.toggle('is-rejected', !ok);
    }
    catch { /* a failed probe must not wedge the drag */ }
    finally {
        drag.validating = false;
    }
}


function render(state, drag) {
    const perMinute = pxPerMinute(state);
    const dy = drag.deltaMinutes * perMinute;
    const minHeight = Math.max(1, (state.options.minDurationMinutes || 15) * perMinute);
    const dayHeight = MINUTES_PER_DAY * perMinute;

    let top = drag.baseTop;
    let height = drag.baseHeight;

    if (drag.kind === KIND_MOVE) {
        top = drag.baseTop + dy;
    }
    else if (drag.kind === KIND_RESIZE_START) {
        top = drag.baseTop + dy;
        height = drag.baseHeight - dy;
        if (height < minHeight) {
            top = drag.baseTop + drag.baseHeight - minHeight;
            height = minHeight;
        }
    }
    else {
        height = Math.max(minHeight, drag.baseHeight + dy);
    }

    top = Math.max(0, Math.min(top, dayHeight - Math.min(height, dayHeight)));

    drag.el.style.top = `${top}px`;
    drag.el.style.height = `${height}px`;
    drag.el.style.transform = drag.dayDelta ? `translateX(${drag.dayDelta * drag.columnWidth}px)` : '';

    renderGuide(state, drag, top);
}


function renderGuide(state, drag, top) {
    const cols = columns(state);
    const target = cols[drag.sourceDayIndex + drag.dayDelta] ?? drag.column;
    const guide = target ? target.querySelector('.shiny-agenda-snapguide') : null;

    if (drag.guide && drag.guide !== guide)
        drag.guide.style.display = 'none';

    drag.guide = guide;
    if (!guide) return;

    guide.style.display = 'block';
    guide.style.top = `${top}px`;
}


// Without this you cannot drag an event from 09:00 to 18:00 on a phone: the destination is not
// on screen.
function autoScroll(state, drag) {
    if (drag.raf) return;

    const step = () => {
        drag.raf = null;
        if (state.drag !== drag || !drag.armed) return;

        const e = drag.lastEvent;
        const rect = drag.scroller.getBoundingClientRect();
        let dy = 0;
        if (e.clientY < rect.top + EDGE) dy = -AUTO_STEP;
        else if (e.clientY > rect.bottom - EDGE) dy = AUTO_STEP;
        if (dy === 0) return;

        const before = drag.scroller.scrollTop;
        drag.scroller.scrollTop = before + dy;
        if (drag.scroller.scrollTop === before) return;

        recompute(state, drag, e);
        render(state, drag);
        drag.raf = requestAnimationFrame(step);
    };
    drag.raf = requestAnimationFrame(step);
}


async function onPointerUp(state, e) {
    const drag = state.drag;
    if (!drag) return;

    clearTimeout(drag.timer);
    state.drag = null;

    if (!drag.armed) return;

    restore(drag);
    try { drag.el.releasePointerCapture(drag.pointerId); } catch { /* already released */ }

    // A pointer drag still produces a trailing click, which would fire the event's @onclick and
    // "select" the thing you just dropped. Same problem as MAUI's trailing tap, different mechanism.
    // Only a press that actually moved is a drag, though: a mouse arms on pointerdown with no delay,
    // so swallowing the click unconditionally meant a plain click could never select an event.
    if (drag.moved)
        suppressNextClick(drag.el);

    if (drag.deltaMinutes === 0 && drag.dayDelta === 0) return;

    try {
        await state.dotNetRef.invokeMethodAsync(
            'OnDragCommit', drag.eventId, drag.kind, drag.deltaMinutes, drag.dayDelta);
    }
    catch { /* the component reverts and reports; nothing useful to do here */ }
}


function cancel(state) {
    const drag = state.drag;
    if (!drag) return;
    clearTimeout(drag.timer);
    state.drag = null;
    if (drag.armed) restore(drag);
}


// Hand the element back exactly as Blazor last rendered it, so the renderer's diff still matches
// the DOM and the next render lands.
function restore(drag) {
    if (drag.raf) cancelAnimationFrame(drag.raf);
    drag.raf = null;

    drag.el.classList.remove('is-dragging', 'is-rejected');
    if (drag.originalStyle)
        drag.el.setAttribute('style', drag.originalStyle);
    else
        drag.el.removeAttribute('style');

    if (drag.guide) drag.guide.style.display = 'none';
    drag.guide = null;
}


function suppressNextClick(el) {
    const swallow = ev => {
        ev.stopPropagation();
        ev.preventDefault();
        el.removeEventListener('click', swallow, true);
    };
    el.addEventListener('click', swallow, true);

    // If no click arrives (some touch stacks), don't leave the listener armed for the next tap.
    setTimeout(() => el.removeEventListener('click', swallow, true), 400);
}
