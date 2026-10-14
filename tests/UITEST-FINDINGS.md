# UI test findings — bugs, gaps and where we left off

Running log from building the end-to-end UI suites (`tests/Sample.UITests`, `tests/Sample.Blazor.UITests`;
see `tests/UITESTING.md` for how to run them). Started 2026-10-13. Update this file as items are fixed or found.

Every bug below is pinned by a test marked `[Skip("BUG: …")]` or `[SkipOn(UIPlatform.X, "BUG: …")]` that asserts
the **correct** behaviour. To fix a bug: fix it, delete the skip, and the test proves it. Find them all with
`grep -rnE 'Skip(On)?\((UIPlatform\.[A-Za-z]+, )?"(BUG|SUSPECTED)' tests/`.

## Where we left off

| Suite / head | Status |
|---|---|
| Blazor (Chromium, dev server) | **All 21 bugs FIXED (2026-10-13): 690/690 pass, 0 skip.** Unit tests 562/562. Release notes added to the docs repo (1.7.0). Trimmed publish mode: 3 interop trimming bugs found and fixed (affected classes 120/120). Not yet run in WebKit or Firefox. |
| MAUI iOS simulator | **All 8 MAUI bugs + 3 sample bugs FIXED (2026-10-14).** Full run: 599 pass / 6 platform skips / 1 fail (`ThemeTests.Flyout_Dark_Switch_Sets_The_App_Theme`, flaky: passes 4/4 in isolation). MAUI unit tests 1523/1523. Release notes added. |
| MAUI Android emulator (devflow-tests-api35) | **Done 2026-10-14.** Final full run (all fixes): **597 pass / 2 fail / 7 skip** of 606. The 2 are intermittent navigation timeouts that only happen deep into a full run (Shell reports an empty route): `NavigationTests…(nav-right-drafts)` (failed in both full runs, right after Push_And_Pop) and the `marquee` sweep. Both pass when re-run (Navigation+Marquee 15/15). Not chased further. GREF: FrostedGlass + Office Ribbon churn fixed and measured flat; remaining growth is Shell page retention (~2k GREFs/page, Office editors ~18–23k each). |
| MAUI macOS AppKit | 397 pass / 3 fail / 187 skip. The 3 are sweep-only: musiclibrary (now in KnownGaps, not re-run); calendarlist and quickentry (order-dependent empty-route hangs). |
| MAUI Linux GTK | **2026-10-14, real Linux box (Ubuntu, aarch64 VM):** now builds and runs on a clean machine. Deliberately not run to completion: an **upstream maui-labs double free** (see *MAUI Linux GTK — findings*) aborts the app at random whenever the GC finalizes a Graphene.Point, so a full run is mostly noise until it's fixed upstream. Targeted run of 8 classes: 65 total / 26 pass / 32 fail / 7 skip. |
| MAUI Windows | Not run. Run it on Windows: `SHINY_UITEST_PLATFORM=windows dotnet run --project tests/Sample.UITests`. |

Nothing is committed yet.

## MAUI Linux GTK — findings (2026-10-14)

> **Update:** the double free, the legacy-package issue and the unreachable second ShellContent are all **already fixed upstream** in maui-labs `0.1.0-preview.13.26514.2`. v1 now uses that version, and their Linux skips are removed. On preview.13 the Badge idle repro survives 60s (3/3), and Badge, ChipGroup, DataGrid and Dialogs pass. Still open on Linux: the AutoComplete wedge, one Expander test and one PasswordStrength test. The text below describes preview.12.

Found by a Claude session on a real Ubuntu aarch64 VM driving `./uitest linux`.

### Upstream bug: double free in `GtkViewHandler.ApplyTransform` (dotnet/maui-labs)

This is in `Microsoft.Maui.Platforms.Linux.Gtk4` 0.1.0-preview.12.26479.1, decompiled:

```csharp
var point = Graphene.Point.Alloc();
point.Init(x, y);
transform = transform.Translate(point);
point.Free();          // graphene_point_free
```

`Point.Alloc` returns a Gir.Core `OwnedHandle`. The explicit `Free()` releases the native point, then the handle's finalizer releases the same pointer again. Crash dump (createdump, then `dotnet-dump clrstack -f` on the .NET finalizer thread):

