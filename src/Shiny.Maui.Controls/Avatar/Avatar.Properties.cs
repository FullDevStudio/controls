using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls;

public partial class Avatar
{
    public static readonly BindableProperty NameProperty = BindableProperty.Create(
        nameof(Name), typeof(string), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyContent();
            a.ApplyColors();
            a.ApplySize();
        }));
    /// <summary>The person's name. Drives the initials, the accessible description and, with <see cref="ColorFromName"/>, the fill colour.</summary>
    public string? Name { get => (string?)GetValue(NameProperty); set => SetValue(NameProperty, value); }

    public static readonly BindableProperty InitialsProperty = BindableProperty.Create(
        nameof(Initials), typeof(string), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyContent();
            a.ApplySize();
        }));
    /// <summary>Initials to show instead of the ones derived from <see cref="Name"/>.</summary>
    public string? Initials { get => (string?)GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }

    public static readonly BindableProperty UriProperty = BindableProperty.Create(
        nameof(Uri), typeof(string), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyContent();
        }));
    /// <summary>A picture to load through <see cref="Images.ShinyImage"/> (http/https, file, <c>resource://</c> or <c>data:</c>). Wins over <see cref="Source"/>.</summary>
    public string? Uri { get => (string?)GetValue(UriProperty); set => SetValue(UriProperty, value); }

    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source), typeof(ImageSource), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyContent();
        }));
    /// <summary>A picture from any MAUI <see cref="ImageSource"/>. Used when <see cref="Uri"/> is empty.</summary>
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(Avatar), 40.0,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplySize();
        }));
    /// <summary>Width and height in device-independent units.</summary>
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public static readonly BindableProperty ShapeProperty = BindableProperty.Create(
        nameof(Shape), typeof(AvatarShape), typeof(Avatar), AvatarShape.Circle,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplySize();
        }));
    /// <summary>Circle, rounded square or square.</summary>
    public AvatarShape Shape { get => (AvatarShape)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    public static readonly BindableProperty StatusProperty = BindableProperty.Create(
        nameof(Status), typeof(AvatarStatus), typeof(Avatar), AvatarStatus.None,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyStatus();
        }));
    /// <summary>The presence dot on the bottom-right corner.</summary>
    public AvatarStatus Status { get => (AvatarStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public static readonly BindableProperty StatusColorProperty = BindableProperty.Create(
        nameof(StatusColor), typeof(Color), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyStatus();
        }));
    /// <summary>Overrides the presence dot's colour. Unset follows the theme's success / warning / error / outline tokens.</summary>
    public Color? StatusColor { get => (Color?)GetValue(StatusColorProperty); set => SetValue(StatusColorProperty, value); }

    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(
        nameof(FillColor), typeof(Color), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyColors();
        }));
    /// <summary>Background behind the initials or silhouette. Unset follows the theme's primary container.</summary>
    public Color? FillColor { get => (Color?)GetValue(FillColorProperty); set => SetValue(FillColorProperty, value); }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyColors();
        }));
    /// <summary>Colour of the initials and silhouette. Unset follows the theme's on-primary-container.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    public static readonly BindableProperty ColorFromNameProperty = BindableProperty.Create(
        nameof(ColorFromName), typeof(bool), typeof(Avatar), false,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyColors();
        }));
    /// <summary>When true and <see cref="FillColor"/> is unset, the fill is a stable hue derived from <see cref="Name"/> - the same name always gets the same colour, on every host.</summary>
    public bool ColorFromName { get => (bool)GetValue(ColorFromNameProperty); set => SetValue(ColorFromNameProperty, value); }

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplyColors();
        }));
    /// <summary>Colour of the ring around the avatar. Unset follows the theme surface, which is what separates overlapping avatars.</summary>
    public Color? RingColor { get => (Color?)GetValue(RingColorProperty); set => SetValue(RingColorProperty, value); }

    public static readonly BindableProperty RingThicknessProperty = BindableProperty.Create(
        nameof(RingThickness), typeof(double), typeof(Avatar), 0.0,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplySize();
        }));
    /// <summary>Thickness of the ring. <c>0</c> draws none.</summary>
    public double RingThickness { get => (double)GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(Avatar), -1.0,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.ApplySize();
        }));
    /// <summary>Size of the initials. The default, <c>-1</c>, scales with <see cref="Size"/>.</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(Avatar), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Avatar), () =>
        {
            var a = (Avatar)b;
            a.UpdateTappable();
        }));
    /// <summary>Executed when the avatar is tapped. Setting it makes the avatar tappable.</summary>
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(Avatar), null);
    /// <summary>Passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get => (object?)GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
}
