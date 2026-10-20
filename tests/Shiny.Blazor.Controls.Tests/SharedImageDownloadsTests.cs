using Shiny.Blazor.Controls.Images;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// Images loaded through an <see cref="IImageDownloader"/> - the authenticated path - share one
/// download per URI, the way the browser path shares one fetch.
/// </summary>
public class SharedImageDownloadsTests
{
    sealed class GatedDownloader : IImageDownloader
    {
        int calls;

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Fail { get; set; }
        public int Calls => Volatile.Read(ref this.calls);

        public async Task<ImageDownloadResult> DownloadAsync(string uri, IProgress<ImageLoadProgress>? progress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.calls);
            this.Started.TrySetResult();
            progress?.Report(new ImageLoadProgress(ImageLoadState.Downloading, 50, 100));

            await this.Gate.Task.ConfigureAwait(false);

            if (this.Fail)
                throw new HttpRequestException("401");

            return new ImageDownloadResult([1, 2, 3], "image/png");
        }
    }

    sealed class Recorder : IProgress<ImageLoadProgress>
    {
        public List<ImageLoadProgress> Seen { get; } = [];
        public void Report(ImageLoadProgress value) { lock (this.Seen) this.Seen.Add(value); }
    }


    [Fact]
    public async Task ConcurrentLoadsOfOneUri_MakeOneDownload()
    {
        var downloader = new GatedDownloader();
        var shared = SharedImageDownloads.For(downloader);

        var loads = Enumerable.Range(0, 12).Select(_ => shared.DownloadAsync("/a.png", null, default)).ToArray();

        await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        downloader.Gate.SetResult();

        var results = await Task.WhenAll(loads);

        downloader.Calls.ShouldBe(1);
        results.ShouldAllBe(r => r.Bytes.Length == 3);
    }


    [Fact]
    public async Task ALateJoinerIsToldHowFarTheDownloadHasGot()
    {
        var downloader = new GatedDownloader();
        var shared = SharedImageDownloads.For(downloader);

        var first = shared.DownloadAsync("/a.png", null, default);
        await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var late = new Recorder();
        var second = shared.DownloadAsync("/a.png", late, default);

        late.Seen.ShouldContain(p => p.BytesRead == 50 && p.TotalBytes == 100);

        downloader.Gate.SetResult();
        await Task.WhenAll(first, second);
    }


    [Fact]
    public async Task OneCallerGivingUp_DoesNotCancelTheOthers()
    {
        var downloader = new GatedDownloader();
        var shared = SharedImageDownloads.For(downloader);
        using var leaving = new CancellationTokenSource();

        var gone = shared.DownloadAsync("/a.png", null, leaving.Token);
        var staying = shared.DownloadAsync("/a.png", null, default);
        await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await leaving.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => gone);

        downloader.Gate.SetResult();
        (await staying).Bytes.Length.ShouldBe(3);
    }


    sealed class HangingDownloader : IImageDownloader
    {
        int calls;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls => Volatile.Read(ref this.calls);

        public async Task<ImageDownloadResult> DownloadAsync(string uri, IProgress<ImageLoadProgress>? progress, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref this.calls);
            using var registration = cancellationToken.Register(() => this.Cancelled.TrySetResult());
            this.Started.TrySetResult();

            // A server that never answers.
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return new ImageDownloadResult([], null);
        }
    }


    [Fact]
    public async Task EveryCallerGivingUp_CancelsTheDownloadAndForgetsIt()
    {
        var downloader = new HangingDownloader();
        var shared = SharedImageDownloads.For(downloader);
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();

        var a = shared.DownloadAsync("/hang.png", null, first.Token);
        var b = shared.DownloadAsync("/hang.png", null, second.Token);
        await downloader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await first.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => a);
        downloader.Cancelled.Task.IsCompleted.ShouldBeFalse();

        await second.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => b);
        await downloader.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // The entry went with it: the next load is a new download, not a wait on the abandoned one.
        using var third = new CancellationTokenSource();
        var c = shared.DownloadAsync("/hang.png", null, third.Token);
        for (var i = 0; i < 50 && downloader.Calls < 2; i++)
            await Task.Delay(20);
        downloader.Calls.ShouldBe(2);

        await third.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => c);
    }


    [Fact]
    public async Task AFailureIsNotRemembered()
    {
        var downloader = new GatedDownloader { Fail = true };
        var shared = SharedImageDownloads.For(downloader);
        downloader.Gate.SetResult();

        await Should.ThrowAsync<HttpRequestException>(() => shared.DownloadAsync("/a.png", null, default));

        downloader.Fail = false;
        (await shared.DownloadAsync("/a.png", null, default)).Bytes.Length.ShouldBe(3);
        downloader.Calls.ShouldBe(2);
    }


    [Fact]
    public async Task DifferentDownloadersShareNothing()
    {
        // One per scope - on Blazor Server, one per user. Their authenticated images must never mix.
        var a = new GatedDownloader();
        var b = new GatedDownloader();
        a.Gate.SetResult();
        b.Gate.SetResult();

        await Task.WhenAll(
            SharedImageDownloads.For(a).DownloadAsync("/me.png", null, default),
            SharedImageDownloads.For(b).DownloadAsync("/me.png", null, default)
        );

        a.Calls.ShouldBe(1);
        b.Calls.ShouldBe(1);
    }
}
