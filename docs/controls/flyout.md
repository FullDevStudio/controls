# Flyout (MAUI)

[← All Shiny Controls](../../README.md)

A side panel that slides in from either edge, can rest as a narrow **icon rail** instead of a full panel, and either **pushes** the content aside or **floats** over it with a scrim. It replaces MAUI's `FlyoutPage` for apps that want more than a drawer, and — unlike `FlyoutPage` — it works **inside Shell**.

> **Blazor equivalent — `AppLayoutPanel` inside `AppLayout`.** Same three states and the same auto-compacting; the Blazor panel additionally persists its state and width to localStorage.

Three states (`Hidden` / `Collapsed` — the rail — / `Expanded`), two RTL-aware sides (`Start` / `End`), and three presentations (`Overlay` / `Push` / `Auto`, which pushes at or above `CompactWidth`, default 800). **`Presentation` governs an *expanded* panel**: a collapsed rail always insets the content on both presentations, because a rail is chrome rather than a drawer — which is what lets a rail expand *over* the content without the content moving at all.

```xml
<shiny:ShinyFlyoutPage xmlns:shiny="http://shiny.net/maui/controls" Title="Workspace">
    <shiny:ShinyFlyoutPage.Start>
        <shiny:FlyoutPanel x:Name="Nav"
                           State="Collapsed"
                           Presentation="Auto"
                           CompactWidth="700"
                           ExpandedWidth="260"
                           CollapsedWidth="64"
                           IsResizable="True">
            <shiny:FlyoutPanel.HeaderContent>
                <Label Text="Explorer" FontAttributes="Bold" Padding="16,14" />
            </shiny:FlyoutPanel.HeaderContent>

            <shiny:FlyoutPanel.RailContent>
                <VerticalStackLayout HorizontalOptions="Center" Padding="0,12" Spacing="6">
                    <Button Text="&#x1F5C2;" Clicked="OnToggle" />
                    <Button Text="&#x1F50D;" Clicked="OnToggle" />
                </VerticalStackLayout>
            </shiny:FlyoutPanel.RailContent>

            <VerticalStackLayout Padding="8">
                <Label Text="Files" Padding="12,10" />
                <Label Text="Search" Padding="12,10" />
            </VerticalStackLayout>
        </shiny:FlyoutPanel>
    </shiny:ShinyFlyoutPage.Start>

    <ScrollView>…detail…</ScrollView>
</shiny:ShinyFlyoutPage>
```

`ShinyFlyoutPage.Detail` is a **`View`**, not a `Page` — MAUI cannot parent a `Page` inside a page's content, so it cannot host a `NavigationPage` the way `FlyoutPage.Detail` can. For a flyout on every page, declare it once as a template on the Shell (or a `NavigationPage`) instead:

```xml
<Shell FlyoutBehavior="Disabled" xmlns:shiny="http://shiny.net/maui/controls">
    <shiny:ShinyFlyout.StartTemplate>
        <DataTemplate>
            <shiny:FlyoutPanel State="Hidden" CollapsedState="Hidden"
                               Presentation="Overlay" ExpandedWidth="280">…nav…</shiny:FlyoutPanel>
        </DataTemplate>
    </shiny:ShinyFlyout.StartTemplate>
    …
</Shell>
```

Each page builds its own panel from the template — sharing one instance would re-parent it on every navigation and throw away its scroll position — and the open/collapsed **state** carries across. The `FlyoutView` options are attached properties on the host too — `ShinyFlyout.PushMode`, `ScrimColor`, `ScrimOpacity` and `IsAnimationEnabled` — copied onto the view installed on every page, and a later change reaches the pages already showing, so you never fish the view out with `ShinyFlyout.GetFlyoutView(page)` after each navigation. Panel options (`IsSwipeEnabled`, `Presentation`, widths…) go on the `FlyoutPanel` in the template. Drive it from a view model through `IFlyoutService` (`ToggleAsync`, `SetStateAsync`, `GetState`, `StateChanged`), which resolves the flyout on the page currently showing. `FlyoutView` is the underlying layout if you want a panel on one screen only.

**Pushing shifts rather than crushes.** `PushMode` defaults to `Shift`: the content keeps its full width and is simply translated aside, so its far edge slides out of view and is clipped. Nothing inside it re-lays out — text does not rewrap and columns do not collapse as the panel moves, which is both the drawer feel most apps want and a great deal less work per frame than re-measuring the whole content tree. `PushMode="Resize"` restores the narrow-and-reflow behaviour, which is what a responsive master/detail beside a permanent sidebar actually wants. Note the mode governs *every* displacement including the rail's, so an app whose rail is permanent chrome should use `Resize`.

`CollapseBelow` drops an expanded panel to its `CollapsedState` when the flyout gets narrow and restores it when there is room again — unless the user has changed the state themselves in the meantime, which always wins. Panels take `MinExpandedWidth`/`MaxExpandedWidth` with a drag-to-resize handle (`IsResizable`), edge-swipe-to-open, tap- or drag-the-scrim-to-close, theme-following colours, and `HeaderContent`/`FooterContent` that stay pinned while the body scrolls.
