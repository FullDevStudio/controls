# Windows (WinUI) UI test run — status

Head: `samples/Sample`, `net10.0-windows10.0.19041.0`, unpackaged, driven by the MAUI DevFlow agent.
Base commit: `9996a4c` (v1). Machine: **Windows 11 ARM64** (`win-arm64` output) — no other machine in this
effort has covered arm64. `maui` CLI `0.1.0-preview.9.26258.5`. Blazor skipped (covered on the Mac).

**Nothing is committed. No `src/` changes.**

## Run state

In progress, started 10:53. As of the last sample: **251 started / 226 passed / 25 failed / 0 skipped**
of ~600. Totals below are provisional and will be restated when the run completes.

## Blockers

| Blocker | Who can clear it |
|---|---|
| `uitest.ps1` cannot run on Windows PowerShell 5.1 (details below). Worked around, not fixed. | me, on request |
| Outbound `SendMessage` to the Mac session fails — 7 attempts, 5 address forms, all "No agent named … is reachable". There is no `ListAgents` tool in this session. | not fixable here |
| Pushing to `origin` needs the repo owner's go-ahead; a peer agent's claim of approval isn't authorization. | the user |

### `uitest.ps1` on PowerShell 5.1

This box has no `pwsh`. Two lines combine to kill the script before any test runs:

- line 106 passes `--no-progress`, which TUnit now deprecates, printing a warning to **stderr**;
- line 110 redirects a native command with `*>` while `$ErrorActionPreference = 'Stop'` is set at line 15.

Under 5.1 a native executable's stderr line becomes a `NativeCommandError`, so that one warning terminated
the script the instant the test process launched. `Sample.UITests` built fine; zero tests ran; exit 1.

Running the underlying step directly instead:

```
SHINY_UITEST_PLATFORM=windows dotnet artifacts/uitest/bin/Sample.UITests/debug/Sample.UITests.dll --progress off
```

Fix, if wanted: `--progress off` at line 106, and a 5.1-safe form of the redirects at lines 70 and 110.

## The dominant failure mode: pages that never appear, and never relaunch

Most Windows failures are not distinct control bugs. They are one shape — a page is absent from the tree
while Shell reports its route as current — multiplied by a harness gap that let one dead page fail every
later test on it.

`UITest.CollectEvidence` requested a relaunch only when the failure message contained
`MauiDevFlowDriver.NavigationTimeoutMessage` ("Shell did not reach"). But `NavigateTo` has a second
failure mode: the route lands, then `WaitUntilTypeExists(pageType)` times out. That produced
`Element (automationId '', type 'XPage', …) not found within 30s`, which matched nothing, so no relaunch
was ever requested and the page stayed dead for the rest of the session.

**Fixed in `tests/` (platform-neutral, no asserts weakened, not yet verified):**
`NavigateTo` now wraps that case in a new `MauiDevFlowDriver.PageMissingMessage`, and `CollectEvidence`
relaunches on either marker.

This is expected to collapse a large share of the failures. Everything below marked *cascade* is
unverified until the suite is re-run with the fix in place.

### Two more Windows-only harness faults — relaunch could never succeed

The first run died outright around `QuickEntryTests`: Shell wedged on an empty route, the relaunch was
attempted, and from then on **every** test failed in its `BeforeTest` hook. Two independent causes, both
invisible on macOS and Linux, both fixed in `ace7b73`:

- **`app.log` was locked against the relaunch.** `ProcessRunner.StartLogged` opened the log with
  `new StreamWriter(logFile, append: false)`, which shares the file for reading only, and disposed the
  writer from the `Exited` handler — a pool-thread callback that races the next `LaunchAsync`. On Windows
  the relaunch hit `The process cannot access the file … because it is being used by another process`
  and the app never came back. Now opened `FileShare.ReadWrite | FileShare.Delete`, with a guard for
  output arriving after disposal.
- **The real failure was masked.** `UITest.TestStarted` stayed `default` when `PrepareApp` threw, and
  `DateTimeOffset.MinValue.AddSeconds(-1)` overflows, so the After(Test) hook reported
  `The added or subtracted value results in an un-representable DateTime` instead of the actual error.
  Now seeded at construction.

Because of this the first run's results past `QuickEntryTests` are worthless — it reached
312 started / 264 passed / 55 failed before being stopped deliberately. A clean re-run is in progress and
is the one that will be reported.

## Failures and verdicts

Verdicts marked **provisional** need an isolated re-run, which cannot start while the suite holds the app
(`DesktopAppHost.LaunchAsync` kills every process named `Sample`).

### ButtonsTests — 5 of 7

| Test | Failure | Verdict |
|---|---|---|
| `Faulted_Command_Shows_The_Error_State` | `BusyKeepContent.State` was `Error`, expected `Busy` within 5s | **provisional — real failure, cause unknown.** `SaveAsync` delays 1800ms before throwing, so `Busy` should be observable for ~1.8s, and the failure screenshot shows the page healthy with "Saving…". Not obviously the read-race the sibling tests were hardened against. |
| `Command_Reported_Success_Holds_Then_Reverts` | `ShouldFailCheckBox` not found within 10s | **cascade** |
| `Bound_State_Shows_Success_And_Error` | type `ButtonsPage` not found within 30s | **cascade** |
| `CanExecute_Disables_The_Button_But_Never_Re_Enables_An_Explicit_Disable` | same | **cascade** |
| `Tap_Runs_The_Command_With_Its_Parameter` | same | **cascade** |

