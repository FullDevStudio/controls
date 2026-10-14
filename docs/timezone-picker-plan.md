# TimeZonePicker

A searchable time zone picker whose zone names are localized into any language, without shipping
translation resource files.

**Status:** design only, nothing implemented. The open decisions are listed under
[Open decisions](#open-decisions).
**Scope:** MAUI ↔ Blazor parity. It goes in the core packages because there are no third-party
dependencies.

## Requirements

1. Pick a time zone. The value is an **IANA ID** (`"Europe/Paris"`) and never a Windows ID or a
   display string.
2. Zone names are shown in a requested culture, not only the OS language, wherever the platform
   allows it.
3. **No major resource files.** The control ships no CLDR or translation tables. Names come from the
   data the platform already has.
4. Searching matches the localized name, the city, and the IANA ID.
5. Each row shows the current UTC offset, computed for "now" so that DST is correct.

## How to get the names without resource files

All the target platforms already include CLDR time zone names through ICU or their own copy of that
data. The control asks the platform and ships nothing.

| Host / runtime | Name source | Any culture? | Localized city? |
|---|---|---|---|
| Blazor (WASM / Server / Hybrid) | Browser `Intl.DateTimeFormat(locale, { timeZone, timeZoneName: 'longGeneric' })`. The zone list comes from `Intl.supportedValuesOf('timeZone')` | ✅ | ❌ (see gaps) |
| MAUI iOS (`-ios`) | `NSDateFormatter` + `NSLocale`, pattern `vvvv` (generic name, "Pacific Time") and `VVVV` (generic location, "Heure : Los Angeles") | ✅ | ✅ |
| MAUI Android (`-android`) | `android.icu.text.TimeZoneNames.GetInstance(ULocale)` → `GetDisplayName(id, LongGeneric, now)` + `GetExemplarLocationName(id)` (API 24+) | ✅ | ✅ |
| MAUI Windows (`-windows`) | `TimeZoneInfo.FindSystemTimeZoneById(iana).DisplayName` | ⚠️ Windows display language only | ❌ |
| MAUI `net10.0` (Mac Catalyst, AppKit macOS, GTK4 Linux) | `TimeZoneInfo` (ICU-backed) `DisplayName` / `StandardName` | ⚠️ process UI culture only | ❌ |

Notes:

- The core MAUI package targets `net10.0;-ios;-android(;-windows)`. Mac Catalyst and AppKit get the
  plain `net10.0` build (see the memory notes on drag/drop and AppKit), so they **cannot** call the
  `NSDateFormatter` path unless that TFM is added. They fall back to `TimeZoneInfo`.
- On ICU runtimes, .NET reads `TimeZoneInfo` display names from `CultureInfo.CurrentUICulture` the
  **first time they are accessed** and then caches them. Changing the culture at runtime does not
  re-localize them. Document this and do not try to work around it.
- The offset, DST flag and IANA ↔ `TimeZoneInfo` mapping always come from `TimeZoneInfo`
  (`FindSystemTimeZoneById` accepts IANA IDs on every platform since .NET 6). Only the **display
  text** goes through a platform name provider.
- The zone list is `TimeZoneInfo.GetSystemTimeZones()` converted to IANA (`TryConvertWindowsIdToIanaId`
  on Windows) in .NET, and `Intl.supportedValuesOf('timeZone')` on Blazor. **Check on Blazor WASM**:
  the trimmed `dotnet.timezones.blat` may lack zones the browser has, or the reverse. Because the
  browser is authoritative for names, take the list from the browser and compute offsets in JS
  (`timeZoneName: 'longOffset'`) when a zone is missing from .NET.

### Known gaps (accepted for v1)

- **Blazor city names stay in English.** `Intl` has no exemplar-city API. The city comes from the last
  segment of the IANA ID (`America/Indiana/Indianapolis` → "Indianapolis", underscores → spaces).
- **Windows and `net10.0` heads** follow the OS/process language, not the `Culture` property. Possible
  later fix: P/Invoke the `icu.dll` that ships with Windows 10 1903+ (`ucal_getTimeZoneDisplayName`,
  `udat_*` with `VVVV`) and the ICU that .NET loads on Linux. This is left out of v1.
- Search finds "Tokyo" in a Japanese UI only through the IANA ID, because there are no English aliases.

## Architecture

The engine is split from the name providers so that each host only adds a provider.

| Piece | Lives in | Role |
|---|---|---|
| `TimeZoneItem` (record) | shared | `Id` (IANA), `DisplayName`, `City`, `UtcOffset`, `IsDaylightSaving`, `OffsetText` ("UTC+01:00") |
| `ITimeZoneNameProvider` | shared | `string? GetGenericName(string ianaId, CultureInfo c)`, `string? GetCity(string ianaId, CultureInfo c)`. Returning `null` falls back to the default provider |
| `TimeZoneNameProvider` (default) | shared | The `TimeZoneInfo` names plus a city taken from the IANA segment |
| `TimeZoneCatalog` | shared | Builds the list (IANA IDs, de-duplicated, `Etc/*` and legacy aliases filtered), computes offsets for a given instant, sorts by offset then name or alphabetically, searches (case-insensitive and **diacritic-insensitive** through `CompareInfo.IndexOf` with `IgnoreNonSpace`), and caches per culture |
| iOS / Android providers | `Shiny.Maui.Controls/Platforms/*` | The native calls above |
| Browser provider | `Shiny.Blazor.Controls` + a small JS module | A single interop call returns every name for one locale as **named DTOs or primitive arrays** (no anonymous types, and arrays of DTOs get trimmed; see the memory notes). Results are cached per locale |

Shared placement: see [Open decisions](#open-decisions) (a new `Shiny.Controls.TimeZones.Shared`
package, or duplicated into each core package the way `CountryData.cs` is today).

## The control

It follows `CountryPicker` (`src/Shiny.Maui.Controls/CountryPicker.cs`,
`src/Shiny.Blazor.Controls/CountryPicker.razor`), which sits on `AutoCompleteEntry`.

Row: **`(UTC+01:00) Central European Time`**, with a secondary line **`Paris · Europe/Paris`**.

| Property | Type | Default | Notes |
|---|---|---|---|
| SelectedTimeZone | `TimeZoneItem?` | null | Two-way |
| SelectedTimeZoneId | `string?` | null | Two-way convenience for binding the IANA ID straight to settings |
| Culture | `CultureInfo?` | null → `CurrentUICulture` | Changing it rebuilds names (cached per culture) |
| DefaultToLocal | bool | false | Pre-selects `TimeZoneInfo.Local` (or the browser's `resolvedOptions().timeZone` on Blazor; on Server that's the **client's** zone, not the server's) |
| SortMode | `TimeZoneSortMode` | ByOffset | `ByOffset` / `Alphabetical` |
| ShowIanaId | bool | true | Secondary line |
| ReferenceTime | `DateTimeOffset?` | null → now | The instant offsets/DST are computed for, e.g. a meeting date |
| Placeholder | string | "Search time zones..." | Default text is English, like CountryPicker; the app localizes it |
| Styling | — | — | The same set as CountryPicker: TextColor, PlaceholderColor, DropDown*, FontSize, FontFamily/CornerRadius (MAUI), InputClass/DropDownClass (Blazor) |

Event: `TimeZoneSelected`.

Blazor service lifetime: anything holding the cache is **Scoped**, or the cache is keyed by locale
and immutable, so one user's culture never leaks to another on Blazor Server.

## Tests (`tests/`)

- Catalog: alias de-duplication, sort order, the search rules (diacritics, IANA ID, city), and the
  ReferenceTime/DST offsets (e.g. `America/New_York` in January vs July).
- The provider fallback chain (a null from a platform provider → default).
- Blazor: bUnit with a fake JS module, plus **one Release WASM publish** to check that interop
  survives trimming.
- Device/browser pass: `fr-FR`, `ja-JP` and `ar` (RTL) on iOS, Android and Chrome; confirm the native
  names really change.

## Deliverables checklist (from CLAUDE.md)

- [ ] Controls on both hosts + sample pages under `samples/Sample/Features/<Area>/` (with a culture switcher)
- [ ] `docs/controls/timezone-picker.md` + a row in the README index
- [ ] `SKILLS/shiny-controls/timezone-picker.md` + a reference in `SKILL.md`
- [ ] Docs site: `src/content/docs/controls/timezone-picker/`, the release notes, a sidebar node in `src/sidebar-topics.mjs`, and an "Input" entry in the homepage "UI Controls" card
- [ ] If a new shared package is chosen: slnx, `Build.slnf`, the README badge + summary, both issue-template dropdowns, and the CLAUDE.md repo layout
- [ ] TODO: capture screenshots for timezone-picker (not part of the build)

## Open decisions

1. **Rows:** one per IANA zone (~420, with a city subtitle to tell repeats apart) **(recommended)**, or
   collapsed to one per localized generic name (~150, "Pacific Time" appears once)?
2. **Windows/Linux any-culture:** accept the OS/process language for v1 **(recommended)**, or do the
   `icu.dll` P/Invoke work now?
3. **Shape:** autocomplete like CountryPicker **(recommended)**, or a button that opens a list/sheet?
4. **Shared engine placement:** a new `Shiny.Controls.TimeZones.Shared` package (one tested engine,
   but the full new-package checklist), or a copy in each core package like `CountryData.cs`?
5. **Mac Catalyst / AppKit:** fall back to `TimeZoneInfo` names, or add Apple TFMs or a runtime
   `NSDateFormatter` path so those heads get any-culture names too?
