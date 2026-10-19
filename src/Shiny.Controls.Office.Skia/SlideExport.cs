using System.IO.Compression;
using Shiny.Controls.Office.Presentation;
using Shiny.Controls.Office.Shell;
using Shiny.Controls.Office.Theming;
using SkiaSharp;

namespace Shiny.Controls.Office.Skia;

/// <summary>
/// Writes a deck out as the formats the Office shell's Save As and Export offer: the deck itself
/// (<c>.pptx</c>), a PDF with a page per slide, the current slide as a PNG or JPEG, or every slide as
/// PNGs in a <c>.zip</c>.
/// </summary>
/// <remarks>
/// Nothing here touches a file system. Each call writes to a stream the host owns, so the same code
/// serves a MAUI app writing to disk and a browser handing the bytes to a download.
/// </remarks>
public static class SlideExport
{
    /// <summary>Every slide as a PNG, packed in a <c>.zip</c> — Export ▸ "PNG (all slides)".</summary>
    public static readonly OfficeFileFormat AllSlidesPng = new(
        "png-all", "PNG Images (all slides)", ".zip", "Every slide as a picture, in one .zip archive.",
        "application/zip");

    /// <summary>Save As: the deck, a PDF, or the current slide as a picture.</summary>
    public static IReadOnlyList<OfficeFileFormat> SaveAsFormats { get; } = [OfficeFileFormats.Pptx, OfficeFileFormats.Pdf, OfficeFileFormats.Png];

    /// <summary>Export: PDF, the current slide as PNG or JPEG, or every slide as PNGs in a zip.</summary>
    public static IReadOnlyList<OfficeFileFormat> ExportFormats { get; } = [OfficeFileFormats.Pdf, OfficeFileFormats.Png, AllSlidesPng, OfficeFileFormats.Jpeg];

    /// <summary>The pixel width pictures are exported at — 1920, a 1080p slide.</summary>
    public const int PictureWidth = 1920;

    /// <summary>Whether <paramref name="format"/> is one this class can write.</summary>
    public static bool CanWrite(OfficeFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return format.Id is "pptx" or "pdf" or "png" or "jpg" or "png-all";
    }

    /// <summary>Writes the deck (or, for a picture, <paramref name="slideIndex"/>) in <paramref name="format"/>.</summary>
    /// <exception cref="NotSupportedException">The format is not one <see cref="CanWrite"/> accepts.</exception>
    public static async Task WriteAsync(SlideDeck deck, int slideIndex, OfficeFileFormat format, Stream destination, OfficeWatermark? watermark = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(destination);

        switch (format.Id)
        {
            case "pptx":
                await deck.SaveToAsync(destination, cancellationToken).ConfigureAwait(false);
                break;

            case "pdf":
                SlideExporter.ToPdf(deck, destination, watermark: watermark);
                break;

            case "png":
            {
                var png = SlideExporter.ToPng(deck, Clamp(deck, slideIndex), PictureWidth, watermark);
                await destination.WriteAsync(png, cancellationToken).ConfigureAwait(false);
                break;
            }

            case "jpg":
            {
                var png = SlideExporter.ToPng(deck, Clamp(deck, slideIndex), PictureWidth, watermark);
                using var bitmap = SKBitmap.Decode(png);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                await destination.WriteAsync(data.ToArray(), cancellationToken).ConfigureAwait(false);
                break;
            }

            case "png-all":
                WriteZip(deck, destination, watermark);
                break;

            default:
                throw new NotSupportedException($"A presentation cannot be written as {format.Name}.");
        }
    }

    /// <summary>The bytes of <see cref="WriteAsync"/>, for a host that hands them straight to a download.</summary>
    public static async Task<byte[]> ToBytesAsync(SlideDeck deck, int slideIndex, OfficeFileFormat format, OfficeWatermark? watermark = null, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await WriteAsync(deck, slideIndex, format, buffer, watermark, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// Every slide as <c>Slide1.png</c>, <c>Slide2.png</c> … in a zip — PowerPoint's own names when it
    /// exports all slides to a folder.
    /// </summary>
    public static void WriteZip(SlideDeck deck, Stream destination, OfficeWatermark? watermark = null, int width = PictureWidth)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(destination);

        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        for (var i = 0; i < deck.Slides.Count; i++)
        {
            var png = SlideExporter.ToPng(deck, i, width, watermark);

            // Already compressed: deflating a PNG again costs time and saves nothing.
            var entry = zip.CreateEntry($"Slide{i + 1}.png", CompressionLevel.NoCompression);
            using var stream = entry.Open();
            stream.Write(png);
        }
    }

    static int Clamp(SlideDeck deck, int slideIndex)
    {
        if (deck.Slides.Count == 0)
            throw new InvalidOperationException("The presentation has no slides to export.");

        return Math.Clamp(slideIndex, 0, deck.Slides.Count - 1);
    }
}
