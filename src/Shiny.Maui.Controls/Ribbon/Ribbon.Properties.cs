using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.Ribbons;

public partial class Ribbon
{
    static BindableProperty Redraw(string name, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type returnType, object? defaultValue = null)
        => BindableProperty.Create(
            name, returnType, typeof(Ribbon), defaultValue,
            // Guarded: MAUI applies an implicit Style from StyleableElement's own constructor, before
            // this class's constructor body has run a line, so an unguarded callback dereferences
            // children that do not exist yet. See StyleGuard.
            propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Ribbon), () => ((Ribbon)b).Rebuild())
        );


    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex),
        typeof(int),
        typeof(Ribbon),
        0,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(Ribbon), () => ((Ribbon)b).OnSelectedIndexChanged((int)o, (int)n))
    );

    public static readonly BindableProperty SelectedTabProperty = BindableProperty.Create(
        nameof(SelectedTab),
        typeof(RibbonTab),
        typeof(Ribbon),
        null,
        BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Ribbon), () => ((Ribbon)b).OnSelectedTabChanged((RibbonTab?)n))
    );

    public static readonly BindableProperty DisplayModeProperty = BindableProperty.Create(
        nameof(DisplayMode),
        typeof(RibbonDisplayMode),
        typeof(Ribbon),
        RibbonDisplayMode.Expanded,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(Ribbon), () => ((Ribbon)b).OnDisplayModeChanged((RibbonDisplayMode)o, (RibbonDisplayMode)n))
    );

    public static readonly BindableProperty AllowCollapseProperty = Redraw(nameof(AllowCollapse), typeof(bool), true);

    public static readonly BindableProperty ApplicationButtonTextProperty = Redraw(nameof(ApplicationButtonText), typeof(string));

    public static readonly BindableProperty ApplicationButtonCommandProperty = Redraw(nameof(ApplicationButtonCommand), typeof(ICommand));

    public static readonly BindableProperty ShowQuickAccessProperty = Redraw(nameof(ShowQuickAccess), typeof(bool), true);

    public static readonly BindableProperty ShowGroupTitlesProperty = Redraw(nameof(ShowGroupTitles), typeof(bool), true);

    public static readonly BindableProperty ShowTabStripProperty = Redraw(nameof(ShowTabStrip), typeof(bool), true);

    public static readonly BindableProperty SmallItemRowsProperty = Redraw(nameof(SmallItemRows), typeof(int), 3);

    public static readonly BindableProperty SmallItemRowHeightProperty = Redraw(nameof(SmallItemRowHeight), typeof(double), 0d);

    public static readonly BindableProperty AllowGroupCollapseProperty = Redraw(nameof(AllowGroupCollapse), typeof(bool), true);

    /// <summary>
    /// Width below which the bar runs itself in <see cref="RibbonDisplayMode.Simplified"/>, and at or
    /// above which it returns to <see cref="RibbonDisplayMode.Expanded"/>. Zero, the default, leaves
    /// <see cref="DisplayMode"/> entirely to the host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Group collapsing is the wrong answer on a phone. It folds groups into dropdowns worst-first,
    /// which is right when a window is a little too narrow - but at phone width there is room for no
    /// group at all, so every command ends up behind a dropdown and the bar is worse than the strip it
    /// replaced. Simplified is the mode meant for that: one dense row, every item small, group titles
    /// dropped.
    /// </para>
    /// <para>
    /// A collapse the user asked for is never overridden - the rule only chooses between Expanded and
    /// Simplified, and <c>ToggleCollapsed</c> restores whichever of the two was in force.
    /// </para>
    /// </remarks>
    public static readonly BindableProperty SimplifyBelowWidthProperty = BindableProperty.Create(
        nameof(SimplifyBelowWidth),
        typeof(double),
        typeof(Ribbon),
        0d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Ribbon), () => ((Ribbon)b).ApplyWidthRule()));

    public static readonly BindableProperty AccentColorProperty = Redraw(nameof(AccentColor), typeof(Color));

    public static readonly BindableProperty HeaderBackgroundColorProperty = Redraw(nameof(HeaderBackgroundColor), typeof(Color));

    public static readonly BindableProperty HeaderForegroundColorProperty = Redraw(nameof(HeaderForegroundColor), typeof(Color));

    public static readonly BindableProperty BodyBackgroundColorProperty = Redraw(nameof(BodyBackgroundColor), typeof(Color));

    public static readonly BindableProperty ShowTooltipsProperty = Redraw(nameof(ShowTooltips), typeof(bool), true);


    /// <summary>
    /// Index of the tab showing, counted over <see cref="Tabs"/> including hidden ones, so it does not
    /// shift when a contextual tab appears. Two-way.
    /// </summary>
    public int SelectedIndex
    {
        get => (int)this.GetValue(SelectedIndexProperty);
        set => this.SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The tab showing. Two-way, and kept in step with <see cref="SelectedIndex"/>.</summary>
    public RibbonTab? SelectedTab
    {
        get => (RibbonTab?)this.GetValue(SelectedTabProperty);
        set => this.SetValue(SelectedTabProperty, value);
    }

    /// <summary>
    /// Expanded, collapsed to the tab strip, or the one-row simplified layout. Two-way, so a toggle in
    /// the app's own chrome can drive it as well as the ribbon's chevron.
    /// </summary>
    public RibbonDisplayMode DisplayMode
    {
        get => (RibbonDisplayMode)this.GetValue(DisplayModeProperty);
        set => this.SetValue(DisplayModeProperty, value);
    }

    /// <summary>
    /// Offers the chevron that collapses the ribbon, and makes a double-tap on a tab do the same. Turn
    /// it off for chrome that has to stay put; <see cref="DisplayMode"/> still works from code.
    /// </summary>
    public bool AllowCollapse
    {
        get => (bool)this.GetValue(AllowCollapseProperty);
        set => this.SetValue(AllowCollapseProperty, value);
    }

    /// <summary>
    /// Text for the accented button at the head of the strip — "File" in most apps. Null (the default)
    /// leaves it out entirely.
    /// </summary>
    public string? ApplicationButtonText
    {
        get => (string?)this.GetValue(ApplicationButtonTextProperty);
        set => this.SetValue(ApplicationButtonTextProperty, value);
    }

    /// <summary>Runs when the application button is pressed. <see cref="ApplicationButtonClicked"/> follows it.</summary>
    public ICommand? ApplicationButtonCommand
    {
        get => (ICommand?)this.GetValue(ApplicationButtonCommandProperty);
        set => this.SetValue(ApplicationButtonCommandProperty, value);
    }

    /// <summary>Draws <see cref="QuickAccessItems"/> at the trailing end of the tab strip. Default true.</summary>
    public bool ShowQuickAccess
    {
        get => (bool)this.GetValue(ShowQuickAccessProperty);
        set => this.SetValue(ShowQuickAccessProperty, value);
    }

    /// <summary>
    /// Draws each group's caption under it. Default true; always false in
    /// <see cref="RibbonDisplayMode.Simplified"/>, where there is no room for a second line.
    /// </summary>
    public bool ShowGroupTitles
    {
        get => (bool)this.GetValue(ShowGroupTitlesProperty);
        set => this.SetValue(ShowGroupTitlesProperty, value);
    }

    /// <summary>
    /// Draws the tab strip. Set false for a single-tab ribbon that is really just a toolbar — the one
    /// tab's groups still show, without a strip above them saying so.
    /// </summary>
    public bool ShowTabStrip
    {
        get => (bool)this.GetValue(ShowTabStripProperty);
        set => this.SetValue(ShowTabStripProperty, value);
    }

    /// <summary>
    /// How many <see cref="RibbonItemSize.Small"/> items stack in one column before a new one starts.
    /// Three is the ribbon convention and what the body's height is sized from; changing it changes the
    /// height of the whole bar.
    /// </summary>
    public int SmallItemRows
    {
        get => (int)this.GetValue(SmallItemRowsProperty);
        set => this.SetValue(SmallItemRowsProperty, value);
    }

    /// <summary>
    /// A fixed height for every <see cref="RibbonItemSize.Small"/> row. Zero, the default, lets each
    /// row size to whatever is in it.
    /// </summary>
    /// <remarks>
    /// Every group lays its own columns out, so with auto-sized rows the groups never agree on where a
    /// row sits: a group holding a 30px picker grows only its own rows, and the buttons in the group
    /// beside it - still at their own height - sit on a different line, with the group titles below
    /// them landing on different baselines. Nothing is wrong with any one group; the bar simply reads
    /// as ragged. Setting one height here is what puts them all on the same rows, and it is the reason
    /// a bar that mixes hosted pickers with icon buttons needs to set it at all.
    /// </remarks>
    public double SmallItemRowHeight
    {
        get => (double)this.GetValue(SmallItemRowHeightProperty);
        set => this.SetValue(SmallItemRowHeightProperty, value);
    }

    /// <summary>
    /// Lets groups collapse to a single button when the tab is wider than the ribbon. Turn it off and
    /// the body scrolls horizontally instead, which is the better answer when every group is small.
    /// </summary>
    public bool AllowGroupCollapse
    {
        get => (bool)this.GetValue(AllowGroupCollapseProperty);
        set => this.SetValue(AllowGroupCollapseProperty, value);
    }

    /// <summary>The selected tab's underline and the application button's fill. Falls back to the theme's primary.</summary>
    /// <inheritdoc cref="SimplifyBelowWidthProperty" />
    public double SimplifyBelowWidth
    {
        get => (double)this.GetValue(SimplifyBelowWidthProperty);
        set => this.SetValue(SimplifyBelowWidthProperty, value);
    }

    public Color? AccentColor
    {
        get => (Color?)this.GetValue(AccentColorProperty);
        set => this.SetValue(AccentColorProperty, value);
    }

    /// <summary>Fill behind the tab strip. Falls back to the theme.</summary>
    public Color? HeaderBackgroundColor
    {
        get => (Color?)this.GetValue(HeaderBackgroundColorProperty);
        set => this.SetValue(HeaderBackgroundColorProperty, value);
    }

    /// <summary>
    /// Ink for the tab strip, when <see cref="HeaderBackgroundColor"/> is something the theme's own
    /// text colour would not be legible on.
    /// </summary>
    /// <remarks>
    /// Needed the moment the header is painted a saturated colour rather than a surface tint: the tab
    /// labels take <c>OnSurface</c>, which is near-black in a light theme and would vanish into a
    /// Word-blue or Excel-green band. Null leaves them on the theme, which is right for a header that
    /// is still a surface.
    /// </remarks>
    public Color? HeaderForegroundColor
    {
        get => (Color?)this.GetValue(HeaderForegroundColorProperty);
        set => this.SetValue(HeaderForegroundColorProperty, value);
    }

    /// <summary>Fill behind the groups. Falls back to the theme.</summary>
    public Color? BodyBackgroundColor
    {
        get => (Color?)this.GetValue(BodyBackgroundColorProperty);
        set => this.SetValue(BodyBackgroundColorProperty, value);
    }

    /// <summary>
    /// Hover tooltips on the buttons. On by default because a ribbon is a desktop control with a
    /// pointer over it; turn it off on a touch head, where a hover tip can never be seen anyway.
    /// </summary>
    public bool ShowTooltips
    {
        get => (bool)this.GetValue(ShowTooltipsProperty);
        set => this.SetValue(ShowTooltipsProperty, value);
    }
}
