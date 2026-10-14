namespace Shiny.Maui.Controls;

/// <summary>
/// The centre line of a rounded-rectangle border as a closed polyline, walked clockwise from the end
/// of the top-left corner. Both border animations draw onto it: the beam asks for points at a distance
/// along it, the shine strokes every piece of it in its own colour.
/// </summary>
/// <remarks>
/// <para>Corners are subdivided by angle and edges by length, so stroking one segment per colour never
/// shows a visible facet or a gap where two butt-capped segments meet on a curve.</para>
/// <para>Pure geometry with no MAUI types beyond <see cref="PointF"/>, so it is unit tested directly.</para>
/// </remarks>
sealed class BorderPath
{
    /// <summary>The largest turn one corner segment may make. Small enough that a butt-capped join never opens.</summary>
    const double MaxCornerStepRadians = Math.PI / 36; // 5°

    readonly PointF[] points;
    readonly double[] distances;

    BorderPath(PointF[] points, double[] distances)
    {
        this.points = points;
        this.distances = distances;
    }


    /// <summary>A path with nothing on it - the box is too small to hold a border of that width.</summary>
    public static BorderPath Empty { get; } = new([], []);

    /// <summary>The length of one full lap.</summary>
    public double Length => this.distances.Length == 0 ? 0 : this.distances[^1];

    /// <summary>How many segments make up the lap.</summary>
    public int SegmentCount => Math.Max(this.points.Length - 1, 0);


    /// <summary>
    /// Builds the centre line of a border <paramref name="borderWidth"/> wide drawn inside a
    /// <paramref name="width"/> × <paramref name="height"/> box with outer corners of <paramref name="cornerRadius"/>.
    /// </summary>
    /// <param name="maxStep">The longest a straight segment may be.</param>
    public static BorderPath Create(double width, double height, double cornerRadius, double borderWidth, double maxStep = 2)
    {
        var inset = Math.Max(borderWidth, 0) / 2;
        var left = inset;
        var top = inset;
        var right = width - inset;
        var bottom = height - inset;

        if (right - left <= 0 || bottom - top <= 0 || double.IsNaN(width) || double.IsNaN(height))
            return Empty;

        // The stroke sits half a border inside the outer edge, so its own radius is that much tighter -
        // the same thing a CSS border does to its padding-box radius.
        var r = Math.Clamp(cornerRadius - inset, 0, Math.Min(right - left, bottom - top) / 2);
        var step = Math.Max(maxStep, 0.25);

        var pts = new List<PointF> { new((float)(left + r), (float)top) };

        Edge(pts, right - r, top, step);
        Corner(pts, right - r, top + r, r, -Math.PI / 2);
        Edge(pts, right, bottom - r, step);
        Corner(pts, right - r, bottom - r, r, 0);
        Edge(pts, left + r, bottom, step);
        Corner(pts, left + r, bottom - r, r, Math.PI / 2);
        Edge(pts, left, top + r, step);
        Corner(pts, left + r, top + r, r, Math.PI);

        // Close exactly on the start point; the last corner lands there to within rounding.
        pts[^1] = pts[0];

        var cumulative = new double[pts.Count];
        for (var i = 1; i < pts.Count; i++)
            cumulative[i] = cumulative[i - 1] + Distance(pts[i - 1], pts[i]);

        return new BorderPath([.. pts], cumulative);
    }


    /// <summary>The point at <paramref name="distance"/> along the lap. Any distance is accepted and wraps.</summary>
    public PointF PointAt(double distance)
    {
        var length = this.Length;
        if (length <= 0)
            return PointF.Zero;

        distance %= length;
        if (distance < 0)
            distance += length;

        // The last index whose distance is not past the target.
        var index = Array.BinarySearch(this.distances, distance);
        if (index < 0)
            index = ~index - 1;
        index = Math.Clamp(index, 0, this.points.Length - 2);

        var start = this.distances[index];
        var span = this.distances[index + 1] - start;
        var t = span <= 0 ? 0 : (distance - start) / span;

        var a = this.points[index];
        var b = this.points[index + 1];
        return new PointF((float)(a.X + ((b.X - a.X) * t)), (float)(a.Y + ((b.Y - a.Y) * t)));
    }


    /// <summary>One segment of the lap: its end points and the distance of its midpoint.</summary>
    public (PointF Start, PointF End, double Middle) Segment(int index)
        => (this.points[index], this.points[index + 1], (this.distances[index] + this.distances[index + 1]) / 2);


    static void Edge(List<PointF> pts, double x, double y, double step)
    {
        var from = pts[^1];
        var length = Math.Sqrt(((x - from.X) * (x - from.X)) + ((y - from.Y) * (y - from.Y)));
        var count = Math.Max(1, (int)Math.Ceiling(length / step));

        for (var i = 1; i <= count; i++)
        {
            var t = (double)i / count;
            pts.Add(new PointF((float)(from.X + ((x - from.X) * t)), (float)(from.Y + ((y - from.Y) * t))));
        }
    }


    static void Corner(List<PointF> pts, double cx, double cy, double radius, double fromAngle)
    {
        if (radius <= 0)
            return;

        var count = (int)Math.Ceiling(Math.PI / 2 / MaxCornerStepRadians);
        for (var i = 1; i <= count; i++)
        {
            var angle = fromAngle + (Math.PI / 2 * i / count);
            pts.Add(new PointF((float)(cx + (radius * Math.Cos(angle))), (float)(cy + (radius * Math.Sin(angle)))));
        }
    }


    static double Distance(PointF a, PointF b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
