namespace Shiny.Maui.Controls.Camera.Video;

/// <summary>The size math for live video: what to encode a frame at, and how to draw a received one.</summary>
static class VideoScaling
{
    /// <summary>
    /// The size to encode a <paramref name="width"/> x <paramref name="height"/> frame at, so its long edge is
    /// at most <paramref name="maxDimension"/>. Never scales up, keeps the aspect ratio as closely as the
    /// alignment allows, and rounds both edges down to a multiple of <paramref name="alignment"/>.
    /// </summary>
    /// <remarks>
    /// H.264's 4:2:0 chroma needs even dimensions, which is why the default alignment is 2. Rounding is down,
    /// so the result never exceeds <paramref name="maxDimension"/>, and never below one alignment step, so a
    /// sliver of a frame still encodes.
    /// </remarks>
    /// <param name="width">Source width in pixels.</param>
    /// <param name="height">Source height in pixels.</param>
    /// <param name="maxDimension">The long-edge limit; zero or less means no limit.</param>
    /// <param name="alignment">Both edges are rounded down to a multiple of this.</param>
    public static (int Width, int Height) Fit(int width, int height, int maxDimension, int alignment = 2)
    {
        if (width <= 0 || height <= 0)
            return (0, 0);

        alignment = Math.Max(1, alignment);
        double w = width, h = height;
        var longEdge = Math.Max(width, height);
        if (maxDimension > 0 && longEdge > maxDimension)
        {
            var scale = (double)maxDimension / longEdge;
            w = width * scale;
            h = height * scale;
        }

        return (Align(w, alignment), Align(h, alignment));
    }

    static int Align(double value, int alignment)
    {
        // a hair of tolerance so 359.99999 from floating point lands on 360, not 358
        var whole = (int)Math.Floor(value + 1e-6);
        return Math.Max(alignment, whole / alignment * alignment);
    }

    /// <summary>
    /// How to draw an un-rotated <paramref name="imageWidth"/> x <paramref name="imageHeight"/> frame into a
    /// view: rotate it clockwise by <paramref name="rotation"/>, then scale the rotated image by
    /// (<c>ScaleX</c>, <c>ScaleY</c>) about the view's centre.
    /// </summary>
    /// <remarks>
    /// The scale is expressed in the rotated (upright) space, which is what lets one answer cover every
    /// <see cref="Aspect"/>: <see cref="Aspect.AspectFit"/> and <see cref="Aspect.Center"/> letterbox,
    /// <see cref="Aspect.AspectFill"/> covers and crops, and <see cref="Aspect.Fill"/> stretches each axis to
    /// the view. Each platform turns this into its own transform (a CALayer affine transform, an Android
    /// <c>Matrix</c>, a XAML <c>CompositeTransform</c>).
    /// </remarks>
    public static (double ScaleX, double ScaleY, int Rotation) Layout(
        int imageWidth, int imageHeight, int rotation, double viewWidth, double viewHeight, Aspect aspect)
    {
        var r = NormalizeRotation(rotation);
        if (imageWidth <= 0 || imageHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
            return (0, 0, r);

        // the image's footprint once upright
        var quarterTurn = r is 90 or 270;
        double uprightW = quarterTurn ? imageHeight : imageWidth;
        double uprightH = quarterTurn ? imageWidth : imageHeight;

        var sx = viewWidth / uprightW;
        var sy = viewHeight / uprightH;
        return aspect switch
        {
            Aspect.Fill => (sx, sy, r),
            Aspect.AspectFill => (Math.Max(sx, sy), Math.Max(sx, sy), r),
            _ => (Math.Min(sx, sy), Math.Min(sx, sy), r)
        };
    }

    /// <summary>Snap a rotation to 0, 90, 180 or 270 clockwise; anything else reads as upright.</summary>
    public static int NormalizeRotation(int degrees)
    {
        var r = degrees % 360;
        if (r < 0)
            r += 360;
        return r is 90 or 180 or 270 ? r : 0;
    }
}
