// Two jobs:
//  1. measure the intrinsic width of every toolbar item (via an off-screen mirror row) and report back to
//     .NET how many fit before the overflow ("hamburger") button is needed;
//  2. place an open dropdown against the button it hangs off, in the browser's top layer, so a bar living
//     inside a panel or a scroller does not clip its own menus.
const states = new WeakMap();
const hooks = new WeakMap();

export function init(root, dotnet) {
    const inner = root.querySelector('.shiny-toolbar-inner');
    if (!inner) return;

    // re-init on an existing root (items arrived later) must not stack observers
    dispose(root);

    const state = { root, inner, dotnet, lastCount: -1 };
    states.set(root, state);

    // a frame later, coalesced: measure() can re-render the bar, which on WebAssembly happens inside
    // this callback - a layout in the same observer pass is WebKit's "ResizeObserver loop" error
    const ro = new ResizeObserver(() => {
        if (state.roFrame) return;
        state.roFrame = requestAnimationFrame(() => { state.roFrame = 0; measure(state); });
    });
    ro.observe(inner);
    state.ro = ro;

    measure(state);
}

// Called from .NET after each render so item/label changes are picked up
// (ResizeObserver only fires on size changes of the bar itself).
export function remeasure(root) {
    const state = states.get(root);
    if (state) measure(state);
}

export function dispose(root) {
    const hooked = hooks.get(root);
    if (hooked) {
        window.removeEventListener('scroll', hooked.fn, true);
        window.removeEventListener('resize', hooked.fn);
        hooks.delete(root);
    }

    const state = states.get(root);
    if (!state) return;
    state.ro?.disconnect();
    if (state.roFrame) cancelAnimationFrame(state.roFrame);
    states.delete(root);
}

const MARGIN = 8;
const GAP = 6;
const SUB_GAP = 4;

/**
 * Raises every open panel into the top layer and positions it against its button.
 *
 * The popover API is what makes a dropdown usable inside a panel: an in-tree menu is clipped by any
 * `overflow: hidden` ancestor and loses every z-index argument on the page. Where the API is missing the
 * panel stays where the stylesheet put it - absolutely positioned against its own button - and only the
 * flip classes keep it on screen.
 */
export function placeMenus(root, backdrop) {
    hook(root, backdrop);

    // Escape is handled on the bar's root, so focus has to be inside it. A click focuses the button
    // that opened the menu in Chromium, but Safari never focuses a clicked button, so there the
    // keydown went to <body> and Escape did nothing. Moving focus to the first entry is also what the
    // ARIA menu-button pattern asks for.
    if (!root.contains(document.activeElement)) {
        const first = root.querySelector('.shiny-toolbar-menu [role=menuitem]:not([disabled]):not([aria-disabled=true])');
        first?.focus({ preventScroll: true });
    }

    // the scrim is shown first so it sits under the panels in the top layer's paint order
    if (backdrop) raise(backdrop);

    // document order == outermost first, which is also the order the top layer should stack them in
    for (const menu of root.querySelectorAll('.shiny-toolbar-menu')) {
        const floating = raise(menu);
        menu.classList.toggle('shiny-toolbar-menu--floating', floating);

        if (floating)
            position(menu, menu.previousElementSibling, root);
        else
            flip(menu);
    }
}

function raise(el) {
    if (typeof el.showPopover !== 'function')
        return false;

    try {
        if (!el.matches(':popover-open'))
            el.showPopover();
    }
    catch {
        // already open, or not connected to the document yet
    }
    return el.matches(':popover-open');
}

function position(menu, anchor, root) {
    if (!anchor) return;

    const a = anchor.getBoundingClientRect();
    const m = menu.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    const submenu = menu.classList.contains('shiny-toolbar-submenu');

    let left;
    let top;

    if (submenu) {
        // beside its row, flipping to the other side when that side is where the room is
        left = a.right + SUB_GAP;
        if (left + m.width > vw - MARGIN && a.left - m.width - SUB_GAP >= MARGIN)
            left = a.left - m.width - SUB_GAP;

        top = a.top - GAP;
        if (top + m.height > vh - MARGIN)
            top = a.bottom + GAP - m.height;
    }
    else {
        const below = !root.classList.contains('shiny-toolbar--bottom');

        left = a.left;
        if (left + m.width > vw - MARGIN && a.right - m.width >= MARGIN)
            left = a.right - m.width;

        top = below ? a.bottom + GAP : a.top - m.height - GAP;

        // no room on the preferred side? take the other one, but only if it is genuinely better
        if (below && top + m.height > vh - MARGIN && a.top - m.height - GAP >= MARGIN)
            top = a.top - m.height - GAP;
        else if (!below && top < MARGIN && a.bottom + GAP + m.height <= vh - MARGIN)
            top = a.bottom + GAP;
    }

    // written inline rather than through a class so nothing in the stylesheet - or the UA's own popover
    // rules - can win the specificity fight over where the panel sits
    menu.style.position = 'fixed';
    menu.style.margin = '0';
    menu.style.right = 'auto';
    menu.style.bottom = 'auto';
    menu.style.left = Math.round(clamp(left, MARGIN, vw - m.width - MARGIN)) + 'px';
    menu.style.top = Math.round(clamp(top, MARGIN, vh - m.height - MARGIN)) + 'px';
}

