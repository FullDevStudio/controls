using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls;

public partial class NotchView
{
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(View), typeof(NotchView), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(NotchView), () =>
            ((NotchView)b).OnContentChanged((View?)o, (View?)n)));
    /// <summary>
    /// Anything you like. The notch measures it and morphs its width, height and corners to fit, and
    /// does so again whenever the content's own size changes or the content is swapped.
    /// </summary>
    public View? Content { get => (View?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    public static readonly BindableProperty ShapeProperty = BindableProperty.Create(
        nameof(Shape), typeof(NotchShape), typeof(NotchView), NotchShape.Island,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).ApplyOutline()));
    /// <summary>
    /// <see cref="NotchShape.Island"/> (every corner rounded, the default) or
    /// <see cref="NotchShape.Notch"/> (square top corners, for a notch flush with the top edge).
    /// </summary>
    public NotchShape Shape { get => (NotchShape)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    public static readonly BindableProperty SurfaceColorProperty = BindableProperty.Create(
        nameof(SurfaceColor), typeof(Color), typeof(NotchView), Colors.Black,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).ApplySurface()));
    /// <summary>
    /// The fill. Black by default on every theme, because the look being imitated is a hole in the
    /// screen — so give the content light text.
    /// </summary>
    public Color SurfaceColor { get => (Color)GetValue(SurfaceColorProperty); set => SetValue(SurfaceColorProperty, value); }

    public static readonly BindableProperty StrokeProperty = BindableProperty.Create(
        nameof(Stroke), typeof(Color), typeof(NotchView), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).ApplySurface()));
    /// <summary>Optional outline colour. None by default.</summary>
    public Color? Stroke { get => (Color?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public static readonly BindableProperty StrokeThicknessProperty = BindableProperty.Create(
        nameof(StrokeThickness), typeof(double), typeof(NotchView), 0d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).ApplySurface()));
    /// <summary>Outline thickness. Only drawn when <see cref="Stroke"/> is set.</summary>
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    public static readonly BindableProperty ContentPaddingProperty = BindableProperty.Create(
        nameof(ContentPadding), typeof(Thickness), typeof(NotchView), new Thickness(14, 8),
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).Refresh()));
    /// <summary>Space between the edge of the notch and its content.</summary>
    public Thickness ContentPadding { get => (Thickness)GetValue(ContentPaddingProperty); set => SetValue(ContentPaddingProperty, value); }

    public static readonly BindableProperty MinNotchWidthProperty = BindableProperty.Create(
        nameof(MinNotchWidth), typeof(double), typeof(NotchView), 120d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).Refresh()));
    /// <summary>The narrowest the notch gets — and its width when there is no content.</summary>
    public double MinNotchWidth { get => (double)GetValue(MinNotchWidthProperty); set => SetValue(MinNotchWidthProperty, value); }

    public static readonly BindableProperty MinNotchHeightProperty = BindableProperty.Create(
        nameof(MinNotchHeight), typeof(double), typeof(NotchView), 36d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).Refresh()));
    /// <summary>The shortest the notch gets — and its height when there is no content.</summary>
    public double MinNotchHeight { get => (double)GetValue(MinNotchHeightProperty); set => SetValue(MinNotchHeightProperty, value); }

    public static readonly BindableProperty MaxNotchWidthProperty = BindableProperty.Create(
        nameof(MaxNotchWidth), typeof(double), typeof(NotchView), Double.PositiveInfinity,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).Refresh()));
    /// <summary>
    /// The widest the notch gets. Unbounded by default, in which case the width the parent offers is
    /// the limit and content wider than that wraps.
    /// </summary>
    public double MaxNotchWidth { get => (double)GetValue(MaxNotchWidthProperty); set => SetValue(MaxNotchWidthProperty, value); }

    public static readonly BindableProperty MaxNotchHeightProperty = BindableProperty.Create(
        nameof(MaxNotchHeight), typeof(double), typeof(NotchView), Double.PositiveInfinity,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).Refresh()));
    /// <summary>The tallest the notch gets. Content taller than this is clipped.</summary>
    public double MaxNotchHeight { get => (double)GetValue(MaxNotchHeightProperty); set => SetValue(MaxNotchHeightProperty, value); }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(NotchView), -1d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).ApplyOutline()));
    /// <summary>
    /// Corner radius. The default, <c>-1</c>, is automatic: a full pill while the notch is short,
    /// capped at 28 as it grows tall, which is what keeps an expanded notch from turning into an
    /// oval. Any value is clamped to half the notch's height.
    /// </summary>
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public static readonly BindableProperty MorphDurationProperty = BindableProperty.Create(
        nameof(MorphDuration), typeof(uint), typeof(NotchView), 420u);
    /// <summary>How long a resize takes, in milliseconds. <c>0</c> snaps to the new size.</summary>
    public uint MorphDuration { get => (uint)GetValue(MorphDurationProperty); set => SetValue(MorphDurationProperty, value); }

    public static readonly BindableProperty MorphEasingProperty = BindableProperty.Create(
        nameof(MorphEasing), typeof(Easing), typeof(NotchView), null);
    /// <summary>The resize curve. When null, a soft spring that overshoots slightly before settling.</summary>
    public Easing? MorphEasing { get => (Easing?)GetValue(MorphEasingProperty); set => SetValue(MorphEasingProperty, value); }

    public static readonly BindableProperty HasShadowProperty = BindableProperty.Create(
        nameof(HasShadow), typeof(bool), typeof(NotchView), true,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(NotchView), () => ((NotchView)b).ApplySurface()));
    /// <summary>Draws a soft drop shadow under the notch. Turn it off when the notch is flush with a dark edge.</summary>
    public bool HasShadow { get => (bool)GetValue(HasShadowProperty); set => SetValue(HasShadowProperty, value); }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(NotchView), null);
    /// <summary>Executed when the notch surface is tapped (taps a button inside the content handles itself).</summary>
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(NotchView), null);
    /// <summary>Passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    static readonly BindablePropertyKey NotchSizePropertyKey = BindableProperty.CreateReadOnly(
        nameof(NotchSize), typeof(Size), typeof(NotchView), Size.Zero);
    public static readonly BindableProperty NotchSizeProperty = NotchSizePropertyKey.BindableProperty;
    /// <summary>
    /// The size the notch is at, or is morphing towards — content plus padding, clamped to the min/max.
    /// </summary>
    public Size NotchSize { get => (Size)GetValue(NotchSizeProperty); private set => SetValue(NotchSizePropertyKey, value); }
}
