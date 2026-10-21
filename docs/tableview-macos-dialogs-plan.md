# TableView picker cells on macOS (AppKit)

Make every TableView cell that opens a picker or prompt work on the macOS AppKit head (`net10.0-macos`), using native AppKit UI (menus, popovers, alerts) supplied by `Shiny.Maui.Controls.Desktop`.

**Status:** plan only. Nothing implemented.
**Found in:** ShinyNotch's settings window (October 2026). It's built on TableView, and had to avoid every cell below because "dialogs don't paint on the AppKit head". The same app also hit a TableView scroll-height bug on AppKit (Part 2).

## Context

Core `Shiny.Maui.Controls` has no macOS target: an AppKit app gets core's `net10.0` asset, which can't call AppKit. Desktop (`Shiny.Maui.Controls.Desktop`) does target `net10.0-macos`. It already plugs native behaviour into core through internal hooks set at load: `PromptEntryPolish.Handler`, and `NativeMotion.Factory` set by `MotionIconRegistration`'s `[ModuleInitializer]`. This plan uses the same pattern.

The AppKit head has two relevant limits:

- It never realises views added to a layout that's already on screen. The overlay-based UI (Toast, Dialogs, OverlayHost, FloatingPanel) re-parents content into the current page at runtime, so it doesn't paint. This is already documented for Quick Entry.
- Controls map to their native counterparts. `Picker` is an `NSPopUpButton` and `DatePicker` an `NSDatePicker`, which have no "focus to open a dialog" behaviour.

## How each cell opens today

| Cell | Mechanism (core) | Likely on AppKit (verify in Phase 0) |
|---|---|---|
| `TextPickerCell` | Hidden zero-size `Picker`, then `Focus()` | Nothing opens: a focused `NSPopUpButton` doesn't pop its menu. |
| `DatePickerCell` | Hidden zero-size `DatePicker`, then `Focus()` | Nothing opens: `NSDatePicker` is inline, with no dialog. |
| `TimePickerCell` | Hidden zero-size `TimePicker`, then `Focus()` | Same as DatePicker. |
| `NumberPickerCell` | `page.DisplayPromptAsync(...)` | Depends on whether maui-labs implements alerts with text input on AppKit. Unknown. |
| `PickerCell` | `page.Navigation.PushAsync(new PickerPage(this))` | Needs a `NavigationPage`, which many desktop windows don't have. Navigation support on the AppKit head is unverified. |
| `DurationPickerCell` | `FloatingPanel` in the page's `OverlayHost` | Overlay content doesn't paint on AppKit. Also needs `ShinyContentPage`. |

Read-only and in-place cells are unaffected: `LabelCell`, `SwitchCell`, `CheckboxCell`, `SimpleCheckCell`, `RadioCell`, `EntryCell`, `CommandCell`, `ButtonCell` and `CustomCell`. ShinyNotch's settings page uses these on AppKit today, and they render and respond.

## Phase 0: verify on the AppKit head

Run `samples/Sample.MacOS`, which already compiles in `Sample/Features/TableView` including `PickerDemoPage`. Tap each picker cell and record exactly what happens: nothing, an exception, a crash, or it works. Do the same in `Sample` on Mac Catalyst, where the cells go through UIKit and are expected to work. Fill the table above with facts.

**Exit criteria:** a list of the cells that are actually broken on AppKit. Cells that already work drop out of Phase 1.

## Phase 1: a native presenter hook

### Core: `CellPickerPresenter`

An internal hook in core, the same shape as `NativeMotion`:

```csharp
internal interface ICellPickerPresenter
{
    // Each returns null when the user cancels.
    Task<int?> PickIndexAsync(View anchor, string? title, IReadOnlyList<string> items, int selectedIndex);
    Task<DateTime?> PickDateAsync(View anchor, DateTime? value, DateTime min, DateTime max);
    Task<TimeSpan?> PickTimeAsync(View anchor, TimeSpan value, bool use24Hour, int minuteInterval);
    Task<TimeSpan?> PickDurationAsync(View anchor, TimeSpan? value, TimeSpan min, TimeSpan max, int minuteInterval);
    Task<int?> PromptNumberAsync(View anchor, string title, int? value, int min, int max, string unit);
    Task<IReadOnlyList<int>?> PickManyAsync(View anchor, string title, IReadOnlyList<string> items, IReadOnlyList<int> selected, int maxSelected);
}

internal static class CellPickers
{
    // Set by a platform that can't use the cells' built-in pickers. Null: the cells behave as today.
    public static ICellPickerPresenter? Presenter { get; set; }
}
```

Each cell checks `CellPickers.Presenter` first and falls back to its current mechanism. That keeps every existing platform on today's code path, unchanged. Each cell's existing `SelectedCommand`, two-way properties and value-text formatting stay in the cell, so the presenter returns raw values only.

### Desktop: `MacCellPickerPresenter` (`Platforms/MacOS`)

Registered by a `[ModuleInitializer]` like `MotionIconRegistration`, so there's nothing to opt into. Everything is anchored to the tapped cell's native view, using its frame converted into the window.

