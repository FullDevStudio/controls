using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
using Shiny.Controls.Office.Shell;

namespace Shiny.Blazor.Controls.Office;

/// <summary>
/// The browser's <see cref="IOfficeDocumentStore"/>: real files where the browser allows it, downloads
/// where it does not.
/// </summary>
/// <remarks>
/// <para>
/// On Chromium browsers (the File System Access API) Browse is the system open dialog, Save writes back
/// over the file it was opened from, Save As and Export are the system save dialog, and the recent list
/// reopens files after a reload — the browser asks once per session for permission to touch a file again.
/// </para>
/// <para>
/// On Safari and Firefox Browse is a file input and every save is a download. Files opened or saved
/// there stay in the recent list only for the life of the page.
/// </para>
/// <para>
/// The recent list lives in this browser's local storage, so it is the visitor's own on Blazor Server
/// as well as WebAssembly. Register it Scoped (<see cref="OfficeServiceCollectionExtensions.AddShinyOfficeDocumentStore(IServiceCollection)"/>).
/// </para>
/// </remarks>
public sealed class BrowserOfficeDocumentStore(IJSRuntime js) : IOfficeDocumentStore, IAsyncDisposable
{
    /// <summary>The largest file Browse will read, in bytes. 100 MB by default.</summary>
    public long MaxFileSize { get; init; } = 100 * 1024 * 1024;

    // The task, not the reference: two calls racing the first import share one, so none is left undisposed.
    Task<IJSObjectReference>? module;

    async Task<IJSObjectReference> ModuleAsync()
    {
        var pending = this.module ??= js.InvokeAsync<IJSObjectReference>("import", "./_content/Shiny.Blazor.Controls.Office/officeFiles.js").AsTask();
        try
        {
            return await pending;
        }
        catch
        {
            // A failed import is not cached: the next call tries again.
            if (ReferenceEquals(this.module, pending))
                this.module = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<OfficeDocumentRef?> PickOpenAsync(OfficeApp app, CancellationToken cancellationToken = default)
    {
        var format = OfficeAppInfo.For(app).NativeFormat;
        var module = await this.ModuleAsync();
        var json = await module.InvokeAsync<string?>("pickOpen", cancellationToken, format.Name, format.MimeType, format.Extension);
        return Parse(json);
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(OfficeDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var module = await this.ModuleAsync();

        await using var blob = await module.InvokeAsync<IJSStreamReference>("read", cancellationToken, document.Id);
        await using var source = await blob.OpenReadStreamAsync(this.MaxFileSize, cancellationToken);

        // The JS stream is only good while its reference is; the document readers want it all anyway.
        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        if (AppForName(document.Name) is { } app)
            await module.InvokeVoidAsync("touch", cancellationToken, document.Id, document.Name, app.ToString());

        return buffer;
    }

    /// <inheritdoc />
    public async Task<OfficeDocumentRef?> SaveAsync(OfficeDocumentRef? target, OfficeSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var module = await this.ModuleAsync();

        using var buffer = new MemoryStream();
        await request.WriteToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var stream = new DotNetStreamReference(buffer, leaveOpen: true);
        var format = request.Format;
        var json = await module.InvokeAsync<string?>("save", cancellationToken, target?.Id, request.FileName, format.Name, format.MimeType, format.Extension, stream);
        if (Parse(json) is not { } written)
            return null;

        if (format.IsNative)
            await module.InvokeVoidAsync("touch", cancellationToken, written.Id, written.Name, request.App.ToString());

        return written;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OfficeRecentFile>> GetRecentAsync(OfficeApp app, CancellationToken cancellationToken = default)
    {
        var module = await this.ModuleAsync();
        var json = await module.InvokeAsync<string>("recent", cancellationToken, app.ToString());
        var entries = JsonSerializer.Deserialize(json, BrowserFilesJsonContext.Default.ListBrowserRecentDto) ?? [];

        return entries
            .Select(x => new OfficeRecentFile(x.Name, "This browser", x.LastOpened)
            {
                App = app,
                Document = new OfficeDocumentRef(x.Id, x.Name) { Location = "This browser" }
            })
            .ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (this.module is { } m)
        {
            try
            {
                await (await m).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone; so is the module.
            }
            catch (JSException)
            {
                // The import itself failed; there is nothing to release.
            }
            catch (TaskCanceledException)
            {
            }
        }
    }

    static OfficeDocumentRef? Parse(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var dto = JsonSerializer.Deserialize(json, BrowserFilesJsonContext.Default.BrowserFileDto);
        return dto is null ? null : new OfficeDocumentRef(dto.Id, dto.Name) { Location = "This browser" };
    }

    static OfficeApp? AppForName(string name)
    {
        foreach (var app in Enum.GetValues<OfficeApp>())
        {
            if (name.EndsWith(OfficeAppInfo.For(app).NativeFormat.Extension, StringComparison.OrdinalIgnoreCase))
                return app;
        }

        return null;
    }
}


sealed class BrowserFileDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}


sealed class BrowserRecentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset LastOpened { get; set; }
}


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserFileDto))]
[JsonSerializable(typeof(List<BrowserRecentDto>))]
partial class BrowserFilesJsonContext : JsonSerializerContext;


public static class OfficeServiceCollectionExtensions
{
    /// <summary>
    /// Gives every Office editor the browser's document store (<see cref="BrowserOfficeDocumentStore"/>).
    /// An editor's own <c>DocumentStore</c> parameter still wins.
    /// </summary>
    /// <remarks>Scoped: on Blazor Server a singleton would share one visitor's JS runtime with everyone.</remarks>
    public static IServiceCollection AddShinyOfficeDocumentStore(this IServiceCollection services)
    {
        services.TryAddScoped<IOfficeDocumentStore, BrowserOfficeDocumentStore>();
        return services;
    }

    /// <summary>Gives every Office editor <typeparamref name="TStore"/> as its document store, Scoped.</summary>
    public static IServiceCollection AddShinyOfficeDocumentStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(this IServiceCollection services)
        where TStore : class, IOfficeDocumentStore
    {
        services.TryAddScoped<IOfficeDocumentStore, TStore>();
        return services;
    }
}
