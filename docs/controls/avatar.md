# Avatar & AvatarGroup

[← All Shiny Controls](../../README.md)

`Avatar` shows a person (or a thing) in a small frame: their **picture** when there is one, their **initials** when there is not, and a generic **silhouette** when there is not even a name. An optional **presence dot** sits on the bottom-right corner. `AvatarGroup` lays avatars out as an overlapping "facepile" and collapses everyone past `Max` into a single **+N** chip. Both are in the core packages on both hosts.

<!-- TODO: capture screenshots for avatar -->

```xml
<shiny:Avatar Name="Grace Hopper"
              Uri="{Binding PhotoUrl}"
              Size="48"
              Status="Online" />

<shiny:AvatarGroup Max="4" TotalCount="{Binding MemberCount}"
                   OverflowCommand="{Binding ShowMembersCommand}">
    <shiny:Avatar Name="Grace Hopper" Uri="https://…/grace.jpg" />
    <shiny:Avatar Name="Ada Lovelace" ColorFromName="True" />
    <shiny:Avatar Name="Alan Turing" ColorFromName="True" />
    <shiny:Avatar Name="Linus Torvalds" />
    <shiny:Avatar Name="Margaret Hamilton" />
</shiny:AvatarGroup>
```

```razor
<Avatar Name="Grace Hopper" Uri="@photoUrl" Size="48" Status="AvatarStatus.Online" />

<AvatarGroup Max="4" TotalCount="@memberCount" OnOverflowClick="ShowMembers">
    @foreach (var member in members)
    {
        <Avatar @key="member.Id" Name="@member.Name" Uri="@member.PhotoUrl" />
    }
</AvatarGroup>
```

## How it decides what to draw

1. **Picture** — `Uri` (both hosts) or `Source` (MAUI `ImageSource`). The initials are drawn *underneath* it, so they show while it loads and stay put if it fails: a broken URL degrades to initials, never to a hole or a broken-image glyph. On MAUI the picture loads through `ShinyImage`, so it is cached, queued and de-duplicated like every other image in the app.
2. **Initials** — `Initials` if set, otherwise derived from `Name`: the first letter of the first and last words (`"Grace Brewster Hopper"` → `GH`, `"Jean-Luc Picard"` → `JP`, `"ada.lovelace@example.com"` → `AL`).
3. **Silhouette** — when there is neither.

## Avatar

| Property | Type (MAUI / Blazor) | Default | Description |
|---|---|---|---|
| Name | string | null | Drives the initials, the accessible label and `ColorFromName` |
| Initials | string | null | Overrides the derived initials |
| Uri | string | null | Picture URL. On MAUI also `resource://`, file and `data:` URIs (via `ShinyImage`) |
| Source | ImageSource | null | **MAUI only** — any `ImageSource`; used when `Uri` is empty |
| Size | double | 40 / -1 (→ 40px) | Width and height |
| Shape | AvatarShape / AvatarShape? | Circle / null (→ circle) | `Circle`, `Rounded` (quarter-size corners), `Square` |
| Status | AvatarStatus | None | Presence dot: `None`, `Online`, `Away`, `Busy`, `Offline` |
| StatusColor | Color / string | theme | Overrides the dot colour. Unset follows success / warning / error / outline tokens |
| FillColor | Color / string | theme primary container | Background behind initials / silhouette |
| TextColor | Color / string | theme on-primary-container | Initials and silhouette colour |
| ColorFromName | bool | false | A stable hue per name (white initials) when `FillColor` is unset — the **same hue on MAUI and Blazor** |
| RingColor | Color / string | theme surface | Ring colour |
| RingThickness | double | 0 / -1 (none) | Ring thickness |
| FontSize | double | -1 (auto) | Initials size; auto is 40% of `Size` (32% for three or more characters) |
| Command / CommandParameter | ICommand / object | null | **MAUI** — tapped. Setting it makes the avatar tappable |
| Clicked | event | — | **MAUI** — tapped. Subscribing makes the avatar tappable |
| OnClick | EventCallback&lt;MouseEventArgs&gt; | — | **Blazor** — handling it renders the avatar as a `<button>` |
| DisplayInitials | string (read-only) | — | The initials actually shown |
| IsShowingImage | bool (read-only) | — | **MAUI** — the picture has loaded and covers the initials |

An avatar nobody listens to takes no taps at all, so a list of rows with avatars keeps its own selection.

## AvatarGroup

| Property | Type (MAUI / Blazor) | Default | Description |
|---|---|---|---|
| Avatars / ChildContent | IList&lt;Avatar&gt; / RenderFragment | — | The avatars, declared inline |
| ItemsSource / ItemTemplate | IEnumerable / DataTemplate | null | **MAUI** — bind instead of declaring; the template root must be an `Avatar`. Without a template each string item becomes an avatar of that name. Observable collections are tracked |
| Max | int | 0 (all) | Most avatars drawn before the rest collapse into the chip (the chip is not counted) |
| TotalCount | int | 0 | The real head count when the avatars are only a sample — `+N` counts against this |
| Size | double | -1 | When positive, the size of every avatar in the group |
| Shape | AvatarShape? | null | When set, the shape of every avatar in the group |
| Overlap | double | -1 (25% of size) | How far each avatar tucks under the previous one; `0` lays them side by side |
| RingColor | Color / string | theme surface | Separator ring — set it to whatever the group sits on |
| RingThickness | double | 2 | Separator ring thickness |
| OverflowFormat | string | `"+{0}"` | Chip text; `{0}` is the hidden count |
| OverflowCommand / OverflowClicked | ICommand / event | — | **MAUI** — chip tapped; the command parameter is the hidden count |
| OnOverflowClick | EventCallback&lt;int&gt; | — | **Blazor** — chip clicked with the hidden count; makes the chip a `<button>` |
| OverflowCount | int (read-only) | — | How many people the chip stands for |
| VisibleAvatars / VisibleCount | — | — | MAUI list / Blazor count of avatars drawn |

Later avatars sit on top of earlier ones. The `+N` chip takes the size and shape of the first avatar, so it always matches the row.

### Host notes

- **Blazor** — size, shape and ring are CSS custom properties (`--shiny-avatar-size`, `--shiny-avatar-radius`, `--shiny-avatar-ring-width`, `--shiny-avatar-ring-color`, plus `--shiny-avatar-fill` / `--shiny-avatar-ink` / `--shiny-avatar-overlap`). The group sets them once on its root; a parameter on an individual avatar still wins. Avatars must be **direct children** of the group: one wrapped in another element (a tooltip, a link) is counted but cannot be hidden past `Max`, because hiding is done by position.
- **Blazor** — the picture is a plain `<img loading="lazy">`, so caching is the browser's.
- **MAUI** — the group *writes* `Size`, `Shape`, `RingThickness`, `RingColor` and `Margin` onto the avatars it holds; set them on the group, not on the children.
- **Accessibility** — each avatar announces its name (and status, e.g. "Grace Hopper, Online"); the group announces "4 people and 12 more".