```
GObject.Internal.Functions.BoxedFree
 <- Graphene.Internal.PointOwnedHandle.ReleaseHandle
 <- SafeHandle.InternalRelease <- SafeHandle.Finalize <- GC.RunFinalizers
```

glibc then aborts, with "double free or corruption (!prev)", "free(): chunks in smallbin corrupted" or "free(): double free detected in tcache 2".

- **Trigger:** any view whose Scale or Rotation is not 1, at a non-zero X+TranslationX or Y+TranslationY. That covers animated badges, Expander indicators and dialog animations.
- **Why it hits random pages:** the abort happens whenever the GC finalizes the handle, not when the transform is applied. The Badge page dies about 2–19s after opening, with no input; Expander and Dialogs died the same way under test.
- **Not Shiny:** BadgeView only exercises the path. Animating the scale to 0.01 instead of 0 dies the same way, and no `src/` change helps. The Badge page is pinned by `KnownGaps.LinuxBadgeHeapCorruption`.

### Upstream packaging: two GTK backends loaded at once

`Microsoft.Maui.DevFlow.Agent.Gtk` depends on the legacy `Platform.Maui.Linux.Gtk4` 0.6.0, so the app loads both `Platform.Maui.Linux.Gtk4.dll` and `Microsoft.Maui.Platforms.Linux.Gtk4.dll`; the dump's module list confirms it.

### GTK platform gaps

- **Second ShellContent unreachable:** the second ShellContent of a multi-content FlyoutItem (chipgroup, treedatagrid, passwordstrength) can't be reached. Shell never even changes the route, even when navigating from //home, so it's worse than AppKit. Pinned by `KnownGaps.LinuxSiblingShellContentUnreachable`.
- **AutoComplete wedge:** the UI thread spins at 100% CPU inside native `gtk_editable_set_text`, reached via `EntryHandler.MapText` ← `AutoCompleteEntry.OnItemTapped`. Tests that never tap also wedge a few seconds after the dropdown shows. This is probably separate from the double free, since a finalizer abort can't explain a CPU spin. 5 of 6 tests are skipped (`KnownGaps.LinuxAutoCompleteGtkWedge`); `Typing_Filters` passes.
- **Screenshots:** `maui devflow ui screenshot` fails on GTK, so failures there save no PNG. The driver's own screenshot call works.

### AutoComplete wedge: root-caused as far as possible (preview.13 and maui-labs main)

- **Still present on main:** a locally built maui-labs main backend wedges AutoComplete 5/5, the same as preview.13.
- **The loop is in GTK's frame clock, not managed layout:** during the wedge the main thread only runs GDK frame-clock cycles. The single managed frame is the root layout tick (`LayoutHandler.StartRootLayoutTick`). The frame clock (`GDK_PRIORITY_REDRAW`) outranks the dispatcher's idle sources, so every dispatched callback starves.
- **Instrumentation:** zero layout passes and zero invalidations during the wedge. Idle CPU is 0–4% before the trigger, and frames are normally paced (16 ms).
- **Trigger:** a text change while the dropdown is open, combined with fast polling. It's timing-dependent: manual repros went from 3/3 early on to 0/6 later.
- **Unknown:** why the frame clock stops pacing. That needs GTK debug symbols, and debuginfod timed out from the box.
- **Decision:** keep `LinuxAutoCompleteGtkWedge` and file an upstream issue (draft 3 below) rather than a PR.

**Draft upstream issue 3 (dotnet/maui-labs), not filed: GTK4 main loop starved by the frame clock after an Entry text change with an overlay visible.** Include the evidence above, the perf call stack (frame clock → root layout tick), and the trigger: a Shiny AutoCompleteEntry, or an Entry with a ScrollView overlay, polled quickly through DevFlow.

### Targeted run (8 classes, before the Linux skips were added)

| Class | Failures | Verdict |
|---|---|---|
| ChipGroup | 9 | Real gap: sibling ShellContent (now skipped) |
| DataGrid (tree) | 2 | Real gap: sibling ShellContent (now skipped) |
| DataGrid (grid) | 5 | Untriaged: first-page waits |
| Expander | 6 | Crash-induced (upstream double free) |
| Dialogs | 4 | 2 crash-induced, 2 untriaged |
| Docking | 2 | Untriaged: "Unknown panel type 'output'", empty main area |
| ColorPicker | 2 | Untriaged: watchdog lockups |
| CountryAddress | 1 | Untriaged: watchdog lockup |
| DurationPicker | 1 | Untriaged |