| Picker | AppKit UI | Why |
|---|---|---|
| Index (`TextPickerCell`, single-select `PickerCell`) | `NSMenu` popped up at the cell (`popUpMenuPositioningItem:atLocation:inView:`), with the current item checked and positioned under the pointer | It's exactly what an `NSPopUpButton` does, so it feels native. Handles long lists with menu scrolling. |
| Date | `NSPopover` holding an `NSDatePicker` (`.clockAndCalendar` style, date elements only) with min/max | The standard macOS inline calendar. |
| Time | `NSPopover` with an `NSDatePicker` (`.textFieldAndStepper`, hour/minute elements), snapping to `MinuteInterval` on commit | No clock-face picker exists on macOS; steppers are the convention. Format follows `Use24Hour` through the picker's locale. |
| Duration | `NSPopover` with two `NSPopUpButton`s (hours, minutes in `MinuteInterval` steps), clamped to min/max | Mirrors the current two-picker panel without needing an `OverlayHost`. |
| Number | An `NSAlert` sheet on the cell's window with an `NSTextField` accessory, an `NSNumberFormatter` (integer, min/max) and the unit as a label | The native "prompt" shape. A sheet doesn't block other windows. |
| Many (multi-select `PickerCell`) | `NSPopover` with a checkbox list (`NSTableView` with a checkbox column), Done/Cancel buttons and `MaxSelectedNumber` enforced | A menu can't hold several selections comfortably, and a pushed page needs navigation. |

Popovers use `.transient` behaviour: clicking outside cancels, and **Done** or **Return** commits. The date and time pickers commit when the popover closes, matching how macOS inline pickers behave in System Settings.

### `PickerCell` without navigation

On AppKit, `PickerCell` uses the presenter: the menu for single-select, the popover list for multi-select. Its full-page `PickerPage` stays for every other platform. A `PickerCell` with `ItemTemplate`-style rich rows (`DisplayMember` and `SubDisplayMember`) shows the sub-text as the menu item's secondary line on macOS 14+ (`NSMenuItem.subtitle`), and is dropped on older versions.

### Tests

Core gets unit tests with a fake `ICellPickerPresenter`, as `MotionIconNativePlaybackTests` does for motion icons:

- each cell calls the presenter with its current value, min/max and items;
- a returned value updates the bound property and fires `SelectedCommand`;
- cancelling (null) changes nothing;
- with no presenter, the existing path runs.

The AppKit presenter is checked by hand in `Sample.MacOS` (the `PickerDemoPage` is the checklist), plus one DevFlow UI test per cell if the agent can drive `NSMenu` and `NSPopover`.

## Part 2: TableView cut off on AppKit when it renders after load

### Symptom

ShinyNotch builds its settings page in code, with eight sections. The settings window is created hidden at startup, so the page loads before TableView's first render, which runs on the dispatcher (`ScheduleRender`). On AppKit only the first few sections were reachable. The `ScrollView` kept the content size from its first layout and didn't grow when the rows arrived, so everything past that height was cut off and couldn't be scrolled to. No exception was thrown.

### Workaround in the app

After `ModelChanged`, dispatch `InvalidateMeasure()` on both the TableView and its page. Invalidating only the TableView wasn't enough.

### Fix in TableView

1. Reproduce in `Sample.MacOS`: a code-built TableView page set on a window that is already loaded but hidden, with enough sections to scroll.
2. At the end of `RenderSections`, when the table is already loaded, invalidate the measure of the internal `ScrollView` and its content. If that isn't enough on AppKit, do it on the next dispatcher turn, as the app's workaround does.
3. The underlying cause is probably in the maui-labs AppKit head: measure invalidation from children added after realisation doesn't reach the `NSScrollView` document size. File it on dotnet/maui-labs with the minimal repro from step 1, and link it from the code comment, as `Ui.Pressable` does for maui-labs#744 in ShinyNotch.

## Out of scope

- **Mac Catalyst:** the cells go through UIKit there and are expected to work. Phase 0 confirms it, and if not it becomes a separate item.
- **Linux (GTK4):** the same hook would serve it later with GTK popovers. Not part of this plan.
- **Toast, Dialogs and `OverlayHost` on AppKit in general:** the broader overlay limitation is a separate, larger problem. This plan only removes TableView's dependence on it.

## Required updates when this ships (per CLAUDE.md)

- `docs/controls/tableview.md`: a macOS (AppKit) section describing how each picker cell presents there.
- `SKILLS/shiny-controls/tableview.md`: remove any "avoid picker cells on AppKit" guidance, and note the native presentation.
- Docs site: a release-notes entry in `src/content/docs/controls/release-notes.mdx` (a fix for the pickers, and a fix for the scroll height).

## Open questions

1. Does `DisplayPromptAsync` work on the current maui-labs AppKit head? If it does, `NumberPickerCell` may only need the `NSNumberFormatter` polish, not a new alert.
2. Should the hook be public, so an app can supply its own presenter (on Linux, say) before the library does? Internal is safer for a first version.
3. Is `NSMenuItem.subtitle` (macOS 14+) acceptable given `SupportedOSPlatformVersion` on the AppKit head, or should `PickerCell` sub-text be dropped on macOS?
