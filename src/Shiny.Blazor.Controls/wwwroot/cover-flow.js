// Shiny cover flow engine. One instance per stage element, keyed by an id from C#: by the time a
// dispose arrives the stage may already be out of the DOM, and a removed element arrives as null.
//
// The whole control is one continuous scalar, `pos`: the (fractional) index sitting in the centre.
// Every slide's transform is a function of `index - pos`, so a drag, a settle animation and a jump
// are all just ways of moving that one number. The server renders only a window of slides around
// the centre (data-index carries each one's virtual index — unbounded when looping), and
// `update` is called after every render so slides that arrive mid-motion pick up their transform.
//
// C# callbacks: OnCenterChanged(virtualIndex) while moving, OnSettled(virtualIndex) when motion
// stops, OnItemClicked(virtualIndex) for a tap on the centre slide.

const states = new Map();

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const now = () => performance.now();
const easeOut = t => 1 - Math.pow(1 - t, 3);

export function init(id, root, stage, dotnetRef, options) {
    dispose(id);
    if (!root || !stage) return;

    const state = {
        root,
        stage,
        dotnet: dotnetRef,
        opts: normalize(options),
        slides: [],
        pos: options.center ?? 0,
        anim: null,
        raf: 0,
        lastCenter: options.center ?? 0,
        settledAt: options.center ?? 0,
        // drag
        pointerId: null,
        startX: 0,
        startPos: 0,
        moved: false,
        samples: [],
        wheelTimer: 0,
        reducedMotion: window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
    };
    states.set(id, state);

    collect(state);
    apply(state);
    wire(state);
    root.setAttribute('data-ready', '');
}

export function update(id, options) {
    const state = states.get(id);
    if (!state) return;
    state.opts = normalize(options);
    collect(state);

    // Shrinking the list (or turning looping off) can strand the centre outside the items.
    if (!state.opts.loop && state.pointerId === null) {
        const max = Math.max(0, state.opts.count - 1);
        if (state.pos < 0 || state.pos > max) {
            const target = clamp(Math.round(state.pos), 0, max);
            state.anim = null;
            state.pos = target;
            reportCenter(state);
            settled(state);
        }
    }
    apply(state);
}

/** Move to a virtual index; `animate=false` jumps without reporting a settle back. */
export function goTo(id, index, animate) {
    const state = states.get(id);
    if (!state) return;
    animateTo(state, clampIndex(state, index), animate !== false);
}

export function next(id) {
    const state = states.get(id);
    if (state) animateTo(state, clampIndex(state, currentTarget(state) + 1), true);
}

export function prev(id) {
    const state = states.get(id);
    if (state) animateTo(state, clampIndex(state, currentTarget(state) - 1), true);
}

export function dispose(id) {
    const state = states.get(id);
    if (!state) return;
    cancelAnimationFrame(state.raf);
    clearTimeout(state.wheelTimer);
    state.unwire?.();
    states.delete(id);
}

function normalize(o) {
    return {
        count: o.count | 0,
        loop: !!o.loop,
        sideAngle: +o.sideAngle,
        sideScale: +o.sideScale,
        sideOpacity: +o.sideOpacity,
        centerSpacing: Math.max(1, +o.centerSpacing),
        itemSpacing: +o.itemSpacing,
        visibleItems: Math.max(0, o.visibleItems | 0),
        duration: Math.max(0, o.animationDuration | 0),
        swipe: !!o.isSwipeEnabled
    };
}

function collect(state) {
    state.slides = Array.from(state.stage.querySelectorAll(':scope > [data-index]'))
        .map(el => ({ el, index: +el.getAttribute('data-index') }));
}

function clampIndex(state, i) {
    return state.opts.loop ? Math.round(i) : clamp(Math.round(i), 0, Math.max(0, state.opts.count - 1));
}

function currentTarget(state) {
    return state.anim ? state.anim.to : Math.round(state.pos);
}

// ----- layout -----

function apply(state) {
    const o = state.opts;
    const fadeEnd = o.visibleItems + 1;

    for (const s of state.slides) {
        const d = s.index - state.pos;
        const a = Math.abs(d);
        const sign = d < 0 ? -1 : 1;

        const x = a <= 1 ? d * o.centerSpacing : sign * (o.centerSpacing + (a - 1) * o.itemSpacing);
        const rot = -clamp(d, -1, 1) * o.sideAngle;
        const scale = 1 - Math.min(a, 1) * (1 - o.sideScale);

        // Full strength out to the first neighbour, easing to SideOpacity at the last visible one,
        // then fading to nothing over one more step so items never pop in or out.
        let opacity = a <= 1 || o.visibleItems <= 1
            ? 1
            : 1 + (o.sideOpacity - 1) * Math.min(1, (a - 1) / (o.visibleItems - 1));
        if (a > o.visibleItems) opacity *= Math.max(0, fadeEnd - a);

        const st = s.el.style;
        st.transform = `translateX(${x.toFixed(2)}px) rotateY(${rot.toFixed(2)}deg) scale(${scale.toFixed(4)})`;
        st.opacity = clamp(opacity, 0, 1).toFixed(3);
        st.zIndex = String(1000 - Math.round(a * 10));
        st.visibility = a >= fadeEnd ? 'hidden' : '';
        s.el.toggleAttribute('data-center', a < 0.5);
    }
}

function reportCenter(state) {
    const c = Math.round(state.pos);
    if (c !== state.lastCenter) {
        state.lastCenter = c;
        state.dotnet.invokeMethodAsync('OnCenterChanged', c).catch(() => { });
    }
}

function settled(state) {
    const c = Math.round(state.pos);
    state.settledAt = c;
    state.dotnet.invokeMethodAsync('OnSettled', c).catch(() => { });
}

