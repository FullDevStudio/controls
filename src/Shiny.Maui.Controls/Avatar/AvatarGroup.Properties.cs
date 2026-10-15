using System.Collections;
using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls;

public partial class AvatarGroup
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(AvatarGroup), null,
        propertyChanged: (b, o, n) => ((AvatarGroup)b).OnItemsSourceChanged(o as IEnumerable, n as IEnumerable));
    /// <summary>Items to draw an avatar for. Each one goes through <see cref="ItemTemplate"/>; with no template a string becomes an avatar of that name.</summary>
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(AvatarGroup), null,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>A template (or <see cref="DataTemplateSelector"/>) whose root is an <see cref="Avatar"/>.</summary>
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    public static readonly BindableProperty MaxProperty = BindableProperty.Create(
        nameof(Max), typeof(int), typeof(AvatarGroup), 0,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>The most avatars drawn before the rest collapse into the "+N" chip (the chip is not counted). <c>0</c> draws them all.</summary>
    public int Max { get => (int)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    public static readonly BindableProperty TotalCountProperty = BindableProperty.Create(
        nameof(TotalCount), typeof(int), typeof(AvatarGroup), 0,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>The real number of people when the avatars given are only a sample - "+N" counts against this. <c>0</c> uses the number of avatars.</summary>
    public int TotalCount { get => (int)GetValue(TotalCountProperty); set => SetValue(TotalCountProperty, value); }

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(AvatarGroup), -1.0,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>When positive, the size of every avatar in the group. The default, <c>-1</c>, leaves each avatar's own size alone.</summary>
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public static readonly BindableProperty ShapeProperty = BindableProperty.Create(
        nameof(Shape), typeof(AvatarShape?), typeof(AvatarGroup), null,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>When set, the shape of every avatar in the group.</summary>
    public AvatarShape? Shape { get => (AvatarShape?)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    public static readonly BindableProperty OverlapProperty = BindableProperty.Create(
        nameof(Overlap), typeof(double), typeof(AvatarGroup), -1.0,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>How far each avatar tucks under the one before it. The default, <c>-1</c>, is a quarter of the avatar size; <c>0</c> lays them side by side.</summary>
    public double Overlap { get => (double)GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(AvatarGroup), null,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>The separator ring around each avatar. Unset follows the theme surface - set it to whatever the group sits on.</summary>
    public Color? RingColor { get => (Color?)GetValue(RingColorProperty); set => SetValue(RingColorProperty, value); }

    public static readonly BindableProperty RingThicknessProperty = BindableProperty.Create(
        nameof(RingThickness), typeof(double), typeof(AvatarGroup), 2.0,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>Thickness of the separator ring. <c>0</c> draws none.</summary>
    public double RingThickness { get => (double)GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    public static readonly BindableProperty OverflowFormatProperty = BindableProperty.Create(
        nameof(OverflowFormat), typeof(string), typeof(AvatarGroup), "+{0}",
        propertyChanged: (b, _, _) => ((AvatarGroup)b).Rebuild());
    /// <summary>Composite format for the overflow chip; <c>{0}</c> is the hidden count.</summary>
    public string OverflowFormat { get => (string)GetValue(OverflowFormatProperty); set => SetValue(OverflowFormatProperty, value); }

    public static readonly BindableProperty OverflowCommandProperty = BindableProperty.Create(
        nameof(OverflowCommand), typeof(ICommand), typeof(AvatarGroup), null,
        propertyChanged: (b, _, _) => ((AvatarGroup)b).UpdateOverflowTappable());
    /// <summary>Executed when the "+N" chip is tapped, with the hidden count as its parameter - typically to show the full list.</summary>
    public ICommand? OverflowCommand { get => (ICommand?)GetValue(OverflowCommandProperty); set => SetValue(OverflowCommandProperty, value); }

    static readonly BindablePropertyKey OverflowCountPropertyKey = BindableProperty.CreateReadOnly(
        nameof(OverflowCount), typeof(int), typeof(AvatarGroup), 0);
    public static readonly BindableProperty OverflowCountProperty = OverflowCountPropertyKey.BindableProperty;
    /// <summary>How many people the "+N" chip stands for. <c>0</c> when everyone is drawn.</summary>
    public int OverflowCount => (int)GetValue(OverflowCountProperty);
}
