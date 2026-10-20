# Motion Icons

[← All Shiny Controls](../../README.md)

111 hand-drawn animated icons that run **on a timer, on hover, on tap, when they scroll into view, or on command** — a bell that rings from its crown, a hamburger that morphs into a cross, a tick that draws itself on, a folder tab that lifts off its crease. `MotionIconView` on MAUI, `<MotionIcon>` on Blazor, both in the core packages; the artwork and the motion live in `Shiny.Controls.MotionIcons.Shared` so the two hosts render the same drawing running the same curves.

```xml
<!-- MAUI — the standard shiny xmlns, no extra prefix -->
<shiny:MotionIconView Icon="bell"
                      Trigger="Loop"
                      Interval="0:0:1.5"
                      Color="{AppThemeBinding Light=#2563EB, Dark=#60A5FA}"
                      WidthRequest="32"
                      HeightRequest="32" />
```

```razor
@* Blazor — Color defaults to currentColor, so it inherits the text around it *@
<MotionIcon Icon="bell" Trigger="MotionTrigger.Loop" Interval="TimeSpan.FromSeconds(1.5)" Size="32" />
```

**Triggers** are a `[Flags]` enum and combine: `Loop`, `Hover`, `Press`, `Appear`, or `Manual` for `Play()` / `Stop()` / binding `IsPlaying` to a busy flag. `Interval` rests the icon between cycles. `RepeatCount` bounds a triggered play (`Play()`, Press, Appear); setting `IsPlaying` to `true` loops until it is cleared, on both hosts.

**Presets** work on any icon, including your own artwork — `Pulse`, `Beat`, `Spin`, `Shake`, `Wobble`, `Bounce`, `Float`, `Pop`, `Tada`, `Flip`, `Swing`, `Blink`, `Draw`, `Nudge`, `Jiggle`. `Default` plays the motion drawn for that icon and falls back to `Pulse` for artwork that has none.

**The set**, grouped by what each icon is for — one flat, case-insensitive namespace, so enumerate it with `MotionIconLibrary.Names`:

| Group | Icons |
|---|---|
| Actions | `add-user`, `attach`, `check`, `close`, `copy`, `download`, `edit`, `filter`, `link`, `loader`, `logout`, `menu`, `more`, `pin`, `plus`, `power`, `print`, `redo`, `refresh`, `save`, `search`, `send`, `settings`, `share`, `sort`, `trash`, `undo`, `upload`, `zoom-in`, `zoom-out` |
| Navigation | `arrow-down`, `arrow-left`, `arrow-right`, `arrow-up`, `chevron-down`, `chevron-left`, `chevron-right`, `chevron-up`, `collapse`, `compass`, `expand`, `external-link` |
| Objects | `battery`, `bell`, `bookmark`, `calendar`, `camera`, `cart`, `clock`, `cloud`, `coffee`, `credit-card`, `eye`, `flag`, `gift`, `heart`, `home`, `lightbulb`, `location`, `lock`, `mail`, `message`, `rocket`, `shield`, `star`, `sun`, `tag`, `user` |
| Media | `headphones`, `microphone`, `music`, `mute`, `pause`, `play`, `record`, `repeat`, `shuffle`, `skip-back`, `skip-forward`, `stop`, `video`, `volume` |
| Files | `book`, `clipboard`, `database`, `file`, `file-text`, `folder`, `folder-open`, `image` |
| Weather | `cloud-rain`, `cloud-snow`, `droplet`, `lightning`, `moon`, `thermometer`, `umbrella`, `wind` |
| Indicators | `activity`, `bluetooth`, `chart`, `check-circle`, `help`, `hourglass`, `info`, `signal`, `thumbs-up`, `trending-up`, `warning`, `wifi`, `x-circle` |

**Bring your own artwork** with `PathData="M12 2L3 20H21z"` for a quick glyph, a `MotionIconDefinition` for something split into moving parts, or `MotionIconLibrary.Register(...)` to replace a built-in across the whole app.

**Notes:**
- The two hosts run the *same* spec through different machinery: on MAUI it compiles to a `KeyframeScene` driven by the [Keyframe](keyframe.md) engine's `Player`, so motion icons and hand-written timelines share one animation engine, one clock per window and one set of easing curves. On Blazor it compiles to `@keyframes` once and the browser composites it, so no C# runs per frame and the animation keeps going while WebAssembly is busy.
- Because nothing touches a platform SDK, the MAUI side works on every head — including AppKit and GTK4.
- Playback never starts before the view is loaded, so an implicit `<Style TargetType="MotionIconView">` that sets `IsPlaying` cannot reach for a dispatcher from inside a constructor.
- Leaving the tree ends a play the icon started itself. When the view is unloaded mid-play (its page went away, or its parent was moved), a `Play()`, `Loop()`, Press, Hover or `Appear` play stops at rest and drops `IsPlaying`, and the next `Loaded` does not replay it — only `Appear` plays again, as it does on every load. `IsPlaying` set by the app (a local value, a `Binding`, a `Style`) stays `true` across the gap and resumes looping at the next `Loaded`, and so does `Trigger="Loop"`. Setting `IsPlaying = true` while the icon's own play is running hands it to the app as a loop. `ShinyTabBar`, which a Shell moves onto the next page right after it plays the new tab's icon, replays that icon itself once it has landed.
- A hidden icon does not animate. Setting `IsVisible="False"` on a playing `MotionIconView` pauses it — no redraws, no `Progress` writes, and the shared timer sleeps if nothing else is moving — and showing it again resumes mid-cycle. `IsPlaying` stays `true` throughout, so toggling one icon per state by visibility is cheap. Only the view's own `IsVisible` counts; hiding a parent layout does not pause it. On Blazor a hidden element runs no CSS animation anyway.
- Unset `Color` follows the theme pack's on-surface token, so icons restyle with a live `SetTheme`. The token is only a fallback: a `Color` from a local value, a `Style` setter, a `Binding` or an `AppThemeBinding` always wins.
- `Trigger` can change at any time — switching to `Loop` (or `Appear`) after the icon has loaded starts it, and switching away from `Loop` stops it.
- Every generic preset works on artwork the library has never seen; only `Draw` needs to know the parts, so it can stagger them.
- `prefers-reduced-motion` is honoured on the web: the icon renders and still responds, it just holds its resting pose.
- **Write path data with explicit `L` commands.** `Microsoft.Maui.Graphics` does not implement SVG's implicit-lineto rule (`M6 6 18 18` becomes two *movetos*, not a line) and cannot read run-together decimals (`l.06.06`). Browsers handle both, so artwork copied from a design tool can look perfect on Blazor and draw nothing on MAUI. Unit tests guard the built-in set against both.