### Clean-machine fixes (all in v1)

- `NuGet.config`: the `dotnet11-transport` feed. DevFlow.Driver needs an AndroidSdk tools preview that is only published there.
- `Directory.Build.targets`: drops the Apple TFMs on Linux. The list must be built from literal text, because string property functions escape `;`.
- `Sample.Linux` and `Sample.MacOS`: `DevFlowXamlSourceMapsEnabled=false`.
- Harness:
  - bash fallback when there is no zsh;
  - `IsAlive` capped at 10s;
  - desktop watchdog (`SHINY_UITEST_WATCHDOG_SECONDS`, default 15 on Linux, off elsewhere).
- Tooling: install the maui-labs `maui` CLI for broker discovery (or pass `--port 9223`), and uninstall the legacy `maui-devflow` tool.

### Draft upstream issues (dotnet/maui-labs) — not filed

1. **GtkViewHandler.ApplyTransform double-frees a Graphene.Point.**
   - **Repro:** loop `ScaleToAsync` on a view that isn't at (0,0); run with `MALLOC_CHECK_=3`; the app aborts when the GC finalizes the point.
   - **Fix:** remove `point.Free()`, or dispose the `Point` instead. The `Translate` path in `GtkLayoutPanel` already leaves the point to its handle.
2. **Microsoft.Maui.DevFlow.Agent.Gtk references the legacy `Platform.Maui.Linux.Gtk4` 0.6.0.** Both GTK backends end up in the app. Expected: it should reference `Microsoft.Maui.Platforms.Linux.Gtk4`.

## Open investigation: Android GREF growth / memory

After a full route sweep the Android app holds about 32,500 JNI global refs (the hard limit is about 51,200),
uses about 760 MB PSS, has about 269 MB of native heap and about 10,300 live Android Views. Late in a run it
slows to a crawl, so navigation times out.

GREFs added on each page's **first** visit (Shell keeps every visited root page alive, so growth is
expected on a first visit; only growth on **revisits** is a leak):

| Route | +GREFs | Route | +GREFs |
|---|---|---|---|
| slideviewer | 12,316 | slideeditor | 3,869 |
| documentviewer | 6,212 | gantt | 3,680 |
| notebook | 3,591 | ribbon | 1,695 |
| tabbedpage | 1,406 | kanban | 1,103 |
| spreadsheet | 1,026 | documenteditor | 1,014 |

**Resolved — retention only, no leak (see verdict below).** Original question: whether this is retention only, or a leak. The method is to revisit each route 5×
and check whether GREFs and PSS stay flat. If the leak is in `src/`, fix it. If it is only Shell retention
from the sample, record the numbers here.

**Confirmed (2026-10-13):** this is what failed the Android sweep. Past ~90% of the GREF limit (46,080 of
51,200 on the emulator) monodroid logs `N outstanding GREFs. Performing a full GC!` several times a second and
the main thread stalls 30–95 s (`Choreographer: Skipped 5725 frames`): page types never appear, screenshots
fail, Shell reports route ''. The first-visit numbers above jump around and some go *down* after a GC
(timeline -12,224 right after the Office pages), so a lot of the Office growth is collectable churn, not
retention. Harness guard: Android relaunches after 25 distinct routes, or on the first monodroid-gc GREF
warning for the current pid (`AndroidAppHost.NeedsFreshProcessAsync`).

**Verdict: retention, not a leak (measured 2026-10-13, emulator API 35, `debug.mono.log=gref` + `dumpsys meminfo`).**
Fresh process each, home ⇄ route ×5, GREFs / Android Views read 5 s after each visit:

