using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiny.Controls.Office.Shell;

/// <summary>
/// An <see cref="IOfficeDocumentStore"/> over one folder: files are saved into it, opened from it, and
/// the recent list is kept beside them in a small index.
/// </summary>
/// <remarks>
/// <para>
/// Plain <see cref="System.IO"/>, so it works anywhere .NET has a file system. A save goes to a sibling
/// temporary file that is moved into place, so an interrupted save never leaves a half-written file.
/// A new file (Save As, Export, the first Save) never overwrites one already there: it becomes
/// "Report (2).docx" instead.
/// </para>
/// <para>
/// <see cref="PickOpenAsync"/> has nothing to pick with and returns null; a host subclass overrides it
/// with the platform's picker and brings the chosen file in with <see cref="ImportAsync"/>. The MAUI
/// package's <c>MauiOfficeDocumentStore</c> is exactly that.
/// </para>
/// </remarks>
public class FileSystemOfficeDocumentStore : IOfficeDocumentStore
{
    /// <summary>The index's file name, inside <see cref="Folder"/>.</summary>
    public const string IndexFileName = ".recent.json";

    readonly SemaphoreSlim gate = new(1, 1);
    readonly TimeProvider time;

    public FileSystemOfficeDocumentStore(string folder, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        this.Folder = Path.GetFullPath(folder);
        this.time = time ?? TimeProvider.System;
    }

    /// <summary>Where files are saved, and what the recent list's Location says.</summary>
    public string Folder { get; }

    /// <summary>How many files the recent list keeps. 20 by default.</summary>
    public int MaxRecentFiles { get; init; } = 20;

    /// <summary>A file in <see cref="Folder"/> (or anywhere, by full path) as a reference the editors can open.</summary>
    public OfficeDocumentRef RefFor(string path)
    {
        var full = Path.GetFullPath(path);
        return new OfficeDocumentRef(full, Path.GetFileName(full)) { Location = Path.GetDirectoryName(full) };
    }

    /// <inheritdoc />
    /// <remarks>Nothing to pick with: null. Override with the platform's picker.</remarks>
    public virtual Task<OfficeDocumentRef?> PickOpenAsync(OfficeApp app, CancellationToken cancellationToken = default)
        => Task.FromResult<OfficeDocumentRef?>(null);

    /// <inheritdoc />
    public virtual async Task<Stream> OpenReadAsync(OfficeDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var stream = new FileStream(document.Id, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        try
        {
            await this.TouchAsync(document.Id, null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Never handed out, so nobody else will close it - and an open handle blocks the next save over it.
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        return stream;
    }

    /// <inheritdoc />
    public virtual async Task<OfficeDocumentRef?> SaveAsync(OfficeDocumentRef? target, OfficeSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(this.Folder);

        var path = target?.Id ?? this.UniquePath(request.FileName);
        var temp = path + ".saving";

        try
        {
            await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await request.WriteToAsync(file, cancellationToken).ConfigureAwait(false);
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }

        // Only the app's own files belong in the recent list - an exported PDF is not something to reopen.
        if (request.Format.IsNative)
            await this.TouchAsync(path, request.App, cancellationToken).ConfigureAwait(false);

        return this.RefFor(path);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<OfficeRecentFile>> GetRecentAsync(OfficeApp app, CancellationToken cancellationToken = default)
    {
        var entries = await this.ReadIndexAsync(cancellationToken).ConfigureAwait(false);

        return entries
            .Where(x => AppFor(x) == app && File.Exists(x.Path))
            .OrderByDescending(x => x.LastOpened)
            .Select(x =>
            {
                var document = this.RefFor(x.Path);
                return new OfficeRecentFile(document.Name, document.Location, x.LastOpened)
                {
                    App = app,
                    Document = document
                };
            })
            .ToList();
    }

    /// <summary>
    /// Copies a file from elsewhere — a picker's result, an attachment — into <see cref="Folder"/>, under
    /// a name that does not overwrite anything, and returns it as the file to open.
    /// </summary>
    protected async Task<OfficeDocumentRef> ImportAsync(Stream source, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        Directory.CreateDirectory(this.Folder);

        var path = this.UniquePath(fileName);
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

        return this.RefFor(path);
    }

    /// <summary>A path in <see cref="Folder"/> for <paramref name="fileName"/> that is not taken: "Report.docx", then "Report (2).docx".</summary>
    protected string UniquePath(string fileName)
    {
        var safe = SafeFileName(fileName);
        var stem = Path.GetFileNameWithoutExtension(safe);
        var extension = Path.GetExtension(safe);

        var path = Path.Combine(this.Folder, safe);
        for (var n = 2; File.Exists(path); n++)
            path = Path.Combine(this.Folder, $"{stem} ({n}){extension}");

        return path;
    }

    /// <summary>The name alone, with anything a file system would refuse replaced — a renamed document can say "Q1/Q2".</summary>
    static string SafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(fileName.Select(c => invalid.Contains(c) || c is '/' or '\\' or ':' ? '-' : c).ToArray()).Trim().TrimStart('.');
        return name.Length == 0 || Path.GetFileNameWithoutExtension(name).Length == 0 ? "Document" + Path.GetExtension(name) : name;
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // The save already failed; a leftover temp file is not worth masking that with.
        }
    }


    // ---- recent index ----

    string IndexPath => Path.Combine(this.Folder, IndexFileName);

    static OfficeApp? AppFor(RecentEntry entry)
    {
        if (Enum.TryParse<OfficeApp>(entry.App, out var app))
            return app;

        var extension = Path.GetExtension(entry.Path);
        foreach (var candidate in Enum.GetValues<OfficeApp>())
        {
            if (string.Equals(OfficeAppInfo.For(candidate).NativeFormat.Extension, extension, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    async Task TouchAsync(string path, OfficeApp? app, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await this.ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false);
            var full = Path.GetFullPath(path);
            var existing = entries.FirstOrDefault(x => string.Equals(x.Path, full, StringComparison.Ordinal));

            if (existing is not null)
                entries.Remove(existing);

            entries.Insert(0, new RecentEntry
            {
                Path = full,
                App = app?.ToString() ?? existing?.App,
                LastOpened = this.time.GetUtcNow()
            });

            if (entries.Count > this.MaxRecentFiles)
                entries.RemoveRange(this.MaxRecentFiles, entries.Count - this.MaxRecentFiles);

            Directory.CreateDirectory(this.Folder);
            await using var file = new FileStream(this.IndexPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(file, entries, RecentIndexJsonContext.Default.ListRecentEntry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The recent list is a convenience; a read-only folder must not fail the open or save it rides on.
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] Could not update the recent list: {ex.Message}");
        }
        finally
        {
            this.gate.Release();
        }
    }

    async Task<List<RecentEntry>> ReadIndexAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ReadIndexCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    async Task<List<RecentEntry>> ReadIndexCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(this.IndexPath))
            return [];

        try
        {
            await using var file = new FileStream(this.IndexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return await JsonSerializer.DeserializeAsync(file, RecentIndexJsonContext.Default.ListRecentEntry, cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (JsonException)
        {
            // A corrupt index starts the list again rather than breaking every open and save.
            return [];
        }
    }
}


sealed class RecentEntry
{
    public string Path { get; set; } = string.Empty;
    public string? App { get; set; }
    public DateTimeOffset LastOpened { get; set; }
}


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<RecentEntry>))]
partial class RecentIndexJsonContext : JsonSerializerContext;
