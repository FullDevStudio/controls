using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shiny.Controls.Office.Shell;

namespace Shiny.Maui.Controls.Office;

/// <summary>
/// The MAUI <see cref="IOfficeDocumentStore"/>: files live in a folder of the app's own, Browse is the
/// platform file picker, and Print opens the PDF in the system viewer.
/// </summary>
/// <remarks>
/// <para>
/// The folder defaults to <c>Documents</c> under <see cref="FileSystem.AppDataDirectory"/>. A picked file
/// from anywhere else is copied in (under a name that overwrites nothing) and the copy is what the editor
/// saves back to — on iOS and Android the picker only ever hands over a temporary copy, so that is the
/// one behaviour that means the same thing everywhere. Subclass and override
/// <see cref="FileSystemOfficeDocumentStore.SaveAsync"/> or <see cref="PickOpenAsync"/> for anything else.
/// </para>
/// <para>
/// A head with no picker implementation (the AppKit and GTK heads) gets null from Browse rather than an
/// exception; the recent list still works there.
/// </para>
/// </remarks>
public class MauiOfficeDocumentStore(string? folder = null, TimeProvider? time = null)
    : FileSystemOfficeDocumentStore(folder ?? DefaultFolder, time)
{
    /// <summary><c>Documents</c> under the app's data directory.</summary>
    public static string DefaultFolder => Path.Combine(FileSystem.AppDataDirectory, "Documents");

    /// <inheritdoc />
    public override async Task<OfficeDocumentRef?> PickOpenAsync(OfficeApp app, CancellationToken cancellationToken = default)
    {
        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = $"Open a {OfficeAppInfo.For(app).DocumentNoun}",
                FileTypes = FileTypesFor(app)
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not every platform head has a picker implementation; a missing one is not a crash.
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] No file picker: {ex.Message}");
            return null;
        }

        if (file is null)
            return null;

        // Already one of ours (a desktop picker browsing the store's own folder): open it in place.
        if (!string.IsNullOrEmpty(file.FullPath) && File.Exists(file.FullPath) && IsInside(file.FullPath, this.Folder))
            return this.RefFor(file.FullPath);

        await using var source = await file.OpenReadAsync();
        return await this.ImportAsync(source, file.FileName, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>Print writes the PDF to the cache and opens it in the system viewer, which is where a phone prints from.</remarks>
    public override async Task<OfficeDocumentRef?> SaveAsync(OfficeDocumentRef? target, OfficeSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Action != OfficeFileAction.Print)
            return await base.SaveAsync(target, request, cancellationToken);

        var path = Path.Combine(FileSystem.CacheDirectory, request.FileName);
        await using (var file = File.Create(path))
            await request.WriteToAsync(file, cancellationToken);

        var printed = this.RefFor(path);
        await MainThread.InvokeOnMainThreadAsync(() => Launcher.Default.OpenAsync(
            new OpenFileRequest(request.FileName, new ReadOnlyFile(path, request.Format.MimeType))));

        return printed;
    }

    /// <summary>The picker filter for an app's own format, in each platform's vocabulary.</summary>
    static FilePickerFileType FileTypesFor(OfficeApp app)
    {
        var format = OfficeAppInfo.For(app).NativeFormat;
        var uti = app switch
        {
            OfficeApp.Excel => "org.openxmlformats.spreadsheetml.sheet",
            OfficeApp.PowerPoint => "org.openxmlformats.presentationml.presentation",
            OfficeApp.OneNote => "public.json",
            _ => "org.openxmlformats.wordprocessingml.document"
        };

        return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.iOS] = [uti],
            [DevicePlatform.MacCatalyst] = [uti],
            [DevicePlatform.macOS] = [uti, format.Extension.TrimStart('.')],
            [DevicePlatform.Android] = [format.MimeType],
            [DevicePlatform.WinUI] = [format.Extension]
        });
    }

    static bool IsInside(string path, string folder)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}


public static class OfficeDocumentStoreExtensions
{
    /// <summary>
    /// Gives every Office editor in the app a document store — <see cref="MauiOfficeDocumentStore"/> over
    /// <paramref name="folder"/> (its default when null). An editor's own <c>DocumentStore</c> still wins.
    /// </summary>
    public static MauiAppBuilder UseOfficeDocumentStore(this MauiAppBuilder builder, string? folder = null)
    {
        builder.Services.TryAddSingleton<IOfficeDocumentStore>(_ => new MauiOfficeDocumentStore(folder));
        return builder;
    }

    /// <summary>Gives every Office editor in the app <typeparamref name="TStore"/> as its document store.</summary>
    public static MauiAppBuilder UseOfficeDocumentStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(this MauiAppBuilder builder)
        where TStore : class, IOfficeDocumentStore
    {
        builder.Services.TryAddSingleton<IOfficeDocumentStore, TStore>();
        return builder;
    }

    static IOfficeDocumentStore? fallback;

    /// <summary>
    /// The view's own store, the app's registered one, or — with neither — one shared
    /// <see cref="MauiOfficeDocumentStore"/> over its default folder, so File works with no setup.
    /// </summary>
    internal static IOfficeDocumentStore ResolveDocumentStore(this Element view, IOfficeDocumentStore? own)
    {
        if (own is not null)
            return own;

        var services = (view as VisualElement)?.Handler?.MauiContext?.Services
                       ?? Application.Current?.Handler?.MauiContext?.Services
                       ?? IPlatformApplication.Current?.Services;

        return services?.GetService<IOfficeDocumentStore>()
               ?? (fallback ??= new MauiOfficeDocumentStore());
    }
}