| Route | launch | visit 1 | visit 2 | visit 5 | Views v1→v5 | Native heap v1→v5 |
|---|---|---|---|---|---|---|
| slideviewer | 3,484 | 3,307 | 3,803 | 3,804 | 959→975 | 118.8→124.5 MB |
| documentviewer | 3,484 | 3,286 | 3,769 | 3,784 | 958→974 | 119.1→124.7 MB |
| gantt | 3,505 | 3,994 | 4,039 | 4,033 | 1,107→1,107 | 101.0→105.5 MB |
| ribbon | 3,486 | 4,845 | 4,845 | 4,850 | 1,305→1,305 | 102.2→108.9 MB |
| kanban | 3,508 | 4,394 | 4,350 | 4,442 | 1,264→1,264 | 105.4→109.3 MB |
| tabbedpage | 3,484 | 4,628 | 4,799 | 4,703 | 1,221→1,113 | 103.0→109.8 MB |
| datagrid | 3,484 | 6,072 | 6,022 | 6,034 | 1,634→1,625 | 101.7→111.2 MB |
| treeview (baseline) | 3,484 | 3,537 | 3,543 | 3,553 | 1,020→1,020 | 98.8→102.5 MB |
| expander (baseline) | 3,484 | 3,965 | 3,991 | 3,990 (v3) | — | 106.2→109.2 MB |

In-page churn is flat too: slideviewer 4 rounds of 5×Next/5×Previous/2×Mode → GREFs 3,254/3,253/3,253/3,253,
Views 945 throughout (native heap +6.2/+1.9/+1.0/+0.7 MB per round — converging, Skia caches); datagrid 4 rounds
of FreezeColumn on/off → GREFs 4,899/5,044/4,905/5,052, Views 1,314/1,346.

The big first-visit numbers in the table above were sequence artefacts: on a fresh process slideviewer costs
~320 GREFs, not 12,316 — the sweep's Office cluster creates collectable churn that is only reclaimed later
(timeline −12,224). The whole-run total is Shell retention (~110 live root pages at ~100–2,500 GREFs each),
so nothing in `src/` was changed for it; the harness relaunches instead. A real app that keeps 100+ heavy
Shell pages alive would hit the same ceiling — that's the sample's navigation model, not a control leak.

**Camera path checked separately (watchdog suspected a camera GREF leak, 2026-10-14):** camera page open 60s
idle → 3,865 / 4,481 / 4,678 … 4,678 (flat from 15s on); after Front/Back flips 4,708 → 4,700 flat for 40s each;
Sepia filter 30s → 4,622 flat; the whole CameraTests class → peak 5,672, no monodroid-gc warning; the `B*`
classes + Camera in one process → ~6k. No leak in `Shiny.Maui.Controls.Camera*` on Android. Harness now also
caps every test at 5 min (`[assembly: Timeout]`), so a process wedged in a GC storm fails one test and the
next PrepareApp relaunches it (`NeedsFreshProcessAsync` sees the monodroid-gc warning).

**LEAK FOUND AND FIXED — FrostedGlassView on Android (2026-10-14).** This, not page retention, drove the
mid-run GREF storms (fresh process at 46k in ~7 min). `BlurPreDrawListener.OnPreDraw`
(`src/Shiny.Maui.Controls/FrostedGlassView.cs`) runs every frame and allocated a new `Bitmap` (never recycled),
`Canvas`, `RenderEffect` and `RoundedOutlineProvider` each time, each a Java peer holding a JNI global ref;
hiding/showing the view inside pre-draw keeps the frames coming even on a static page. Before: on screen
3,814 → 4,385 → 4,972 → 5,476 → 6,051 → 6,618 → 7,186 → 7,730 (10 s steps, ~57 GREFs/s), GC back to 3,847, climbing
again. **Fix:** one capture bitmap + canvas kept and rebuilt only on a size change (old one recycled), the blur
effect and outline provider created once with the blur layer, `Invalidate()` instead of re-setting the same
bitmap, everything disposed with the listener. After: 3,334 / 3,317 / 3,317 / 3,317 / 3,322 / 3,318 / 3,334 /
3,329 / 3,321 over 90 s; blur still renders; FrostedGlassTests 2/2. Other animated pages checked for 20 s dwell
and flat: confetti, marquee, skeleton, motionicons, keyframe, gamepad, progressline (every run type), progressbar.

**Second storm (run 4/5, after the FrostedGlass fix) — retention, not a leak.** A per-test timeline
(`uitest-results/android/timeline.log`, new) put the first `46080 outstanding GREFs` at 03:49:30 in a process
started 03:42:19 that had run AutoComplete → … → DataGrid (4 routes, 2 min of sorting/editing/grouping) →
Diagram → Dialogs → Docking → DocumentEditor: ~20 routes. Checked for repeating growth and found none:
ChipGroup class flat ~6.06k; Chat flat while open (4,360) and after leaving (4,644); DataGrid regroup ×5
oscillates 4.9k–5.6k with Views 1,325–1,561; DataGrid class 3.4k → 9.7k across its 4 routes (retained pages).
So test interactions plus Shell retention fill the table faster than the bare sweep. Harness: Android route cap
per launch lowered 25 → 12 (`SHINY_UITEST_ANDROID_ROUTES_PER_LAUNCH` overrides).