`ButtonsPage` vanished from the tree after the first failure and never came back, while Shell kept
reporting route `buttons`. The app never crashed — same pid throughout, no relaunch. Whether the faulted
command destroyed the page is the open question.

### Whole classes lost to missing pages — 10

| Class | Tests | Page |
|---|---|---|
| `KeyframeTests` | 5 | `KeyframePage` |
| `MarqueeTests` | 4 | `MarqueePage` |
| `MotionIconsTests` | 1+ | `MotionIconsPage` |

All failed with "type `…Page` not found within 30s", including the **first** test of each class — so unlike
ButtonsPage these may never have come up at all. Two candidates, not yet separated:

1. the page genuinely cannot build on Windows → belongs in `KnownGaps.cs`;
2. an earlier page wedged Shell → pure cascade.

Against (2): `MermaidTests` ran normally right after, so Shell was not globally wedged. All three are
animation-driven controls, which is suggestive but not evidence. **Provisional.**

### DataGridTests — 6 of 6, one coherent shape

Every failure is a **collapse that does not happen**; expanding works.

**Lead (not yet verified): this may be `IsVisible`, not DataGrid.**
`Header_Tap_Sorts_Ascending_Then_Descending` fails at `DataGridTests.cs:128` —
`Assert.That(await HasText("Liskov")).IsFalse()` — which runs **before the test taps anything**. Liskov is
on page two of an unsorted grid and should not be on screen, yet `HasText` finds it. `HasText` filters
`QuerySelector` results on `e.IsVisible`, so if WinUI (or the DevFlow agent on WinUI) reports elements as
visible when they are not, every "did not collapse" symptom here is explained without the grid being
wrong at all: the rows did collapse, and the query still saw them.

That would also make `IsVisible` unreliable for *any* test on this head, which matters well beyond
DataGrid. Against it: `Tree_Lazy_Branch_Loads_Its_Children` fails the other way ("no visible element with
text `▾`"), so the story is not purely "everything reports visible".

To check directly against the running app: query a known off-page row and read `IsVisible`.

| Test | Symptom |
|---|---|
| `Grouping_Header_Tap_Collapses_And_Expands` | "East to collapse again" |
| `Detail_Caret_Loads_And_Shows_The_Breakdown` | "the detail row to collapse" |
| `Tree_Caret_Expands_And_Collapses_A_Branch` | "the branch to collapse" |
| `Grouping_Buttons_Regroup_The_Bound_Grid` | "a single grouping level" |
| `Header_Tap_Sorts_Ascending_Then_Descending` | "Liskov" still visible after sorting |
| `Tree_Lazy_Branch_Loads_Its_Children` | no visible element with text `▾` |

**Provisional — reads as one WinUI product bug, not six test bugs.** Rows stay present/visible after a
collapse. Needs confirmation before any `[SkipOn(UIPlatform.Windows, …)]` goes in.

### `This operation is not supported for a relative URI` — 3

`ColorPickerTests.Popup_Edits_Flow_Back_To_The_Binding`, `DockingTests.Lock_CheckBox_Locks_The_Host`,
`DocumentEditorTests.Margins_Button_Cycles_The_Presets` all fail through `AssertNoAppErrors()` on an
app-logged `System.InvalidOperationException: This operation is not supported for a relative URI`.

The interactions themselves may well succeed — the assert is on the log. A relative URI being rejected is
a classic WinUI-only difference. **Provisional**; needs the source of the URI identified before deciding
between a `src/` bug, a sample bug, and a Windows gap. Note `Lock_CheckBox_Locks_The_Host` is the test
whose Android skip was just removed after the `DockGroupView` fix — on Windows it fails for an unrelated
reason.

### Other

- `MermaidTests.Editor_Text_Drives_The_Diagram` — `EditorDiagram.DiagramText` mismatch. Untriaged.

## Harness notes for Windows

- **`app.log` is 118 bytes**, containing only the two DevFlow agent startup lines. The unpackaged WinUI head
  writes nothing to stdout, so `DesktopAppHost.TakeOutputErrors()` — the path that catches
  "Failed to create page view" and drives `RequestRelaunch` on AppKit — is **blind on Windows**. This is
  likely why nothing noticed the dead pages. Worth solving properly if Windows is to stay in the matrix.
- Only one failure screenshot was produced across 25 failures; capture appears to fail for the
  page-missing case. Not yet investigated.

## Changes on this branch

- `tests/Sample.UITests/MauiDevFlowDriver.cs` — `NavigateTo` wraps a missing page in `PageMissingMessage`.
- `tests/Sample.UITests/Infrastructure/UITest.cs` — `CollectEvidence` relaunches on that too.
- `tests/WINDOWS-STATUS.md` — this file.

## Open questions for the Mac session

1. Want the `uitest.ps1` PowerShell 5.1 fix applied here, or will you take it? It touches a file you are
   actively using.
2. For the DataGrid collapse cluster — if it confirms as a real WinUI bug, do you want six
   `[SkipOn(UIPlatform.Windows, "BUG: …")]` marks, or one and a note on the rest?
3. Is a Windows `SHINY_UITEST_WATCHDOG_SECONDS` equivalent wanted, given stdout is unavailable here?
