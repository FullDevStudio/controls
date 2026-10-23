namespace Shiny.Maui.Controls.Camera.Video;

/// <summary>
/// One 8-bit image plane in native memory, described the way Android's <c>Image.Plane</c> and CameraX's
/// <c>ImageProxy.PlaneProxy</c> describe theirs: a base pointer, the bytes between rows, and the bytes between
/// neighbouring samples in a row (2 for the interleaved chroma of NV12 / NV21).
/// </summary>
readonly unsafe struct YuvPlane(byte* data, int rowStride, int pixelStride)
{
    public byte* Data { get; } = data;
    public int RowStride { get; } = rowStride;
    public int PixelStride { get; } = pixelStride;
}


/// <summary>
/// Scales 8-bit planes from one strided layout to another in a single pass, reading the source in place.
/// </summary>
/// <remarks>
/// <para>
/// This is how Android gets a camera frame into an encoder without an intermediate copy: the source is the
/// CameraX <c>ImageProxy</c>'s planes and the destination is MediaCodec's input <c>Image</c> (or the NV21
/// buffer <c>YuvImage</c> compresses), both direct buffers addressed by pointer. Because every plane carries
/// its own row and pixel stride, one routine covers I420, NV12, NV21 and padded rows alike, on either side.
/// </para>
/// <para>
/// Downscaling averages a box of source pixels per output pixel (capped at 4 x 4 samples), which is what keeps
/// a 640 → 160 BLE-profile frame from shimmering the way nearest-neighbour sampling does. Upscaling (never
/// asked for by the encoder, but legal) degenerates to nearest-neighbour.
/// </para>
/// </remarks>
static unsafe class YuvScaler
{
    const int MaxSamplesPerAxis = 4;

    /// <summary>Scale <paramref name="source"/> (<paramref name="sourceWidth"/> x <paramref name="sourceHeight"/>) into <paramref name="destination"/>.</summary>
    public static void ScalePlane(
        YuvPlane source, int sourceWidth, int sourceHeight,
        YuvPlane destination, int destinationWidth, int destinationHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0)
            return;

        // column boxes are the same for every row, so work them out once
        Span<int> starts = destinationWidth <= 1024 ? stackalloc int[destinationWidth] : new int[destinationWidth];
        Span<int> ends = destinationWidth <= 1024 ? stackalloc int[destinationWidth] : new int[destinationWidth];
        for (var x = 0; x < destinationWidth; x++)
            (starts[x], ends[x]) = Box(x, destinationWidth, sourceWidth);

        var sp = source.PixelStride;
        for (var y = 0; y < destinationHeight; y++)
        {
            var (y0, y1) = Box(y, destinationHeight, sourceHeight);
            var yStep = Math.Max(1, (y1 - y0) / MaxSamplesPerAxis);
            var dstRow = destination.Data + (long)y * destination.RowStride;

            for (var x = 0; x < destinationWidth; x++)
            {
                int x0 = starts[x], x1 = ends[x];
                var xStep = Math.Max(1, (x1 - x0) / MaxSamplesPerAxis);
                int sum = 0, count = 0;
                for (var sy = y0; sy < y1; sy += yStep)
                {
                    var srcRow = source.Data + (long)sy * source.RowStride;
                    for (var sx = x0; sx < x1; sx += xStep)
                    {
                        sum += srcRow[sx * sp];
                        count++;
                    }
                }
                dstRow[x * destination.PixelStride] = (byte)((sum + count / 2) / count);
            }
        }
    }

    // The source span [start, end) that output index i of n covers; at least one pixel wide.
    static (int Start, int End) Box(int i, int n, int extent)
    {
        var start = (int)((long)i * extent / n);
        var end = (int)((long)(i + 1) * extent / n);
        start = Math.Min(start, extent - 1);
        return (start, Math.Max(start + 1, Math.Min(end, extent)));
    }
}
