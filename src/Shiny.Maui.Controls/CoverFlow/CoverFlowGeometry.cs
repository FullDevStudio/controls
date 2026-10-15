namespace Shiny.Maui.Controls;

/// <summary>Where one cover sits, relative to the centre of the stage, for a given offset from the current position.</summary>
/// <param name="TranslationX">Horizontal distance of the cover's centre from the stage's centre.</param>
/// <param name="RotationY">Degrees about the vertical axis. Right-hand covers turn negative, left-hand positive.</param>
/// <param name="Scale">Uniform scale: 1 in the centre, <c>SideScale</c> from one step out.</param>
/// <param name="Opacity">1 near the centre, fading toward <c>SideOpacity</c> and then to nothing past the last visible cover.</param>
/// <param name="Depth">Distance from the centre in items - smaller draws on top.</param>
readonly record struct CoverFlowTransform(double TranslationX, double RotationY, double Scale, double Opacity, double Depth);


/// <summary>The settings the transform depends on, gathered so a test can state them in one place.</summary>
sealed record CoverFlowLayout(
    double CenterSpacing = 130,
    double ItemSpacing = 50,
    double SideAngle = 60,
    double SideScale = 0.85,
    double SideOpacity = 1.0,
    int VisibleItems = 4);


/// <summary>
/// The pure maths behind <see cref="CoverFlowView"/>: where each cover goes, how a position wraps when
/// looping, the rubber band past the ends, and where a flick comes to rest.
/// </summary>
/// <remarks>
/// The Blazor <c>CoverFlowView</c> mirrors these rules in <c>cover-flow.js</c>, so a change here is a
/// change there too - the two hosts are meant to lay the same covers out in the same places.
/// </remarks>
static class CoverFlowGeometry
{
    /// <summary>How long a flick keeps coasting, in seconds, when picking the cover it settles on.</summary>
    public const double FlickCoast = 0.25;

    /// <summary>How much of a drag past the first or last cover actually moves it.</summary>
    public const double RubberBand = 0.35;


    /// <summary>The transform for a cover <paramref name="offset"/> items from the current position.</summary>
    public static CoverFlowTransform For(double offset, CoverFlowLayout layout)
    {
        var distance = Math.Abs(offset);
        var sign = Math.Sign(offset);
        var visible = Math.Max(0, layout.VisibleItems);

        var x = distance <= 1
            ? distance * layout.CenterSpacing
            : layout.CenterSpacing + ((distance - 1) * layout.ItemSpacing);

        var rotation = -Math.Clamp(offset, -1, 1) * layout.SideAngle;
        var scale = Lerp(1, layout.SideScale, Math.Min(distance, 1));

        double opacity;
        if (distance <= 1)
            opacity = 1;
        else if (distance <= visible)
            opacity = visible <= 1 ? layout.SideOpacity : Lerp(1, layout.SideOpacity, (distance - 1) / (visible - 1));
        else if (distance < visible + 1)
            // The cover entering from beyond the last visible slot fades in rather than popping.
            opacity = (visible == 0 ? 1 : layout.SideOpacity) * (visible + 1 - distance);
        else
            opacity = 0;

        return new CoverFlowTransform(sign * x, rotation, scale, Math.Clamp(opacity, 0, 1), distance);
    }


    /// <summary>A non-negative remainder: -1 mod 5 is 4.</summary>
    public static int Mod(int value, int count)
        => count <= 0 ? 0 : ((value % count) + count) % count;


    /// <summary>The item a (possibly virtual, possibly fractional) position rests on.</summary>
    public static int IndexAt(double position, int count, bool loop)
    {
        if (count <= 0)
            return 0;

        var rounded = (int)Math.Round(position, MidpointRounding.AwayFromZero);
        return loop ? Mod(rounded, count) : Math.Clamp(rounded, 0, count - 1);
    }


    /// <summary>
    /// The covers either side of the centre worth showing. Looping with fewer items than slots would put
    /// the same item on both sides at once, so the reach is cut to what the items can fill uniquely.
    /// </summary>
    public static int Reach(int visibleItems, int count, bool loop)
    {
        var reach = Math.Max(0, visibleItems);
        return loop ? Math.Min(reach, Math.Max(0, (count - 1) / 2)) : reach;
    }


    /// <summary>
    /// Where a drag puts the position. Past either end of a non-looping list only
    /// <see cref="RubberBand"/> of the overshoot is applied.
    /// </summary>
    public static double Resist(double position, int count, bool loop)
    {
        if (loop || count <= 0)
            return position;

        var last = count - 1;
        if (position < 0)
            return position * RubberBand;
        if (position > last)
            return last + ((position - last) * RubberBand);

        return position;
    }


    /// <summary>The whole-item position a release at <paramref name="velocity"/> items/second comes to rest on.</summary>
    public static double SettleTarget(double position, double velocity, int count, bool loop)
    {
        if (count <= 0)
            return 0;

        var target = Math.Round(position + (velocity * FlickCoast), MidpointRounding.AwayFromZero);
        return loop ? target : Math.Clamp(target, 0, count - 1);
    }


    /// <summary>
    /// The virtual position of <paramref name="index"/> nearest to <paramref name="from"/>. When looping
    /// this takes the short way round; otherwise it is the index itself.
    /// </summary>
    public static double NearestVirtual(double from, int index, int count, bool loop)
    {
        if (!loop || count <= 0)
            return index;

        var anchor = (int)Math.Round(from, MidpointRounding.AwayFromZero);
        var delta = Mod(index - anchor, count);
        if (delta > count / 2)
            delta -= count;

        return anchor + delta;
    }


    /// <summary>
    /// The on-screen footprint of a cover, ignoring perspective: rotation narrows it by cos(angle) and
    /// scale shrinks it, both about its centre. Used to hit-test taps front to back.
    /// </summary>
    public static Rect Footprint(CoverFlowTransform transform, Point stageCentre, double itemWidth, double itemHeight)
    {
        var width = itemWidth * transform.Scale * Math.Abs(Math.Cos(transform.RotationY * Math.PI / 180));
        var height = itemHeight * transform.Scale;

        return new Rect(
            stageCentre.X + transform.TranslationX - (width / 2),
            stageCentre.Y - (height / 2),
            width,
            height);
    }


    /// <summary>Cubic ease-out: fast away, gentle arrival.</summary>
    public static double EaseOut(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var inverse = 1 - t;
        return 1 - (inverse * inverse * inverse);
    }


    static double Lerp(double from, double to, double t) => from + ((to - from) * t);
}