**Correction (2026-10-14): the run-4/5/6 storms were DocumentEditor, not retention.** In every one the first
`46080 outstanding GREFs` came during DocumentEditorTests (run 6: process up at 04:06:33, only Docking before it,
storm 41 s into `Shell_Button_Toggles_ShowShell`). Measured with temporary handler/rebuild logging (removed again):
- Opening //documenteditor creates ~719 handlers in ~10 s; Android Views peak 5,682 and settle at ~3,250 (fresh app
  ~915). 429 of the handlers are GraphicsView-based `OfficeRibbonIconView`s; every ribbon tab is built up front, and
  `Ribbon.Rebuild` runs 25× during construction (tab adds, `Redraw` properties, item `Changed`).
- **Each `ShowShell` toggle rebuilds the whole Ribbon three times** — `ApplyShell` sets `ribbon.ShowQuickAccess`,
  `ApplyFileButton` sets `ApplicationButtonText` and a *new* `Command` for `ApplicationButtonCommand`, all `Redraw`
  properties (`DocumentEditorView.Shell.cs` ShowShellProperty/ApplyShell, `DocumentEditorView.cs` ApplyFileButton)
  — ~980 new handlers per toggle. Views stay ~9.0–9.1k (the old ones are collectable), but from a fresh process the
  **second toggle already hits the 46,080 GREF threshold** (monodroid-gc warnings 0 → 2 → 4 → … per toggle).
  ShowToolbar toggles create nothing.
- Not a leak (no retained growth: Views flat across 6 toggles), so `src/` was not changed under the leak-only
  scope. **Recommended fix:** coalesce `Ribbon.Rebuild` (one rebuild per dispatcher turn once the handler
  exists) and keep one cached File-button `Command` in `DocumentEditorView` — today any host that flips a couple of
  ribbon properties at runtime on Android pays ~1k handlers / ~20k GREFs each time.
- Harness copes: the next test's PrepareApp sees the monodroid-gc warning and relaunches; per-test 5-min cap.
  Route cap restored to 25.

**FIXED by main (Ribbon `DeferRebuild()` + cached File `Command` in the editors + DeferRebuild around every
`BuildBar` and the Spreadsheet `IsInShell` setter) — verified on Android 2026-10-14**, fresh process each,
`debug.mono.log=gref`, ShowShell set and read back 6× (GREFs after each toggle):
| Editor | settled | toggles 1–6 | GC warnings |
|---|---|---|---|
| documenteditor | 18,343 | 11,980 / 12,031 / 11,974 / 17,453 / 12,102 / 17,453 | 0 (before: 46k by toggle 2) |
| spreadsheet | — | 14,329 / 10,321 / 14,369 / 10,354 / 10,440 / 10,385 | 0 (before: storm at its Shell test) |
| slideeditor | — | 21,931 / 14,116 / 21,935 / 14,350 / 14,175 / 21,933 | 0 |
| notebook | idle ~6,025 | no ShowShell property on NotebookEditorView | 0 |
Android Views stay 2.3k–4.7k across toggles. Test classes on Android: SpreadsheetTests 4/4, DocumentEditorTests 5/5,
SlideEditorTests 4/4, NotebookTests 6/6, zero monodroid-gc warnings during any of them.

## MAUI — product bugs (`src/`)

