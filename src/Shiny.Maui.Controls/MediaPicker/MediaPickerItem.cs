namespace Shiny.Maui.Controls.Media;

/// <summary>
/// A single photo held by a <see cref="MediaPickerButton"/>. The bytes are already
/// compressed/converted to the button's <see cref="MediaPickerButton.OutputFormat"/>.
/// </summary>
/// <param name="Data">Encoded image bytes in <paramref name="ContentType"/>.</param>
/// <param name="Width">Pixel width of the (possibly resized) image.</param>
/// <param name="Height">Pixel height of the (possibly resized) image.</param>
/// <param name="ContentType">MIME type of <paramref name="Data"/> (e.g. <c>image/jpeg</c>).</param>
public record MediaPickerItem(byte[] Data, int Width, int Height, string ContentType)
{
    /// <summary>Open a fresh read-only stream over the encoded bytes.</summary>
    public Stream OpenRead() => new MemoryStream(this.Data, false);

    /// <summary>An <see cref="ImageSource"/> for binding/display; yields a new stream each load.</summary>
    public ImageSource Thumbnail => ImageSource.FromStream(() => new MemoryStream(this.Data, false));

    /// <summary>
    /// False keeps the photo in the picker: no remove button on its thumbnail. For a photo the
    /// screen loaded itself — one already saved — rather than one the user just picked.
    /// </summary>
    public bool CanRemove { get; init; } = true;
}
