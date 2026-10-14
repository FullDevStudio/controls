namespace Shiny.Maui.Controls;

/// <summary>
/// A card with a short beam of light that travels endlessly around its border, in the style of
/// Magic UI's border beam.
/// </summary>
/// <example>
/// <code>
/// &lt;shiny:BorderBeam CornerRadius="16" BorderWidth="2" Padding="20"&gt;
///     &lt;Label Text="Upgrade to Pro" /&gt;
/// &lt;/shiny:BorderBeam&gt;
/// </code>
/// </example>
/// <remarks>
/// The beam is <see cref="Size"/> long, brightest at its head in <see cref="ColorFrom"/>, shading
/// through <see cref="ColorTo"/> and fading out at its tail. With no content it is a plain overlay: lay
/// a second one (<see cref="AnimatedBorder.TrackColor"/> Transparent, <c>InputTransparent</c> True,
/// <see cref="Reverse"/>d or with an <see cref="InitialOffset"/>) over the first in a Grid for the
/// double-beam look.
/// </remarks>
public class BorderBeam : AnimatedBorder
{
    public BorderBeam() => Infrastructure.StyleGuard.MarkReady(this, typeof(BorderBeam));


    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(BorderBeam), 50d,
        propertyChanged: (b, _, _) => ((BorderBeam)b).Invalidate());

    /// <summary>How long the beam is, measured along the border. Default 50.</summary>
    public double Size
    {
        get => (double)this.GetValue(SizeProperty);
        set => this.SetValue(SizeProperty, value);
    }

    public static readonly BindableProperty DurationProperty = BindableProperty.Create(
        nameof(Duration), typeof(TimeSpan), typeof(BorderBeam), TimeSpan.FromSeconds(6),
        propertyChanged: (b, _, _) => ((BorderBeam)b).Invalidate());

    /// <summary>How long one lap takes. Default 6 seconds.</summary>
    public TimeSpan Duration
    {
        get => (TimeSpan)this.GetValue(DurationProperty);
        set => this.SetValue(DurationProperty, value);
    }

    public static readonly BindableProperty DelayProperty = BindableProperty.Create(
        nameof(Delay), typeof(TimeSpan), typeof(BorderBeam), TimeSpan.Zero,
        propertyChanged: (b, _, _) => ((BorderBeam)b).Invalidate());

    /// <summary>
    /// How far into its lap the beam already is when it appears - as in Magic UI, this is a head start
    /// rather than a wait, so the beam is never missing.
    /// </summary>
    public TimeSpan Delay
    {
        get => (TimeSpan)this.GetValue(DelayProperty);
        set => this.SetValue(DelayProperty, value);
    }

    public static readonly BindableProperty ColorFromProperty = BindableProperty.Create(
        nameof(ColorFrom), typeof(Color), typeof(BorderBeam), Color.FromArgb("#FFAA40"),
        propertyChanged: (b, _, _) => ((BorderBeam)b).Invalidate());

    /// <summary>The colour at the head of the beam. Default #FFAA40.</summary>
    public Color ColorFrom
    {
        get => (Color)this.GetValue(ColorFromProperty);
        set => this.SetValue(ColorFromProperty, value);
    }

    public static readonly BindableProperty ColorToProperty = BindableProperty.Create(
        nameof(ColorTo), typeof(Color), typeof(BorderBeam), Color.FromArgb("#9C40FF"),
        propertyChanged: (b, _, _) => ((BorderBeam)b).Invalidate());

    /// <summary>The colour the beam shades into before its tail fades out. Default #9C40FF.</summary>
    public Color ColorTo
    {
        get => (Color)this.GetValue(ColorToProperty);
        set => this.SetValue(ColorToProperty, value);
    }

    public static readonly BindableProperty ReverseProperty = BindableProperty.Create(
        nameof(Reverse), typeof(bool), typeof(BorderBeam), false,
        propertyChanged: (b, _, _) => ((BorderBeam)b).Invalidate());

    /// <summary>Travels anticlockwise instead of clockwise.</summary>
    public bool Reverse
    {
        get => (bool)this.GetValue(ReverseProperty);
        set => this.SetValue(ReverseProperty, value);
    }

    public static readonly BindableProperty InitialOffsetProperty = BindableProperty.Create(
        nameof(InitialOffset), typeof(double), typeof(BorderBeam), 0d,
        propertyChanged: (b, _, _) => ((BorderBeam)b).Invalidate());

    /// <summary>Where on the lap the beam starts, as a percentage (0-100) of the way round from the top-left.</summary>
    public double InitialOffset
    {
        get => (double)this.GetValue(InitialOffsetProperty);
        set => this.SetValue(InitialOffsetProperty, value);
    }


    /// <summary>Where the centre of the beam is, as a fraction (0 to 1) of the lap clockwise from the top-left.</summary>
    internal double Position
    {
        get
        {
            var p = this.LoopProgress(this.Duration, this.Delay) + (this.InitialOffset / 100);
            p -= Math.Floor(p);
            return this.Reverse ? (1 - p) % 1 : p;
        }
    }


    /// <summary>
    /// The beam's colour <paramref name="fromHead"/> of the way back from its head: Magic UI's
    /// <c>linear-gradient(to left, from, to, transparent)</c> laid along the direction of travel.
    /// </summary>
    internal Color ColorAlong(double fromHead)
    {
        fromHead = Math.Clamp(fromHead, 0, 1);
        return fromHead <= 0.5
            ? Lerp(this.ColorFrom, this.ColorTo, fromHead * 2)
            // Out to the tail colour's own zero-alpha variant, not Transparent (#00000000), so the fade
            // does not drag the hue through black on the way.
            : Lerp(this.ColorTo, this.ColorTo.WithAlpha(0), (fromHead - 0.5) * 2);
    }


    internal override void DrawEffect(ICanvas canvas, BorderPath path, float width, float height)
    {
        var length = path.Length;
        var size = Math.Min(Math.Max(this.Size, 0), length);
        if (size <= 0)
            return;

        var center = this.Position * length;
        var direction = this.Reverse ? -1 : 1;
        var head = center + (direction * size / 2);

        // Short segments, each in its own colour: the beam bends round the corners with the border,
        // which a gradient across a rectangle (Magic UI's square) cannot do outside a mask.
        var count = Math.Max(2, (int)Math.Ceiling(size / 1.5));
        var step = size / count;

        for (var i = 0; i < count; i++)
        {
            var from = head - (direction * step * i);
            var to = from - (direction * step);

            canvas.StrokeColor = this.ColorAlong((i + 0.5) / count);
            var a = path.PointAt(from);
            var b = path.PointAt(to);
            canvas.DrawLine(a.X, a.Y, b.X, b.Y);
        }
    }


    internal static Color Lerp(Color a, Color b, double t)
    {
        var f = (float)Math.Clamp(t, 0, 1);
        return new Color(
            a.Red + ((b.Red - a.Red) * f),
            a.Green + ((b.Green - a.Green) * f),
            a.Blue + ((b.Blue - a.Blue) * f),
            a.Alpha + ((b.Alpha - a.Alpha) * f)
        );
    }
}
