using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui.Graphics.Converters;

namespace Shiny.Maui.Controls;

/// <summary>
/// A card whose border shimmers with a slowly drifting multi-colour glow, in the style of Magic UI's
/// shine border.
/// </summary>
/// <example>
/// <code>
/// &lt;shiny:ShineBorder ShineColors="#A07CFE,#FE8FB5,#FFBE7B" BorderWidth="2" Padding="20"&gt;
///     &lt;Label Text="Sign in" /&gt;
/// &lt;/shiny:ShineBorder&gt;
/// </code>
/// </example>
/// <remarks>
/// Magic UI paints a radial gradient - transparent, then the colours, then transparent again - three
/// times the size of the card, and slides it from one corner to the other and back. This draws the same
/// gradient, sampled along the border, so the colours wash in and out as the gradient passes.
/// </remarks>
public class ShineBorder : AnimatedBorder
{
    static readonly IReadOnlyList<Color> DefaultColors =
    [
        Color.FromArgb("#A07CFE"),
        Color.FromArgb("#FE8FB5"),
        Color.FromArgb("#FFBE7B")
    ];

    public ShineBorder() => Infrastructure.StyleGuard.MarkReady(this, typeof(ShineBorder));


    public static readonly BindableProperty ShineColorsProperty = BindableProperty.Create(
        nameof(ShineColors), typeof(IReadOnlyList<Color>), typeof(ShineBorder), DefaultColors,
        propertyChanged: (b, _, _) => ((ShineBorder)b).Invalidate());

    /// <summary>
    /// The colours the shine passes through, in order. In XAML a comma-separated list
    /// (<c>"#A07CFE,#FE8FB5,#FFBE7B"</c>) or a single colour. Default violet, pink and peach.
    /// </summary>
    [TypeConverter(typeof(ColorListTypeConverter))]
    public IReadOnlyList<Color> ShineColors
    {
        get => (IReadOnlyList<Color>)this.GetValue(ShineColorsProperty);
        set => this.SetValue(ShineColorsProperty, value);
    }

    public static readonly BindableProperty DurationProperty = BindableProperty.Create(
        nameof(Duration), typeof(TimeSpan), typeof(ShineBorder), TimeSpan.FromSeconds(14),
        propertyChanged: (b, _, _) => ((ShineBorder)b).Invalidate());

    /// <summary>How long the shine takes to drift across and back. Default 14 seconds.</summary>
    public TimeSpan Duration
    {
        get => (TimeSpan)this.GetValue(DurationProperty);
        set => this.SetValue(DurationProperty, value);
    }


    /// <summary>
    /// Magic UI's <c>background-position</c>, as a fraction: 0 to 1 over the first half of the loop
    /// and back to 0 over the second.
    /// </summary>
    internal double Sweep
    {
        get
        {
            var p = this.LoopProgress(this.Duration, TimeSpan.Zero);
            return p < 0.5 ? p * 2 : 2 - (p * 2);
        }
    }


    /// <summary>
    /// The shine's colour at (<paramref name="x"/>, <paramref name="y"/>) in a <paramref name="width"/>
    /// × <paramref name="height"/> card, at <paramref name="sweep"/>.
    /// </summary>
    /// <remarks>
    /// The CSS this reproduces: <c>background-size: 300% 300%</c> places a 3W × 3H box at
    /// <c>-2W·sweep, -2H·sweep</c>; its <c>radial-gradient</c> is a farthest-corner ellipse from the box
    /// centre, so its radii are the half-box diagonals, 1.5W·√2 by 1.5H·√2.
    /// </remarks>
    internal Color ColorAt(double x, double y, double width, double height, double sweep)
    {
        var cx = (-2 * width * sweep) + (1.5 * width);
        var cy = (-2 * height * sweep) + (1.5 * height);
        var rx = 1.5 * width * Math.Sqrt(2);
        var ry = 1.5 * height * Math.Sqrt(2);

        var dx = (x - cx) / rx;
        var dy = (y - cy) / ry;
        return GradientAt(this.ShineColors, Math.Sqrt((dx * dx) + (dy * dy)));
    }


    /// <summary>
    /// <c>radial-gradient(transparent, transparent, c1 … cn, transparent, transparent)</c> at
    /// <paramref name="t"/>: every stop evenly spaced, the transparent ends taking their neighbour's
    /// hue so the fade never passes through black.
    /// </summary>
    internal static Color GradientAt(IReadOnlyList<Color>? colors, double t)
    {
        if (colors is null || colors.Count == 0)
            return Colors.Transparent;

        var first = colors[0].WithAlpha(0);
        var last = colors[^1].WithAlpha(0);
        var stops = colors.Count + 4;
        var position = Math.Clamp(t, 0, 1) * (stops - 1);
        var index = Math.Min((int)Math.Floor(position), stops - 2);

        return BorderBeam.Lerp(Stop(index), Stop(index + 1), position - index);

        Color Stop(int i) => i switch
        {
            0 or 1 => first,
            _ when i >= colors.Count + 2 => last,
            _ => colors[i - 2]
        };
    }


    internal override void DrawEffect(ICanvas canvas, BorderPath path, float width, float height)
    {
        var colors = this.ShineColors;
        if (colors is null || colors.Count == 0)
            return;

        var sweep = this.Sweep;
        for (var i = 0; i < path.SegmentCount; i++)
        {
            var (a, b, _) = path.Segment(i);
            var color = this.ColorAt((a.X + b.X) / 2, (a.Y + b.Y) / 2, width, height, sweep);
            if (color.Alpha <= 0.004f)
                continue;

            canvas.StrokeColor = color;
            canvas.DrawLine(a.X, a.Y, b.X, b.Y);
        }
    }
}


/// <summary>Reads <c>"#A07CFE,#FE8FB5"</c> (or one colour, or named colours) as a list of colours.</summary>
public class ColorListTypeConverter : TypeConverter
{
    static readonly ColorTypeConverter ColorConverter = new();

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string);

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        var text = value as string ?? string.Empty;
        return Split(text)
            .Select(part => (Color)ColorConverter.ConvertFromInvariantString(part)!)
            .ToArray();
    }

    // Commas inside rgb(...)/hsla(...) belong to the colour, not the list.
    static IEnumerable<string> Split(string text)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length)
            {
                var c = text[i];
                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth = Math.Max(depth - 1, 0);

                if (c != ',' || depth > 0)
                    continue;
            }

            var part = text[start..i].Trim();
            if (part.Length > 0)
                yield return part;
            start = i + 1;
        }
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is IEnumerable<Color> colors
            ? string.Join(",", colors.Select(c => c.ToArgbHex(c.Alpha < 1)))
            : null;
}
