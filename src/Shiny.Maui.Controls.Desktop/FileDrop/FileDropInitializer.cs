using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Dispatching;

namespace Shiny.Maui.Controls.Desktop.FileDrop;

/// <summary>
/// Attaches the drop target to application windows as they open.
/// </summary>
/// <remarks>
/// <para>
/// There is no public "a window was added" event on <see cref="Application"/>, and the
/// <c>WindowHandler.Mapper</c> hook that would normally stand in for one never runs on the AppKit
/// and GTK4 heads — they ship their own handler types. So this does the only two things that work
/// everywhere: it polls briefly at startup until the first window exists, then switches to
/// <see cref="Application.PageAppearing"/>, which fires whenever a window shows a page and so
/// catches every window opened later.
/// </para>
/// <para>
/// Attaching twice is harmless — <see cref="FileDropService.AttachOpenWindows"/> skips windows it
/// already holds — so the belt-and-braces is cheap.
/// </para>
/// </remarks>
sealed class FileDropInitializer : IMauiInitializeService
{
    /// <summary>How long to wait for the first window before giving up on the startup poll.</summary>
    internal static TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(30);

    static readonly TimeSpan pollInterval = TimeSpan.FromMilliseconds(250);

    public void Initialize(IServiceProvider services)
    {
        var options = services.GetRequiredService<FileDropOptions>();
        if (!options.AutoAttachWindows)
            return;

        if (services.GetRequiredService<IFileDropService>() is not FileDropService service || !service.IsSupported)
            return;

        // Initialize services run while the MauiApp is being built, before Application.Current
        // exists. Nothing here can be done synchronously.
        var time = services.GetService<TimeProvider>() ?? TimeProvider.System;
        _ = WatchAsync(service, Dispatcher.GetForCurrentThread(), time);
    }

    static async Task WatchAsync(FileDropService service, IDispatcher? dispatcher, TimeProvider time)
    {
        var hooked = false;
        var started = time.GetTimestamp();

        while (time.GetElapsedTime(started) < StartupTimeout)
        {
            var settled = await InvokeAsync(dispatcher, () =>
            {
                var app = Application.Current;
                if (app == null)
                    return false;

                if (!hooked)
                {
                    hooked = true;
                    app.PageAppearing += (_, _) => service.AttachOpenWindows();
                }

                service.AttachOpenWindows();
                return app.Windows.Count > 0;
            }).ConfigureAwait(false);

            // Once a window exists, PageAppearing takes over and the poll has nothing left to do.
            if (settled)
                return;

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        service.LogDebug("No application window appeared before the file drop startup timeout — call IFileDropService.AttachTo yourself.");
    }

    static Task<bool> InvokeAsync(IDispatcher? dispatcher, Func<bool> work)
    {
        if (dispatcher == null || !dispatcher.IsDispatchRequired)
            return Task.FromResult(work());

        var source = new TaskCompletionSource<bool>();
        dispatcher.Dispatch(() =>
        {
            try
            {
                source.TrySetResult(work());
            }
            catch (Exception ex)
            {
                source.TrySetException(ex);
            }
        });
        return source.Task;
    }
}
