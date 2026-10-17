using System.Text.Json;

namespace Sample.Blazor.UITests;

/// <summary>
/// Opens every gallery page on a 360px-wide phone and fails when anything does not fit: the page
/// scrolls sideways, something hangs off either edge, or a button/field is cut off where no
/// scroller can bring it back. Content a control deliberately scrolls (a grid, a code block) or
/// clips (a carousel, a marquee) is left alone — only what the user cannot reach counts.
/// Runs in every Blazor run; `./uitest blazor-mobile` runs the whole suite as a phone as well.
/// </summary>
public class MobileFitTests : BlazorTest
{
    protected override bool Phone => true;

    public static IEnumerable<Func<string>> Routes()
        => BlazorRoutes.All.Select(r => (Func<string>)(() => r));

    [Test]
    [MethodDataSource(nameof(Routes))]
    public async Task Page_Fits_On_A_Phone(string route)
    {
        await Open(route);

        // layout settles after OnAfterRenderAsync interop (measured columns, canvases, observers)
        await Page.WaitForTimeoutAsync(750);
        await Screenshot(Path.Combine("phone", route.Trim('/') is { Length: > 0 } r ? r : "home"));

        var problems = await MeasureAsync();
        if (problems.Count > 0)
            Assert.Fail($"'{route}' does not fit a {await Page.EvaluateAsync<int>("() => document.documentElement.clientWidth")}px phone:\n"
                + String.Join("\n", problems.Take(15)));

        await AssertNoConsoleErrors();
    }

    async Task<List<string>> MeasureAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(FitScript);
        return json.EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    /// <summary>
    /// The page-level scrollers (document, AppLayout regions) never excuse an overflow — a page that
    /// scrolls sideways is the bug. Any other ancestor that scrolls or clips horizontally does: what
    /// it holds is that control's business. Only the outermost offender is reported.
    /// </summary>
    const string FitScript =
        """
        () => {
            const vw = document.documentElement.clientWidth;
            const out = [];
            const describe = el => {
                let s = el.tagName.toLowerCase();
                if (el.id) s += '#' + el.id;
                const cls = [...el.classList].filter(c => !c.startsWith('b-')).slice(0, 3);
                if (cls.length) s += '.' + cls.join('.');
                const owner = el.closest('[class*="shiny-"]');
                if (owner && owner !== el) {
                    const oc = [...owner.classList].find(c => c.startsWith('shiny-'));
                    s += '  (in .' + oc + ')';
                }
                return s;
            };
            const isPageLevel = el =>
                el === document.documentElement || el === document.body ||
                el.id === 'app' || el.classList.contains('page') ||
                [...el.classList].some(c => c.startsWith('shiny-applayout')) ||
                el.classList.contains('content-inner');
            const visible = el => {
                const cs = getComputedStyle(el);
                if (cs.visibility === 'hidden' || cs.display === 'none' || cs.display === 'contents') return false;
                if (el.closest('[aria-hidden="true"], [inert], #shiny-splash, #blazor-error-ui')) return false;
                const r = el.getBoundingClientRect();
                return r.width > 0 && r.height > 0;
            };
            // nearest non-page ancestor that scrolls or clips sideways, and is it a scroller?
            const container = el => {
                for (let a = el.parentElement; a; a = a.parentElement) {
                    if (isPageLevel(a)) continue;
                    const cs = getComputedStyle(a);
                    if (cs.overflowX !== 'visible' || cs.contain.includes('paint')) return a;
                    if (cs.position === 'fixed') return null;
                }
                return null;
            };

            // 1. The page itself scrolls sideways.
            for (const s of [document.scrollingElement, ...document.querySelectorAll('.shiny-applayout-content')]) {
                if (s && s.scrollWidth > s.clientWidth + 1)
                    out.push(`page scrolls sideways: ${describe(s)} is ${s.scrollWidth}px wide in ${s.clientWidth}px`);
            }

            // 2. Elements off either edge that no control-owned scroller/clipper contains.
            const all = [...document.body.querySelectorAll('*')].filter(el => !(el instanceof SVGElement && el.tagName !== 'svg'));
            const offenders = new Set();
            for (const el of all) {
                if (!visible(el)) continue;
                const r = el.getBoundingClientRect();
                const over = r.right > vw + 1, under = r.left < -1;
                if (!over && !under) continue;
                const c = container(el);
                if (c) {
                    // clipped by a control: fine, unless it's an interactive element cut off by a
                    // clipper that cannot scroll it into view
                    const cs = getComputedStyle(c);
                    const scrolls = cs.overflowX === 'auto' || cs.overflowX === 'scroll';
                    const interactive = el.matches('button, a[href], input, select, textarea, [role="button"], [role="tab"], [tabindex]:not([tabindex="-1"])');
                    const cr = c.getBoundingClientRect();
                    const containerFits = cr.left >= -1 && cr.right <= vw + 1;
                    if (scrolls || !interactive || !containerFits) continue;
                    // only flag what is clipped by the container AND off the screen
                    if (r.right <= cr.right + 1 && r.left >= cr.left - 1) continue;
                    offenders.add(el);
                    continue;
                }
                offenders.add(el);
            }
            // 3. Text a non-scrolling container cuts off (a squeezed pane, a fixed-width column).
            //    Ellipsis is a choice, and controls that scroll content through a window by design
            //    (marquees, carousels, cover flow) are skipped; a sample can opt out with data-fit-clip.
            const clipper = el => {
                for (let a = el.parentElement; a; a = a.parentElement) {
                    if (isPageLevel(a)) return null;
                    const cs = getComputedStyle(a);
                    if (cs.overflowX === 'auto' || cs.overflowX === 'scroll') return null;
                    if (cs.overflowX !== 'visible') return a;
                }
                return null;
            };
            const clippedText = new Set();
            const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
            for (let t = walker.nextNode(); t; t = walker.nextNode()) {
                if (!t.textContent.trim()) continue;
                const el = t.parentElement;
                if (!el || !visible(el) || el.closest('[data-fit-clip], .shiny-marquee, .shiny-carousel-viewport, .shiny-coverflow-stage, svg, textarea, select, option')) continue;
                if (getComputedStyle(el).textOverflow === 'ellipsis') continue;
                const c = clipper(el);
                if (!c || getComputedStyle(c).textOverflow === 'ellipsis') continue;
                const cr = c.getBoundingClientRect();
                const range = document.createRange();
                range.selectNodeContents(t);
                const tr = range.getBoundingClientRect();
                if (tr.width === 0) continue;
                // off the clipper's side by a few pixels = a glyph cut in half
                if (tr.right > cr.right + 3 || tr.left < cr.left - 3)
                    clippedText.add(el);
            }
            for (const el of clippedText) {
                if (offenders.has(el)) continue;
                const text = el.textContent.trim().slice(0, 30);
                out.push(`text cut off: ${describe(el)} "${text}"`);
            }

            for (const el of offenders) {
                let parentOffends = false;
                for (let a = el.parentElement; a; a = a.parentElement)
                    if (offenders.has(a)) { parentOffends = true; break; }
                if (parentOffends) continue;
                const r = el.getBoundingClientRect();
                out.push(`${describe(el)}  x ${Math.round(r.left)}..${Math.round(r.right)} (w ${Math.round(r.width)})`);
            }
            return out;
        }
        """;
}
