# ShinyToolbar & ShinyTabBar (Blazor)

[← All Shiny Controls](../../README.md)

Two screen-docked navigation chromes for Blazor. **`ShinyToolbar`** docks to the top or bottom of its
scroll container as an action bar (icons with links/actions, title, custom slots). **`ShinyTabBar`** is a
mobile-style tab bar pinned to the bottom of the viewport with a selected state and badges. Both support a
**frosted-glass** toggle (`Frosted`) backed by `backdrop-filter`.

Toolbar items that don't fit the bar collapse into an **overflow dropdown** automatically, and any item can
be a **menu button** of its own — give it `Children` and it opens a dropdown instead of raising a click,
nested as deep as you like with `IsSeparator` dividers between groups. Every panel is drawn in the browser's
**top layer** (the popover API), so a bar living inside a panel, a card or a scroller is never clipped by
that ancestor's `overflow`.

When the bar is too narrow, the leading region (`Title` / `StartContent`) gives up space first: a long title
ends in an ellipsis rather than pushing `EndContent` and the trailing actions off-screen on a phone.

The top toolbar uses `position: sticky`, so it reserves its own height (content never starts *underneath*
it) yet page content scrolls *under* it as you scroll — the classic translucent-header effect. The tab bar
uses `position: fixed` so it stays pinned regardless of scroll.

```razor
@using Shiny.Blazor.Controls

<!-- Frosted top toolbar: content scrolls under it -->
<ShinyToolbar Dock="ToolbarDock.Top"
              Frosted="true"
              Title="Inbox"
              Items="@toolbarItems"
              ItemClicked="OnItemClicked" />

<!-- Bottom tab bar with two-way selection and a badge -->
<ShinyTabBar Items="@tabs"
             @bind-SelectedKey="selectedKey"
             ActiveColor="#7C3AED"
             Frosted="true" />

@code {
    string? selectedKey = "home";

    List<ToolbarItem> toolbarItems = new()
    {
        new() { Icon = "<svg>…search…</svg>", Text = "Search" },
        new() { Icon = "<svg>…bell…</svg>", Text = "Alerts", Badge = "3" },
        new() { Icon = "compose.png", Text = "Compose", Href = "/compose" },

        // a menu button: opens a dropdown instead of raising ItemClicked
        new()
        {
            Icon = "<svg>…file…</svg>",
            Text = "File",
            Children = new()
            {
                new() { Text = "New" },
                new() { Text = "Export", Children = new()
                    {
                        new() { Text = "PDF" },
                        new() { Text = "Markdown" }
                    }
                },
                new() { IsSeparator = true },
                new() { Text = "Delete", IconColor = "#EF4444" }
            }
        }
    };

    List<TabBarItem> tabs = new()
    {
        new() { Key = "home",   Label = "Home",   Icon = "<svg>…</svg>", ActiveIcon = "<svg>…filled…</svg>" },
        new() { Key = "chat",   Label = "Chat",   Icon = "<svg>…</svg>", Badge = "5" },
        new() { Key = "me",     Label = "Profile",Icon = "<svg>…</svg>", Href = "/profile" }
    };

    void OnItemClicked(ToolbarItem item) { /* … */ }
}
```

> Icons accept inline SVG/HTML markup, an emoji/glyph, or an image URL (`.png`/`.svg`/`http…`/`/…`).

**ShinyToolbar** parameters:

| Property | Type | Default | Description |
|---|---|---|---|
| Dock | ToolbarDock | Top | Docks to the `Top` or `Bottom` edge |
| Sticky | bool | true | `position:sticky` (content scrolls under); set false for a normal in-flow bar |
| Title | string? | null | Convenience leading title text (used when `StartContent` is not set) |
| Items | `List<ToolbarItem>?` | null | Trailing action/link/menu items; these are the ones that collapse into the overflow dropdown |
| StartContent / ChildContent / EndContent | RenderFragment? | null | Custom leading / center / trailing content (`EndContent` is pinned beside `Items` and never collapses) |
| OverflowEnabled | bool | true | Collapse items that don't fit into a dropdown behind a "more" button |
| OverflowIcon / OverflowText / OverflowAriaLabel | string / string? / string | hamburger SVG / "More" / "More actions" | The overflow button's glyph, label and accessible name |
| DropdownIcon | string | chevron SVG | Caret drawn on items that open a dropdown (empty string removes it) |
| MenuBackgroundColor / MenuTextColor | string / string | #FFFFFF / #1F2937 | Dropdown panel colors |
| BackgroundColor | string | #FFFFFF | Solid fill (ignored when `Frosted`) |
| TextColor | string | #1F2937 | Foreground color |
| Height | double | 56 | Bar height (min-height) |
| IconSize | double | 22 | Item icon size |
| ShowItemLabels | bool | false | Show each item's `Text` under its icon |
| Frosted | bool | false | Frosted glass via `backdrop-filter` |
| BlurRadius | double | 20 | Blur amount when `Frosted` |
| TintColor | string | rgba(255,255,255,0.7) | Translucent fill when `Frosted` |
| HasShadow | bool | true | Edge shadow (direction follows `Dock`) |
| BorderColor / BorderThickness | string? / double | null / 0 | Hairline on the docked edge |
| SafeArea | bool | true | Adds `env(safe-area-inset-*)` padding on the docked edge |
| ZIndex | int | 100 | Stacking order |
| CssClass / Style | string? | null | Extra root class / inline style |

Events: `ItemClicked` — fires the `ToolbarItem` that was tapped (items with an `Href` also navigate).

**ToolbarItem** properties: `Icon`, `Text`, `Tooltip`, `Href`, `Target`, `Badge`, `IconColor`, `IsDisabled`,
`Children` (a dropdown, nestable into submenus), `IsSeparator` (a divider inside a dropdown), `Tag`.

**ShinyTabBar** parameters:

| Property | Type | Default | Description |
|---|---|---|---|
| Items | `List<TabBarItem>?` | null | The tabs |
| SelectedKey | string? | null | Two-way bindable active tab `Key` |
| Dock | ToolbarDock | Bottom | Docks to the `Bottom` (default) or `Top` edge |
| Fixed | bool | true | `position:fixed` (always pinned); set false to use `sticky` inside a container |
| BackgroundColor | string | #FFFFFF | Solid fill (ignored when `Frosted`) |
| ActiveColor | string | #2196F3 | Selected tab color |
| InactiveColor | string | #9CA3AF | Unselected tab color |
| ShowLabels | bool | true | Show each tab's `Label` under its icon |
| Height | double | 56 | Bar height (min-height) |
| IconSize | double | 24 | Tab icon size |
| Frosted / BlurRadius / TintColor | bool / double / string | false / 20 / rgba(255,255,255,0.7) | Frosted glass options |
| HasShadow / BorderColor / BorderThickness | bool / string? / double | true / null / 0 | Edge chrome |
| SafeArea | bool | true | Adds `env(safe-area-inset-bottom)` padding (home-indicator clearance) |
| ZIndex | int | 100 | Stacking order |
| CssClass / Style | string? | null | Extra root class / inline style |

Events: `SelectedKeyChanged` (two-way bind via `@bind-SelectedKey`), `ItemClicked` — fires the tapped `TabBarItem`.

**TabBarItem** properties: `Key`, `Icon`, `ActiveIcon` (optional filled variant shown when selected), `Label`, `Href` (selecting also navigates), `Badge` (empty string `""` renders a dot), `IsDisabled`, `Tag`.

**Placement tip**: `position:sticky` sticks relative to the nearest scroll container, and any ancestor with
`overflow: hidden` silently breaks it — use `overflow: clip` if you must clip. For app-wide chrome, place
`ShinyToolbar` as the first element of your page/layout scroll area and drop `ShinyTabBar` anywhere (it's
`Fixed`). The Blazor sample wires both into `MainLayout` — a frosted top header plus a bottom tab bar that
appears on narrow viewports.

## Dark mode

`BackgroundColor`, `TextColor`, `MenuBackgroundColor`, `MenuTextColor`, `TintColor` and (on
`ShinyTabBar`) `ActiveColor` / `InactiveColor` default to theme tokens, so a bar follows the app's
scheme with nothing set. They are emitted as inline styles, so a literal you pass wins permanently —
and if you pass a background, pass a matching `TextColor` too.

Item and menu-item hover is keyed off the bar's own `currentColor` rather than a fixed black wash, so
it stays visible on a dark bar and on a brand-coloured one. See
[Styling & theming](styling.md#dark-mode).