| Control | Head | Bug | Test |
|---|---|---|---|
| **FIXED** CameraView | iOS | **Crash (SIGABRT)** on Facing flip. The session-queue `InvokeOnMainThread` reads `VirtualView.Zoom` after the handler disconnects (`CameraViewHandler.Apple.cs:1052`). | CameraTests `Flip_Swaps_The_Facing` |
| DockHostView | Android | **FIXED 2026-10-14** — **Crash** when setting `IsLocked`. `RebuildAll` re-adds panel views that still have an Android parent (IllegalStateException). A real `adb input tap` on the sample's Lock checkbox kills the app: ViewGroup.AddView ← DockHostView.RebuildAll ← set_IsLocked ← DockingPage.OnLockChanged.  **Fix:** `DockGroupView.Apply` (`src/Shiny.Maui.Controls.Desktop/Docking/Controls/DockGroupView.cs`) now removes a cached panel view from its previous `Layout` before adding it (`if (view.Parent is Layout previous && previous != content) previous.Remove(view);`). Verified: 5/5 DockingTests on Android, two real touches lock/unlock with no crash. | DockingTests `Lock_CheckBox_Locks_The_Host` (skip removed) |
| CarouselGallery | Android | Re-enabling snap (SnapCount 0→1) moves CurrentPosition 0→2 with no user scroll: `LinearSnapHelper` snaps to the *centre* item while `MapCurrentPosition` start-aligns (`ScrollToPositionWithOffset(pos, 0)`), `CarouselGalleryHandler.Android.cs:76-80,161`. No test asserts it; the position test now resets first. | (none) |
| Ribbon | Android | **Crash opening the page** (fresh app): IllegalStateException "The specified child already has a parent" — ViewGroup.AddView ← LayoutHandler.SetVirtualView ← BorderHandler ← the Ribbon root Grid ← ShellFragmentContainer.OnCreateView. Started once the sample's command bindings were corrected (`OpenFileMenuCommand`/`OpenFontDialogCommand`/`InsertPictureCommand` now resolve): each resolving binding fires a `Redraw` property → `Ribbon.Rebuild` re-adds views during first realization (`Ribbon.Render.cs` Rebuild). UIKit reparents silently; Android refuses. Same class of bug as DockHostView. | RibbonTests (class skipped on Android); sweep via `KnownGaps.AndroidRibbonReparentCrash` |
| MediaElement | Android | **Crash** opening the page: `java.lang.AbstractMethodError: abstract method "void androidx.media3.common.Player$Listener.onSurfaceSizeChanged(int, int)"` at `AndroidPlayerListener.n_onSurfaceSizeChanged`. `Platforms/Android/AndroidPlayerListener.cs` doesn't implement `OnSurfaceSizeChanged` (a default interface method in the Media3 1.10 binding) but the Java stub forwards it. Likely fix: add a no-op `OnSurfaceSizeChanged(int, int)` override. | MediaElementTests (class skipped on Android); sweep via `KnownGaps.AndroidMediaElementListenerCrash` |
| CameraView | Android | Selecting a facing the device doesn't have (`Facing=Front` on an AVD with `hw.camera.front=none`) throws `IllegalArgumentException: No available camera can be found` from `BindToLifecycle` inside `MapFacing` (`Platforms/Android/CameraViewHandler.Android.cs` `BindUseCases`, ~line 611) — it escapes the `Facing` setter (unhandled from a real Flip tap) and the property is wedged afterwards (setting Back is ignored, Facing stays Front). Should report via `OnCameraError` instead. | CameraTests `Flip_Swaps_The_Facing` (SkipOn Android) |
| **FIXED** FabMenu | all | `AnimateToStateAsync` returns early while animating, so an `IsOpen=false` during the open animation is dropped: IsOpen reads False but the items stay visible. | FabTests |
| **FIXED** MotionIconView | all | `IsPlaying=true` plays `RepeatCount` (default 1) cycles, then flips IsPlaying back to False. The docs say to bind it to a busy flag, and Blazor loops. | MotionIconsTests |
| **FIXED** TableView / CellBase | all | `ApplyCascadedStyles` never applies the value-text styles, so a table-level value style doesn't reach LabelCell/EntryCell. | TableViewTests |
| **FIXED** TagEntry | all | Pasting a tag that already exists deletes the newest chip: the write made inside TextChanged is deferred and lands as if it were a Backspace. | TagEntryTests |
| **FIXED** ZoomPanView | all | A Reset/ZoomLevel change during a zoom animation is lost: the view ends at 1× while `IsZoomed` stays True. | ZoomPanTests |
| **FIXED** Accordion | all | `ExpandedIndex` stays -1 while item 0 is open, because `EnforceRules` opens it silently and `AllowCollapseAll` never publishes the index (found by code reading). | ExpanderTests |
| **FIXED** (iOS verified; AppKit not re-run) Walkthrough | AppKit (likely all; slow there) | Stopping during auto-start records "Tour completed", and a Start straight afterwards is ignored while the old run unwinds. | WalkthroughTests |

