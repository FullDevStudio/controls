using System.Runtime.CompilerServices;

namespace Shiny.Blazor.Controls.Images;

/// <summary>
/// One download per URI through an <see cref="IImageDownloader"/>, however many images ask at once.
/// </summary>
/// <remarks>
/// <para>Attached to the downloader instance rather than registered, so it works with whatever an app
/// has registered and needs no setup. The downloader is scoped, so on Blazor Server this is one per
/// circuit: two users never share an authenticated image.</para>
///
/// <para>The download runs on a token of its own, not the first caller's; each caller only stops
/// waiting on its own token. One image leaving the screen must not cancel the download the
/// other eleven are showing a ring for.</para>
///
/// <para>When the last one stops waiting, though, nobody wants the bytes: the download is cancelled and
/// its entry dropped, so a request that never answers does not sit in the table for the life of the
/// circuit.</para>
/// </remarks>
sealed class SharedImageDownloads
{
    static readonly ConditionalWeakTable<IImageDownloader, SharedImageDownloads> PerDownloader = new();

    sealed class InFlight
    {
        readonly List<IProgress<ImageLoadProgress>> subscribers = [];
        readonly Lock gate = new();

        public required Task<ImageDownloadResult> Task { get; init; }
        public CancellationTokenSource Abandoned { get; } = new();
        public ImageLoadProgress? Latest { get; private set; }

        /// <summary>Callers still awaiting it. Guarded by the owner's gate, not this entry's.</summary>
        public int Waiters { get; set; }

        public void Subscribe(IProgress<ImageLoadProgress>? progress)
        {
            if (progress is null)
                return;

            ImageLoadProgress? snapshot;
            lock (this.gate)
            {
                this.subscribers.Add(progress);
                snapshot = this.Latest;
            }

            if (snapshot is not null)
                progress.Report(snapshot);
        }

        public void Unsubscribe(IProgress<ImageLoadProgress>? progress)
        {
            if (progress is null)
                return;

            lock (this.gate)
                this.subscribers.Remove(progress);
        }

        public void Report(ImageLoadProgress value)
        {
            IProgress<ImageLoadProgress>[] targets;
            lock (this.gate)
            {
                this.Latest = value;
                targets = [.. this.subscribers];
            }

            foreach (var target in targets)
                target.Report(value);
        }
    }

    readonly Dictionary<string, InFlight> inFlight = new(StringComparer.Ordinal);
    readonly Lock gate = new();
    readonly IImageDownloader downloader;

    SharedImageDownloads(IImageDownloader downloader) => this.downloader = downloader;


    public static SharedImageDownloads For(IImageDownloader downloader)
        => PerDownloader.GetValue(downloader, d => new SharedImageDownloads(d));


    public async Task<ImageDownloadResult> DownloadAsync(
        string uri,
        IProgress<ImageLoadProgress>? progress,
        CancellationToken cancellationToken
    )
    {
        InFlight entry;

        lock (this.gate)
        {
            if (!this.inFlight.TryGetValue(uri, out entry!))
            {
                var tcs = new TaskCompletionSource<ImageDownloadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                entry = new InFlight { Task = tcs.Task };
                this.inFlight[uri] = entry;

                _ = this.RunAsync(uri, entry, tcs);
            }
            entry.Waiters++;
        }

        entry.Subscribe(progress);

        try
        {
            return await entry.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            entry.Unsubscribe(progress);
            this.Leave(uri, entry);
        }
    }


    void Leave(string uri, InFlight entry)
    {
        lock (this.gate)
        {
            if (--entry.Waiters > 0 || entry.Task.IsCompleted)
                return;

            if (this.inFlight.TryGetValue(uri, out var current) && ReferenceEquals(current, entry))
                this.inFlight.Remove(uri);
        }

        // Outside the lock: cancellation callbacks run inline, and the download's finally takes it.
        try
        {
            entry.Abandoned.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // It finished in between.
        }
    }


    async Task RunAsync(string uri, InFlight entry, TaskCompletionSource<ImageDownloadResult> tcs)
    {
        try
        {
            // Off the caller's stack first: it was started under the lock, and a downloader that
            // completes synchronously would otherwise run its finally - which takes that lock - there.
            await Task.Yield();

            var result = await this.downloader.DownloadAsync(uri, new ForwardingProgress(entry), entry.Abandoned.Token).ConfigureAwait(false);
            tcs.TrySetResult(result);
        }
        catch (OperationCanceledException) when (entry.Abandoned.IsCancellationRequested)
        {
            // Nobody is waiting; a faulted task nobody observes would only surface as UnobservedTaskException.
            tcs.TrySetCanceled();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            lock (this.gate)
            {
                if (this.inFlight.TryGetValue(uri, out var current) && ReferenceEquals(current, entry))
                    this.inFlight.Remove(uri);
            }
            entry.Abandoned.Dispose();
        }
    }


    /// <summary>
    /// Reports straight through rather than via <see cref="Progress{T}"/>, which posts to a captured
    /// context - on a download nobody in particular owns, there is no right context to capture.
    /// </summary>
    sealed class ForwardingProgress(InFlight entry) : IProgress<ImageLoadProgress>
    {
        public void Report(ImageLoadProgress value) => entry.Report(value);
    }
}
