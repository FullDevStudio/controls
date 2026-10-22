# Expander & Accordion

[← All Shiny Controls](../../README.md)

A header you tap and content that animates in beneath — or above — it, on both hosts. **`Accordion`**
stacks them and decides how many may be open at once.

**Motion.** `Animation` is a flags enum, so the three effects combine: `Fade`, `Slide` and `Height`.
`Height` is the one that makes an accordion read as an accordion — the panel grows and shrinks between
zero and the content's size, so everything below it moves with the reveal instead of being uncovered by
it. `SlideFrom` aims the slide at `Top`, `Bottom`, `Left` or `Right`, and `AnimationDuration` /
`AnimationEasing` set its pace. `ExpandDirection="Up"` puts the content above the header, which is what
a panel pinned to the bottom of a page wants.

On MAUI `Height` measures the content and animates a clipped panel; where there is nothing to measure
yet — the very first reveal, before layout has run — it stands down and lets fade and slide carry the
transition on their own. On Blazor the whole reveal is CSS: the panel is a grid transitioning
`grid-template-rows` between `0fr` and `1fr`, so there is no measuring, no JS interop, and content that
changes size while open still lays out normally. Both hosts honour `prefers-reduced-motion` / a zero
duration.

**Chrome.** `BorderColor`, `BorderThickness`, `CornerRadius`, `HasShadow`, `HeaderBackgroundColor`,
`ContentBackgroundColor`, `HeaderPadding`, `ContentPadding`, `ShowSeparator` and `SeparatorColor` are all
yours; leave one alone and it follows the active theme. The indicator is a glyph that either rotates
(`IndicatorMode="Rotate"`, the default) or swaps between `CollapsedIcon` and `ExpandedIcon`
(`"Swap"`), sits at either end (`IndicatorPosition`), or is replaced outright by a view of your own.
`Header` / `HeaderTemplate` take over the whole header when the built-in `HeaderText` + `HeaderDetail`
pair is not enough.

**State.** `IsExpanded` is two-way. `Expanding` and `Collapsing` are cancelable — set `Cancel` to keep a
section shut until a form validates — and `Expanded`, `Collapsed` and `ExpandedChanged` report what
happened. `LoadContentOnDemand` holds `ContentTemplate` (MAUI) / `ChildContent` (Blazor) back until the
first open, so a list of twenty expanders over twenty forms builds one form rather than twenty.

```xml
<shiny:Expander HeaderText="Shipping" HeaderDetail="Arrives Tuesday"
                Animation="Height,Slide,Fade" SlideFrom="Top"
                BorderColor="#7C3AED" CornerRadius="18">
    <Label Text="123 Fake Street" />
</shiny:Expander>
```

```razor
<Expander HeaderText="Shipping" HeaderDetail="Arrives Tuesday"
          Animation="ExpanderAnimation.Height | ExpanderAnimation.Slide | ExpanderAnimation.Fade">
    <p>123 Fake Street</p>
</Expander>
```

**Accordion.** `SelectionMode` is `Single` (opening one closes the rest) or `Multiple`.
`AllowCollapseAll="False"` refuses to end up with nothing open: the last open item stops responding to
taps and a list that starts closed opens its first item. `ExpandedIndex` is two-way, `ExpandedIndexes`
reports every open item, and `ExpandAll()` / `CollapseAll()` / `ExpandItem(index)` drive it from code.

Items can be written out one by one, generated from data, or both — the generated ones are appended after
whatever was declared in markup. On MAUI bind `ItemsSource` with a `HeaderTemplate` and `ContentTemplate`
(or an `ItemTemplate` that returns a whole `Expander`); on Blazor a plain `@foreach` of `<Expander>` inside
the accordion is usually nicer, since the models stay strongly typed, with an `Items` parameter there for
when the shape is only known at runtime.

On MAUI the generated expanders are kept in line with `ItemsSource` by data item: when the source changes
(any `CollectionChanged`, Reset included) or a new `ItemsSource` is assigned, an item that is still there keeps
its expander, with its open state and its content, and only new items get a new one. Items are matched by
`Equals`, duplicates in order, and `ExpandedIndex` follows the open item to its new place. Changing
`ItemTemplate`, `HeaderTemplate` or `ContentTemplate` rebuilds them all.

The accordion's motion and chrome properties are **defaults**: they reach every item that did not set the
same property itself, so one odd expander in the list stays odd. `ItemStyle` (MAUI) takes a
`TargetType="shiny:Expander"` style for anything the shortcuts do not cover.

```xml
<shiny:Accordion SelectionMode="Single" AllowCollapseAll="False"
                 ExpandedIndex="{Binding ExpandedIndex}"
                 Animation="Height,Fade" CornerRadius="14">
    <shiny:Expander HeaderText="Account">…</shiny:Expander>
    <shiny:Expander HeaderText="Billing">…</shiny:Expander>
</shiny:Accordion>
```

```razor
<Accordion SelectionMode="AccordionSelectionMode.Single" @bind-ExpandedIndex="index">
    <Expander HeaderText="Account">…</Expander>
    <Expander HeaderText="Billing">…</Expander>
</Accordion>
```

### Automation ids (MAUI)

Set `AutomationId` on an `Expander` and its header row, which carries the tap, is named after it:
`<id>_Header` (`Expander.HeaderAutomationIdSuffix`). Drive the header id in a UI test; the expander's own
id finds nothing to tap. The header follows the expander's id when it changes, so a bound id in a
`BindableLayout` / `ItemsSource` template is fine across rebuilds.