## MAUI — platform gaps (handlers missing, by head)

These are listed in `tests/Sample.UITests/Infrastructure/KnownGaps.cs`, so the sweep and the per-control
tests skip with the reason.

- **macOS AppKit**
  - SkiaSharp `SKCanvasView` has no AppKit handler (throws NotImplementedException), which blocks floorplan, seatingchart and all 6 Office pages.
  - No handler for CarouselGallery, StaggeredGrid or VirtualizedGrid.
  - Shell: switching to a sibling ShellContent in the same item changes the route but never shows the page (treedatagrid, passwordstrength, musicbrowse, musiclibrary).
  - AppKit runs no layout pass when a property or size changes, so Flex, Yoga and Expander only move on a window resize (`KnownGaps.AppKitNoRelayoutOnChange`).
  - Children added after layout never realize, so the DataGrid body is blank.
  - The CollectionView raises no Scrolled event, so ParallaxCollectionView never moves its hero.
  - Entry never raises Focused, so TagEntry Backspace removes nothing.
  - A second photo added while MediaPickerButton shows its carousel aborts the load.
- **Linux GTK**
  - No handler for CarouselGallery, StaggeredGrid or VirtualizedGrid.
  - The second ShellContent of a FlyoutItem is unreachable (chipgroup, treedatagrid, passwordstrength); the route never changes. See *MAUI Linux GTK — findings*.
  - Badge page: upstream maui-labs double free (see *MAUI Linux GTK — findings*).
- **Build:** a plain `dotnet build` of `samples/Sample.MacOS` fails (InitializeComponent/x:Name missing). DevFlow Agent 0.1.0-preview.12.26479.1 double-adds the XAML on net10.0-macos. The workaround is `-p:DevFlowXamlSourceMapsEnabled=false`. Not yet put in the harness or csproj, and not yet re-verified by hand.

## MAUI — sample bugs (`samples/`)

- **FIXED** `RibbonPage.xaml` binds `{Binding OpenFileMenu}`, `{Binding InsertPicture}` and `{Binding OpenFontDialog}`; each is missing the `Command` suffix, so those commands are null. (RibbonTests)
- **FIXED** (confirmed: ShinyButton calls `command.Execute`, which rethrows through async void) `ButtonsViewModel.SaveAsync` lacks `FlowExceptionsToTaskScheduler`, so its deliberate fault is rethrown through async void. **Suspected** from the source, not yet seen to crash. (ButtonsTests)
- Fixed already: the macOS head didn't embed `Assets/embedded-icon.svg`, and its MauiProgram lacked the MinimizedSheetStandalonePage view-model mapping. **FIXED**: the Linux csproj now embeds the SVG too (build not run here).

## Blazor — product bugs (`src/`)