function setPos(state, p) {
    state.pos = p;
    apply(state);
    reportCenter(state);
}

// ----- motion -----

function animateTo(state, target, animate) {
    cancelAnimationFrame(state.raf);
    const duration = animate && !state.reducedMotion ? state.opts.duration : 0;

    if (duration <= 0 || Math.abs(target - state.pos) < 1e-3) {
        state.anim = null;
        setPos(state, target);
        if (animate || target !== state.settledAt) settled(state);
        return;
    }

    // A long jump gets a little longer, but never drags on.
    const span = Math.abs(target - state.pos);
    state.anim = {
        from: state.pos,
        to: target,
        start: now(),
        duration: duration * Math.min(2, 1 + Math.log2(Math.max(1, span)) * 0.25)
    };

    const step = () => {
        const a = state.anim;
        if (!a) return;
        const t = clamp((now() - a.start) / a.duration, 0, 1);
        setPos(state, a.from + (a.to - a.from) * easeOut(t));
        if (t < 1) {
            state.raf = requestAnimationFrame(step);
        } else {
            state.anim = null;
            settled(state);
        }
    };
    state.raf = requestAnimationFrame(step);
}

function rubberBand(state, p) {
    if (state.opts.loop) return p;
    const max = Math.max(0, state.opts.count - 1);
    if (p < 0) return p * 0.35;
    if (p > max) return max + (p - max) * 0.35;
    return p;
}

// ----- input -----

function wire(state) {
    const { stage } = state;
    const DRAG_SLOP = 6;

    const down = e => {
        if (e.button !== undefined && e.button !== 0) return;
        // No capture until the pointer actually drags (capturing on down would retarget clicks meant
        // for buttons inside a template), so a press released outside the stage is never heard from
        // again — a new press simply takes over.
        state.pointerId = e.pointerId;
        state.startX = e.clientX;
        state.startPos = state.pos;
        state.moved = false;
        state.samples = [{ t: now(), p: state.pos }];
        state.downSlide = e.target.closest?.('[data-index]');
        if (state.anim) {
            // Catch the motion where it is, as a finger would.
            cancelAnimationFrame(state.raf);
            state.anim = null;
            state.moved = true;
        }
    };

    const move = e => {
        if (e.pointerId !== state.pointerId || !state.opts.swipe) return;
        const dx = e.clientX - state.startX;
        if (!state.moved) {
            if (Math.abs(dx) < DRAG_SLOP) return;
            state.moved = true;
        }
        if (!stage.hasPointerCapture?.(e.pointerId)) {
            try { stage.setPointerCapture(e.pointerId); } catch { }
            stage.classList.add('is-dragging');
        }
        const raw = state.startPos - dx / state.opts.centerSpacing;
        setPos(state, rubberBand(state, raw));
        const t = now();
        state.samples.push({ t, p: state.pos });
        while (state.samples.length > 2 && t - state.samples[0].t > 100) state.samples.shift();
        e.preventDefault();
    };

    const up = e => {
        if (e.pointerId !== state.pointerId) return;
        state.pointerId = null;
        stage.classList.remove('is-dragging');
        try { stage.releasePointerCapture(e.pointerId); } catch { }

        if (!state.moved) {
            // A tap: a side slide comes to the centre, the centre slide is selected.
            const slide = state.downSlide;
            if (slide && e.type === 'pointerup') {
                const index = +slide.getAttribute('data-index');
                if (index === Math.round(state.pos) && Math.abs(index - state.pos) < 0.5)
                    state.dotnet.invokeMethodAsync('OnItemClicked', index).catch(() => { });
                else
                    animateTo(state, clampIndex(state, index), true);
            } else {
                animateTo(state, clampIndex(state, state.pos), true);
            }
            return;
        }

        const first = state.samples[0];
        const last = state.samples[state.samples.length - 1];
        const dt = (last.t - first.t) / 1000;
        const v = dt > 0 && now() - last.t < 80 ? (last.p - first.p) / dt : 0;
        animateTo(state, clampIndex(state, state.pos + v * 0.25), true);
    };

    const wheel = e => {
        if (!state.opts.swipe) return;
        const dx = Math.abs(e.deltaX) > Math.abs(e.deltaY) ? e.deltaX : (e.shiftKey ? e.deltaY : 0);
        if (!dx) return;
        e.preventDefault();
        cancelAnimationFrame(state.raf);
        state.anim = null;
        setPos(state, rubberBand(state, state.pos + dx / state.opts.centerSpacing));
        clearTimeout(state.wheelTimer);
        state.wheelTimer = setTimeout(() => animateTo(state, clampIndex(state, state.pos), true), 140);
    };

    // A drag that ends on a slide would otherwise fire a click the browser synthesizes.
    const click = e => { if (state.moved) { e.preventDefault(); e.stopPropagation(); } };
    const dragstart = e => e.preventDefault();

    stage.addEventListener('pointerdown', down);
    stage.addEventListener('pointermove', move);
    stage.addEventListener('pointerup', up);
    stage.addEventListener('pointercancel', up);
    stage.addEventListener('wheel', wheel, { passive: false });
    stage.addEventListener('click', click, true);
    stage.addEventListener('dragstart', dragstart);

    state.unwire = () => {
        stage.removeEventListener('pointerdown', down);
        stage.removeEventListener('pointermove', move);
        stage.removeEventListener('pointerup', up);
        stage.removeEventListener('pointercancel', up);
        stage.removeEventListener('wheel', wheel);
        stage.removeEventListener('click', click, true);
        stage.removeEventListener('dragstart', dragstart);
    };
}