function clamp(value, min, max) {
    // a panel taller/wider than the viewport inverts the range; pin to the low edge rather than jumping
    if (max < min) return min;
    return value < min ? min : (value > max ? max : value);
}

// no-popover fallback: the stylesheet anchors the panel, these classes keep it inside the viewport
function flip(menu) {
    const flipX = 'shiny-toolbar-menu--flip-x';
    const flipY = 'shiny-toolbar-menu--flip-y';

    menu.classList.remove(flipX, flipY);

    // a submenu keeps flying out the same way its parent did - flipping one level of a chain on its own
    // walks the panels back over each other
    const parent = menu.parentElement?.closest('.shiny-toolbar-menu');
    if (parent?.classList.contains(flipX))
        menu.classList.add(flipX);

    const r = menu.getBoundingClientRect();
    if (r.right > window.innerWidth - MARGIN && !menu.classList.contains(flipX)) {
        menu.classList.add(flipX);
        if (menu.getBoundingClientRect().left < MARGIN)
            menu.classList.remove(flipX);
    }
    else if (r.left < MARGIN && menu.classList.contains(flipX)) {
        menu.classList.remove(flipX);
        if (menu.getBoundingClientRect().right > window.innerWidth - MARGIN)
            menu.classList.add(flipX);
    }

    if (menu.classList.contains('shiny-toolbar-submenu') &&
        menu.getBoundingClientRect().bottom > window.innerHeight - MARGIN) {
        menu.classList.add(flipY);
        if (menu.getBoundingClientRect().top < MARGIN)
            menu.classList.remove(flipY);
    }
}

// A panel in the top layer is out of flow, so nothing moves it when the page (or the scroller the bar
// lives in) scrolls. Re-place it instead of leaving it pointing at where the button used to be.
function hook(root, backdrop) {
    const existing = hooks.get(root);
    if (existing) {
        existing.backdrop = backdrop;
        return;
    }

    const state = { backdrop };
    state.fn = () => {
        if (root.querySelector('.shiny-toolbar-menu'))
            placeMenus(root, state.backdrop);
    };

    window.addEventListener('scroll', state.fn, true);
    window.addEventListener('resize', state.fn);
    hooks.set(root, state);
}

function measure(state) {
    const { inner, root, dotnet } = state;

    const measureRow = root.querySelector('.shiny-toolbar-measure');
    if (!measureRow) return;

    const items = Array.from(measureRow.querySelectorAll('[data-measure-item]'));
    const total = items.length;
    if (total === 0) return;

    const toggleEl = measureRow.querySelector('[data-measure-toggle]');

    const innerStyle = getComputedStyle(inner);
    const padL = parseFloat(innerStyle.paddingLeft) || 0;
    const padR = parseFloat(innerStyle.paddingRight) || 0;
    const innerGap = parseFloat(innerStyle.gap) || 0;

    // width already consumed by everything in the bar that ISN'T the trailing region
    // (title / start / child content)
    let otherWidth = 0;
    let siblingCount = 0;
    for (const child of inner.children) {
        if (child.classList.contains('shiny-toolbar-trailing')) continue;
        otherWidth += child.getBoundingClientRect().width;
        siblingCount++;
    }

    // ...and by whatever sits beside the items inside the trailing region (EndContent), which is pinned
    // and never collapses
    const trailing = inner.querySelector('.shiny-toolbar-trailing');
    let pinnedWidth = 0;
    let trailingGaps = 0;
    if (trailing) {
        const trailingGap = parseFloat(getComputedStyle(trailing).gap) || 0;
        for (const child of trailing.children) {
            if (child.classList.contains('shiny-toolbar-items')) continue;
            pinnedWidth += child.getBoundingClientRect().width;
            trailingGaps += trailingGap;
        }
    }

    const available = inner.clientWidth - padL - padR - otherWidth - innerGap * siblingCount - pinnedWidth - trailingGaps;

    const itemsRow = root.querySelector('.shiny-toolbar-items');
    const itemGap = itemsRow ? (parseFloat(getComputedStyle(itemsRow).gap) || 0) : 0;

    const widths = items.map(el => el.getBoundingClientRect().width);
    const toggleWidth = toggleEl ? toggleEl.getBoundingClientRect().width : 0;

    // first pass: do they all fit with no overflow button?
    let count = fit(widths, available, itemGap, 0);
    if (count < total) {
        // they don't — reserve room for the overflow button and refit
        count = fit(widths, available, itemGap, toggleWidth + itemGap);
    }
    if (count < 0) count = 0;

    if (count !== state.lastCount) {
        state.lastCount = count;
        dotnet.invokeMethodAsync('SetVisibleCount', count);
    }
}

function fit(widths, available, gap, reserve) {
    let used = 0;
    let count = 0;
    for (let i = 0; i < widths.length; i++) {
        const w = widths[i] + (count > 0 ? gap : 0);
        if (used + w + reserve <= available) {
            used += w;
            count++;
        } else {
            break;
        }
    }
    return count;
}