| Control | Bug | Test |
|---|---|---|
| **FIXED** CellBase / TableView | `onclick:stopPropagation` is written as a literal attribute (renders, does nothing), so the switch click bubbles to the row and toggles back (CellBase.cs:249). | TableViewTests |
| **FIXED** RadioCell | `HandleChange` reads `e.Value` as bool, but a radio's change value is "on", so no radio ends up checked. | TableViewTests |
| **FIXED** Ribbon | Making a contextual tab Visible and selecting it in the same render falls back to Home: `EnsureSelection` runs before the tab's Visible lands. | RibbonTests |
| **FIXED** ShinyToolbar | `aria-expanded` is bound to a bool, so it renders as `""` or is missing, never "true"/"false". | ToolbarTests |
| **FIXED** ShinyToolbar | The click-outside backdrop popover renders 14×14px (UA fit-content sizing beats `inset:0`), so an outside click never closes the menu. | ToolbarTests |
| **FIXED** MermaidDiagram | It only re-renders when Diagram changes, so a theme change does nothing. | MermaidTests |
| **FIXED** ProgressLineHost | Config strings are passed without `@` (`BarColor="config.BarColor"`), so the literal text reaches the CSS. | ProgressLineTests ×3 |
| **FIXED** ChatView | Enter-to-send doesn't preventDefault, so a stray newline is left in the composer. | ChatTests |
| **FIXED** ModalView | `ModalButton.OnClick` is `Func<Task>`, not `EventCallback`, so the parent never re-renders and IsOpen=false never closes the modal. | ModalTests |
| **FIXED** DateRangeCalendar | "Next month" does nothing when Months=2: the ShowMonthAsync "already visible" guard returns early. | RangePickersTests |
| **FIXED** Scheduler agenda | With drag/resize on, a plain click never selects: the drag is armed with no delay and `suppressNextClick` swallows the click. | SchedulerTests |
| **FIXED** KanbanView | No `OnDropRejected` for a pointer drop on a blocked lane: kanban.js never calls back. The docs promise it. | KanbanTests |
| **FIXED** RangeSlider | Releasing a drag also fires the track click, which jumps the nearer thumb. | SliderTests |
| **FIXED** PasswordStrength | The Show/Hide label only updates in OnParametersSet, so it is stale after clicking Show. | PasswordStrengthTests |
| **FIXED** QuickEntry | One Escape both clears the suggestion and closes the popup, because the key bubbles to QuickEntryHost. | QuickEntryTests |
| **FIXED** Accordion | `AllowCollapseAll=false` is not applied at runtime. | ExpanderTests |
| **FIXED** Accordion | `LoadContentOnDemand` ignores markup (`@foreach`) children. | ExpanderTests |
| **FIXED** ShinyButton | The "This will fail" demo shows Blazor's unhandled-error bar, because ShinyButton rethrows by design. Either the button or the sample needs a decision. | ButtonTests |
| **FIXED** GamepadView / MediaElement / FileDrop | **Published (trimmed) WASM only.** The interop DTOs lost their constructors (`GamepadJsOptions` positional record → ConstructorContainsNullParameterNames; `MediaStatus`, `FileDropPayload` → DeserializeNoConstructor). Found by `SHINY_BLAZOR_MODE=publish` (21 failures), now kept alive with `DynamicDependency`. | GamepadTests, MediaElementTests, FileDropTests |

## Blazor — sample bugs

- **FIXED** `TextEntryPage.razor` validates email in OnAfterRender without re-rendering, so the error lags one render.
- **FIXED** `AgendaListPage.razor` (and the Calendar page) never re-render on `provider.Changed`.
- **FIXED** DataGrid tree page status text doesn't update.
- Fixed already: Chat sample avatar 404 (`InMemoryChatSessionProvider` now uses `css/shiny_logo.png`).

## Test-harness notes (things that cost time)

- Shell keeps every page and its view-model alive, so tests must reset page state in `OpenPage`.
- Text queries match across every live page, so scope them with `FindBySelector("#Id …")`.
- A page that fails to build can wedge Shell (empty route). The harness now relaunches after a navigation timeout or a "Failed to create page view" line in the app's stdout.
- The DevFlow mutation lease ("Another DevFlow session is driving this app") is ridden out with retries.
- **Android: clean builds ship an app that crashes on any page with a core-package Java peer.** `Shiny.Maui.Controls.Desktop` has no Android TFM, so the Android sample also gets its net10.0 `Shiny.Maui.Controls.dll`; on a clean build the Java stub scanner reads that copy ("Shiny.Maui.Controls - Found 0 Java types", empty `obj/.../android/assets/arm64-v8a/Shiny.Maui.Controls.jlo.xml`), and at runtime `CarouselGalleryHandler+CarouselAdapter`, `StaggeredGridHandler+StaggeredGridAdapter`, `FrostedGlassView+BlurPreDrawListener` … die with "no Java peer type found". Incremental rebuild after deleting the empty scan fixes it; `AndroidAppHost.BuildAsync` now does that automatically. Real fix belongs in the build (give Desktop an Android TFM or don't reference it from the Android head).
- Android: GREF exhaustion from Shell retention cripples the app after ~70 pages; the harness relaunches after 25 distinct routes or on a monodroid-gc warning.
- Android: `monkey` refuses to start on a freshly booted emulator, so the app is launched with `am start` on the resolved activity.
- iOS: simulator permission prompts block the UI, so the harness grants privacy permissions with `simctl privacy`.
- GTK locks up (the UI thread spins in native code while the port stays open). The desktop watchdog kills the app after 15s without an agent answer, and `IsAlive` is capped at 10s, so a hang costs one test, not the whole run.
- GTK on macOS needs the .so→dylib shims and must exec the apphost directly (SIP strips `DYLD_*` through zsh).
