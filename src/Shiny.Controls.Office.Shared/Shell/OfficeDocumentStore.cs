namespace Shiny.Controls.Office.Shell;

/// <summary>
/// Where the Office editors' files live: what Open, Save, Save As, Export and the recent list mean.
/// </summary>
/// <remarks>
/// <para>
/// Hand one to an editor (its <c>DocumentStore</c> property, or register it with the host's DI) and the
/// File backstage works without a single event handler: Browse picks a file and the editor opens it,
/// a recent file reopens, Save writes back to where the file came from, Save As and Export write a new
/// one, and the recent list fills itself.
/// </para>
/// <para>
/// A store deals in files, never in documents. The editor turns its document into bytes in the
/// requested format (<see cref="OfficeSaveRequest.WriteToAsync"/>) and reads a document back from
/// <see cref="OpenReadAsync"/>, so one store serves the document, spreadsheet and slide editors alike.
/// </para>
/// <para>
/// An editor event the host handles still wins: a handled <c>SaveRequested</c> (or <c>FileRequested</c>,
/// <c>OpenRequested</c>, <c>RecentFileSelected</c>) is the host's, and the store is not asked.
/// </para>
/// </remarks>
public interface IOfficeDocumentStore
{
    /// <summary>Lets the user choose a file to open — a picker, a dialog. Null when they cancelled or there is nothing to choose with.</summary>
    Task<OfficeDocumentRef?> PickOpenAsync(OfficeApp app, CancellationToken cancellationToken = default);

    /// <summary>The bytes of a file this store handed out. The caller disposes the stream.</summary>
    Task<Stream> OpenReadAsync(OfficeDocumentRef document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a file. <paramref name="target"/> is the file to overwrite — the open document, on a plain
    /// Save — or null to write a new one (Save As, Export, Print, or the first Save of an untitled
    /// document), where the store may ask the user where it goes.
    /// </summary>
    /// <returns>The file written, or null when the user cancelled.</returns>
    Task<OfficeDocumentRef?> SaveAsync(OfficeDocumentRef? target, OfficeSaveRequest request, CancellationToken cancellationToken = default);

    /// <summary>The backstage's recent list for an app, newest first. Set <see cref="OfficeRecentFile.Document"/> so the editor can reopen one.</summary>
    Task<IReadOnlyList<OfficeRecentFile>> GetRecentAsync(OfficeApp app, CancellationToken cancellationToken = default);
}


/// <summary>A file an <see cref="IOfficeDocumentStore"/> knows about.</summary>
/// <param name="Id">The store's key for it — a path, a URL, a handle id. Opaque to the editors.</param>
/// <param name="Name">The file name with its extension — "Budget.xlsx".</param>
public sealed record OfficeDocumentRef(string Id, string Name)
{
    /// <summary>Where it is, for the title bar and the Info page — a folder, "OneDrive › Documents".</summary>
    public string? Location { get; init; }

    /// <summary>Anything the store wants back — a handle, a version tag.</summary>
    public object? Tag { get; init; }

    /// <summary>The name the editor shows: <see cref="Name"/> without its extension.</summary>
    public string DisplayName
    {
        get
        {
            var dot = this.Name.LastIndexOf('.');
            return dot > 0 ? this.Name[..dot] : this.Name;
        }
    }
}


/// <summary>What the user asked the File backstage (or the title bar) to do.</summary>
public enum OfficeFileAction
{
    /// <summary>Save — always the app's own format, back over the open file when there is one.</summary>
    Save,

    /// <summary>Save As, in the chosen format.</summary>
    SaveAs,

    /// <summary>Export, in the chosen format.</summary>
    Export,

    /// <summary>Print: a PDF to hand to a printer or viewer.</summary>
    Print
}


/// <summary>A file an editor wants an <see cref="IOfficeDocumentStore"/> to write.</summary>
public sealed class OfficeSaveRequest
{
    readonly Func<Stream, CancellationToken, Task> write;

    public OfficeSaveRequest(OfficeApp app, OfficeFileFormat format, string fileName, OfficeFileAction action, Func<Stream, CancellationToken, Task> write)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(write);

        this.App = app;
        this.Format = format;
        this.FileName = fileName;
        this.Action = action;
        this.write = write;
    }

    public OfficeApp App { get; }

    public OfficeFileFormat Format { get; }

    /// <summary>The document name with the format's extension — "Book1.xlsx", "Report.pdf".</summary>
    public string FileName { get; }

    public OfficeFileAction Action { get; }

    /// <summary>True when the result becomes the open document: a Save or Save As in the app's own format.</summary>
    public bool ReplacesDocument => this.Format.IsNative && this.Action is OfficeFileAction.Save or OfficeFileAction.SaveAs;

    /// <summary>Writes the file's bytes to <paramref name="destination"/>.</summary>
    public Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return this.write(destination, cancellationToken);
    }

    /// <summary>The file's bytes, for a store that hands them straight to a download or an upload.</summary>
    public async Task<byte[]> ToBytesAsync(CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await this.write(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}


/// <summary>
/// The part of an editor's File handling that does not depend on the host: which file is open, what a
/// Save overwrites, and opening one through the store. Every editor on both hosts keeps one.
/// </summary>
public sealed class OfficeDocumentSession(OfficeApp app)
{
    public OfficeApp App { get; } = app;

    /// <summary>The file the open document came from or was last saved as. Null for a new, untitled one.</summary>
    public OfficeDocumentRef? Current { get; set; }

    /// <summary>
    /// Writes a file through <paramref name="store"/>: a native Save over <see cref="Current"/>, anything
    /// else as a new file. A Save or Save As in the native format makes the result <see cref="Current"/>.
    /// </summary>
    /// <returns>The file written, or null when the user cancelled.</returns>
    public async Task<OfficeDocumentRef?> SaveAsync(
        IOfficeDocumentStore store,
        OfficeFileFormat format,
        OfficeFileAction action,
        string documentName,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        // Save on an untitled document, or one whose file is in another format, is a Save As.
        var target = action == OfficeFileAction.Save && format.IsNative && this.Current is { } current && HasExtension(current.Name, format)
            ? current
            : null;

        var request = new OfficeSaveRequest(this.App, format, format.FileNameFor(documentName), action, write);
        var written = await store.SaveAsync(target, request, cancellationToken).ConfigureAwait(false);

        if (written is not null && request.ReplacesDocument)
            this.Current = written;

        return written;
    }

    /// <summary>
    /// Opens <paramref name="document"/> — or, when it is null, the one the user picks — and loads it
    /// with <paramref name="load"/>. <see cref="Current"/> moves only once the load succeeded.
    /// </summary>
    /// <returns>The loaded document and its file, or null when the user cancelled the picker.</returns>
    public async Task<(T Document, OfficeDocumentRef File)?> OpenAsync<T>(
        IOfficeDocumentStore store,
        OfficeDocumentRef? document,
        Func<Stream, CancellationToken, Task<T>> load,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(load);

        document ??= await store.PickOpenAsync(this.App, cancellationToken).ConfigureAwait(false);
        if (document is null)
            return null;

        T loaded;
        await using (var stream = await store.OpenReadAsync(document, cancellationToken).ConfigureAwait(false))
            loaded = await load(stream, cancellationToken).ConfigureAwait(false);

        this.Current = document;
        return (loaded, document);
    }

    static bool HasExtension(string name, OfficeFileFormat format)
        => name.EndsWith(format.Extension, StringComparison.OrdinalIgnoreCase);
}
