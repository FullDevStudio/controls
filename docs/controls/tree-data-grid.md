# TreeDataGrid

[← All Shiny Controls](../../README.md)

The same grid in hierarchy mode: hand it a `ChildrenSelector` and rows nest, with the indent and expand
caret carried inline by the first column — every other grid feature (columns, sorting, filtering, frozen
columns, selection, editing) works exactly as it does on a flat grid. `TreeDataGrid` and `DataGrid` are
the same type; the name is there so the markup says what the grid is.

```razor
@* Blazor *@
<TreeDataGrid TItem="CostNode" Items="accounts"
              ChildrenSelector="n => n.Lazy ? null : n.Children"
              ChildrenLoader="LoadChildrenAsync"
              HasChildrenSelector="n => n.Lazy || n.Children.Count > 0"
              TreeIndentSize="18">
    <Columns>
        <PropertyColumn Property="x => x.Name" Title="Account" />
        <PropertyColumn Property="x => x.Budget" Format="C0" />
    </Columns>
</TreeDataGrid>
```

```xml
<!-- MAUI -->
<shiny:TreeDataGrid ItemsSource="{Binding Accounts}"
                    ChildrenSelector="{Binding ChildrenSelector}"
                    ChildrenLoader="{Binding ChildrenLoader}"
                    HasChildrenSelector="{Binding HasChildrenSelector}"
                    TreeIndentSize="18">
    <shiny:DataGridColumn Title="Account" PropertyName="Name" Width="2*" />
    <shiny:DataGridColumn Title="Budget" PropertyName="Budget" StringFormat="{}{0:C0}" Width="1.2*" />
</shiny:TreeDataGrid>
```

`ChildrenLoader` fetches a level the first time it is expanded (that row's caret becomes a spinner
meanwhile) and caches the result; **`ChildrenSelector` gets first refusal**, so the loader only runs for the items it returns
`null` for and one tree can mix in-memory branches with fetched ones. `HasChildrenSelector` lets leaves
render caret-free before anything has loaded, and `ChildrenLoadFailed` reports a failed fetch (the row
collapses again).

Sorting and filtering apply **per level**, so children stay under their parent, and a row is kept when a
*descendant* matches the filter — otherwise the match would be unreachable. Paging pages the roots, and
tree mode and `Groupable` are mutually exclusive (grouping wins).

### Automation ids (MAUI)

`TreeDataGrid` names its parts exactly as [DataGrid](datagrid.md#automation-ids-maui) does; the tree
caret on each row is `<id>_RowToggle_<row>`.
