using System.Text;
using Shiny.Controls.Office.Document;
using Shiny.Controls.Office.Shell;

namespace Shiny.Controls.Office.Skia;

/// <summary>
/// Writes a Word document out as the formats the Office shell's Save As and Export offer: the document
/// itself (<c>.docx</c>), a PDF, plain text or a web page.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="SpreadsheetExport"/> and <see cref="SlideExport"/>. Nothing here touches
/// a file system; each call writes to a stream the caller owns.
/// </remarks>
public static class DocumentExport
{
    /// <summary>Whether <paramref name="format"/> is one this class can write.</summary>
    public static bool CanWrite(OfficeFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return format.Id is "docx" or "pdf" or "txt" or "html";
    }

    /// <summary>Writes <paramref name="document"/> in <paramref name="format"/>.</summary>
    /// <param name="pdfOptions">The title, author and clock a PDF carries; also the web page's title.</param>
    /// <exception cref="NotSupportedException">The format is not docx, pdf, txt or html.</exception>
    public static async Task WriteAsync(WordDocument document, OfficeFileFormat format, Stream destination, DocumentPdfOptions? pdfOptions = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(destination);

        switch (format.Id)
        {
            case "docx":
                await document.SaveToAsync(destination, cancellationToken).ConfigureAwait(false);
                break;

            case "pdf":
                DocumentPdfExporter.Export(document, destination, pdfOptions);
                break;

            case "txt":
                await destination.WriteAsync(Encoding.UTF8.GetBytes(document.PlainText), cancellationToken).ConfigureAwait(false);
                break;

            case "html":
                await destination.WriteAsync(Encoding.UTF8.GetBytes(WordShell.ToHtml(document, pdfOptions?.Title)), cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new NotSupportedException($"A document cannot be written as {format.Name}.");
        }
    }

    /// <summary>The bytes of <see cref="WriteAsync"/>, for a host that hands them straight to a download.</summary>
    public static async Task<byte[]> ToBytesAsync(WordDocument document, OfficeFileFormat format, DocumentPdfOptions? pdfOptions = null, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await WriteAsync(document, format, buffer, pdfOptions, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
