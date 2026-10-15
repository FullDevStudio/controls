using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using Shiny.Maui.Controls.Images;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// A person (or thing) in a small round frame: their picture when there is one, their initials when
/// there is not, and a generic silhouette when there is not even a name. An optional presence dot sits
/// on the bottom-right corner.
/// </summary>
/// <remarks>
/// <para>Remote pictures load through <see cref="ShinyImage"/>, so they are cached, de-duplicated and
/// queued like every other image in the app. The initials are drawn underneath while the picture
/// loads and stay put if it fails, so a broken URL degrades to initials rather than to a hole.</para>
/// <para>Inside an <see cref="AvatarGroup"/> the group owns <see cref="Size"/>, <see cref="Shape"/>
/// and the separator ring of every avatar it holds.</para>
/// </remarks>
public partial class Avatar : ContentView, IView
{
    readonly Grid root;
    readonly Border face;
    readonly Grid silhouette;
    readonly Ellipse silhouetteHead;
    readonly Ellipse silhouetteBody;
    readonly Label initialsLabel;
    readonly ShinyImage image;
    readonly Border statusDot;
    readonly TapGestureRecognizer tap;

    EventHandler? clicked;
    bool isOverflowChip;

    public Avatar()
    {
        this.silhouetteHead = new Ellipse
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true
        };
        this.silhouetteBody = new Ellipse
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true
        };
        this.silhouette = new Grid
        {
            InputTransparent = true,
            Opacity = 0.7,
            Children = { this.silhouetteHead, this.silhouetteBody }
        };

        this.initialsLabel = new Label
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap,
            MaxLines = 1,
            FontAttributes = Microsoft.Maui.Controls.FontAttributes.Bold,
            InputTransparent = true
        };

        // Nothing over the initials while loading or after a failure: the initials ARE the loading
        // state and the error state. A spinner at 32px is noise.
        this.image = new ShinyImage
        {
            Aspect = Aspect.AspectFill,
            IsVisible = false,
            InputTransparent = true,
            LoadingTemplate = new DataTemplate(() => new ContentView()),
            ErrorTemplate = new DataTemplate(() => new ContentView())
        };
        this.image.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShinyImage.State))
                this.ApplyContent();
        };

        this.face = new Border
        {
            Padding = 0,
            StrokeThickness = 0,
            Content = new Grid
            {
                InputTransparent = true,
                Children = { this.silhouette, this.initialsLabel, this.image }
            }
        };

        this.statusDot = new Border
        {
            Padding = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            StrokeShape = new Ellipse(),
            InputTransparent = true,
            IsVisible = false
        };

        this.root = new Grid { Children = { this.face, this.statusDot } };
        this.Content = this.root;

        this.tap = new TapGestureRecognizer();
        this.tap.Command = new Command(this.OnTapped);

        this.ApplyAll();

        // Last line: replays any styled property that was applied before the children existed.
        // See StyleGuard.
        StyleGuard.MarkReady(this, typeof(Avatar));
    }


    /// <summary>Raised when the avatar is tapped. Subscribing makes it tappable.</summary>
    public event EventHandler? Clicked
    {
        add
        {
            this.clicked += value;
            this.UpdateTappable();
        }
        remove
        {
            this.clicked -= value;
            this.UpdateTappable();
        }
    }


    // Start/Center unless something - a local value, a Style, a binding - says otherwise. See DefaultAlignment.
    Microsoft.Maui.Primitives.LayoutAlignment IView.HorizontalLayoutAlignment
        => DefaultAlignment.Resolve(this, HorizontalOptionsProperty, Microsoft.Maui.Primitives.LayoutAlignment.Start);

    Microsoft.Maui.Primitives.LayoutAlignment IView.VerticalLayoutAlignment
        => DefaultAlignment.Resolve(this, VerticalOptionsProperty, Microsoft.Maui.Primitives.LayoutAlignment.Center);


    /// <summary>The initials actually shown: <see cref="Initials"/> when set, otherwise derived from <see cref="Name"/>.</summary>
    public string DisplayInitials
        => !String.IsNullOrWhiteSpace(this.Initials) ? this.Initials! : AvatarText.GetInitials(this.Name);


    /// <summary>True when a picture has loaded and is covering the initials.</summary>
    public bool IsShowingImage => this.HasImage && this.image.State == ImageLoadState.Loaded;


    bool HasImage => !String.IsNullOrWhiteSpace(this.Uri) || this.Source is not null;


    // The test seam: TapGestureRecognizer.Tapped cannot be raised from a test, its Command can.
    internal ICommand TapCommand => this.tap.Command;


    void OnTapped()
    {
        this.clicked?.Invoke(this, EventArgs.Empty);

        var command = this.Command;
        var parameter = this.CommandParameter;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }


    void UpdateTappable()
    {
        var wanted = this.clicked is not null || this.Command is not null;
        var has = this.GestureRecognizers.Contains(this.tap);

        // Only an avatar somebody listens to takes the tap: an idle recognizer on every row of a list
        // would sit between the finger and the list's own selection.
        if (wanted && !has)
            this.GestureRecognizers.Add(this.tap);
        else if (!wanted && has)
            this.GestureRecognizers.Remove(this.tap);
    }


    /// <summary>Turns this avatar into an AvatarGroup's "+N" chip.</summary>
    internal void MarkAsOverflowChip()
    {
        this.isOverflowChip = true;
        this.ApplyColors();
    }


    void ApplyAll()
    {
        this.ApplySize();
        this.ApplyColors();
        this.ApplyContent();
        this.ApplyStatus();
    }


    void ApplySize()
    {
        var size = Math.Max(1, this.Size);

        this.WidthRequest = size;
        this.HeightRequest = size;
        this.face.WidthRequest = size;
        this.face.HeightRequest = size;

        this.face.StrokeShape = this.Shape switch
        {
            AvatarShape.Rounded => new RoundRectangle { CornerRadius = size * 0.25 },
            AvatarShape.Square => new Microsoft.Maui.Controls.Shapes.Rectangle(),
            _ => new Ellipse()
        };
        this.face.StrokeThickness = Math.Max(0, this.RingThickness);

        this.initialsLabel.FontSize = this.FontSize > 0
            ? this.FontSize
            : Math.Max(8, size * (this.DisplayInitials.Length > 2 ? 0.32 : 0.4));

        this.silhouetteHead.WidthRequest = size * 0.38;
        this.silhouetteHead.HeightRequest = size * 0.38;
        this.silhouetteHead.Margin = new Thickness(0, size * 0.18, 0, 0);
        this.silhouetteBody.WidthRequest = size * 0.72;
        this.silhouetteBody.HeightRequest = size * 0.56;
        this.silhouetteBody.Margin = new Thickness(0, size * 0.62, 0, 0);

        var dot = Math.Max(8, size * 0.28);
        this.statusDot.WidthRequest = dot;
        this.statusDot.HeightRequest = dot;
        this.statusDot.StrokeThickness = Math.Max(1.5, size * 0.05);
    }


    void ApplyColors()
    {
        Color? fill = this.FillColor;
        Color? text = this.TextColor;

        if (fill is null && this.ColorFromName && !this.isOverflowChip && !String.IsNullOrWhiteSpace(this.Name))
        {
            fill = AvatarColors.FromName(this.Name);
            text ??= Colors.White;
        }

        ThemeProbe.Tint(this.face, VisualElement.BackgroundColorProperty, fill,
            this.isOverflowChip ? ShinyThemeKeys.Color.SurfaceContainerHighest : ShinyThemeKeys.Color.PrimaryContainer);

        var textToken = this.isOverflowChip ? ShinyThemeKeys.Color.OnSurfaceVariant : ShinyThemeKeys.Color.OnPrimaryContainer;
        var brushToken = this.isOverflowChip ? ShinyThemeKeys.Brush.OnSurfaceVariant : ShinyThemeKeys.Brush.OnPrimaryContainer;
        ThemeProbe.Tint(this.initialsLabel, Label.TextColorProperty, text, textToken);
        ThemeBrush.Apply(this.silhouetteHead, Microsoft.Maui.Controls.Shapes.Shape.FillProperty, text, brushToken);
        ThemeBrush.Apply(this.silhouetteBody, Microsoft.Maui.Controls.Shapes.Shape.FillProperty, text, brushToken);

        // The ring separates overlapping avatars, so by default it is the colour of whatever they sit on.
        ThemeBrush.Apply(this.face, Border.StrokeProperty, this.RingColor, ShinyThemeKeys.Brush.Surface);
        ThemeBrush.Apply(this.statusDot, Border.StrokeProperty, ShinyThemeKeys.Brush.Surface);
    }


    void ApplyContent()
    {
        var initials = this.DisplayInitials;
        this.initialsLabel.Text = initials;

        var hasImage = this.HasImage;
        if (!hasImage)
        {
            this.image.Uri = null;
            this.image.Source = null;
        }
        else if (!String.IsNullOrWhiteSpace(this.Uri))
        {
            this.image.Source = null;
            this.image.Uri = this.Uri;
        }
        else
        {
            this.image.Uri = null;
            this.image.Source = this.Source;
        }

        var showingImage = hasImage && this.image.State == ImageLoadState.Loaded;
        this.image.IsVisible = hasImage;
        this.initialsLabel.IsVisible = !showingImage && initials.Length > 0;
        this.silhouette.IsVisible = !showingImage && initials.Length == 0;

        this.UpdateSemantics();
    }


    void ApplyStatus()
    {
        var status = this.Status;
        this.statusDot.IsVisible = status != AvatarStatus.None;

        var token = status switch
        {
            AvatarStatus.Online => ShinyThemeKeys.Color.Success,
            AvatarStatus.Away => ShinyThemeKeys.Color.Warning,
            AvatarStatus.Busy => ShinyThemeKeys.Color.Error,
            _ => ShinyThemeKeys.Color.Outline
        };
        ThemeProbe.Tint(this.statusDot, VisualElement.BackgroundColorProperty, this.StatusColor, token);

        this.UpdateSemantics();
    }


    void UpdateSemantics()
    {
        var label = this.isOverflowChip
            ? this.DisplayInitials
            : !String.IsNullOrWhiteSpace(this.Name) ? this.Name! : this.DisplayInitials;

        if (this.Status != AvatarStatus.None && !this.isOverflowChip)
            label = String.IsNullOrEmpty(label) ? this.Status.ToString() : $"{label}, {this.Status}";

        SemanticProperties.SetDescription(this.root, String.IsNullOrEmpty(label) ? null : label);
    }
}


/// <summary>The colour a name maps to when <see cref="Avatar.ColorFromName"/> is on.</summary>
static class AvatarColors
{
    // Saturation and lightness are fixed so white initials read on every hue, in both schemes.
    public static Color FromName(string? name)
        => Color.FromHsla(AvatarText.GetHue(name) / 360.0, 0.55, 0.42);
}
