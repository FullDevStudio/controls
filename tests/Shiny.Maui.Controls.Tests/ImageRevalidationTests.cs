using Shiny.Maui.Controls.Images;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// What happens once a cached image expires: it is revalidated with its validators rather than
/// downloaded again, a stale copy beats an error when the network is gone, and however many views
/// ask at once, one request goes out.
/// </summary>
public class ImageRevalidationTests : IDisposable
{
    const string Uri = "https://example.com/avatar.png";

    readonly string root = Path.Combine(Path.GetTempPath(), "shinyimage-reval-" + Guid.NewGuid().ToString("n"));
    readonly ManualTimeProvider clock = new();

    public void Dispose()
    {
        if (Directory.Exists(this.root))
            Directory.Delete(this.root, true);
    }

    ImageService Service(ScriptedDownloader downloader, Action<ImageOptions>? configure = null)
    {
        // Memory off, so every repeat load has to go through the disk tier this is about. The server
        // sends no expiry, so every entry - and every 304 - is good for exactly an hour.
        var options = new ImageOptions
        {
            CacheDirectory = this.root,
            MemoryCacheEnabled = false,
            DiskCacheDuration = TimeSpan.FromHours(1)
        };
        configure?.Invoke(options);
        return new ImageService(options, downloader, this.clock);
    }


    [Fact]
    public async Task AnExpiredEntryIsRevalidated_AndA304ServesItFromDisk()
    {
        var downloader = new ScriptedDownloader();
        var service = this.Service(downloader);

        (await service.GetAsync(new ImageRequest(Uri))).Origin.ShouldBe(ImageOrigin.Network);

        this.clock.Advance(TimeSpan.FromHours(2));
        downloader.Next = ScriptedDownloader.Answer.NotModified;

        var second = await service.GetAsync(new ImageRequest(Uri));

        second.Success.ShouldBeTrue();
        second.Origin.ShouldBe(ImageOrigin.Disk);
        downloader.Requests.Count.ShouldBe(2);
        downloader.Requests[1].IfNoneMatch.ShouldBe("\"v1\"");
        downloader.Requests[1].IfModifiedSince.ShouldBe(ScriptedDownloader.Modified);
    }


    [Fact]
    public async Task A304ExtendsTheEntry_SoTheNextLoadNeedsNoRequest()
    {
        var downloader = new ScriptedDownloader();
        var service = this.Service(downloader);

        await service.GetAsync(new ImageRequest(Uri));
        this.clock.Advance(TimeSpan.FromHours(2));
        downloader.Next = ScriptedDownloader.Answer.NotModified;
        await service.GetAsync(new ImageRequest(Uri));

        this.clock.Advance(TimeSpan.FromMinutes(30));   // inside the hour the 304 granted
        var third = await service.GetAsync(new ImageRequest(Uri));

        third.Origin.ShouldBe(ImageOrigin.Disk);
        downloader.Requests.Count.ShouldBe(2);
    }


    [Fact]
    public async Task AFreshEntrySendsNoRequestAtAll()
    {
        var downloader = new ScriptedDownloader();
        var service = this.Service(downloader);

        await service.GetAsync(new ImageRequest(Uri));
        this.clock.Advance(TimeSpan.FromMinutes(59));
        await service.GetAsync(new ImageRequest(Uri));

        downloader.Requests.Count.ShouldBe(1);
    }


    [Fact]
    public async Task AStaleCopyBeatsAnError()
    {
        var downloader = new ScriptedDownloader();
        var service = this.Service(downloader);

        await service.GetAsync(new ImageRequest(Uri));
        this.clock.Advance(TimeSpan.FromHours(2));
        downloader.Next = ScriptedDownloader.Answer.Throw;

        var offline = await service.GetAsync(new ImageRequest(Uri));

        offline.Success.ShouldBeTrue();
        offline.Origin.ShouldBe(ImageOrigin.Disk);
    }


    [Fact]
    public async Task AStaleCopyIsNotServed_WhenTheAppSaysNot()
    {
        var downloader = new ScriptedDownloader();
        var service = this.Service(downloader, o => o.ServeStaleOnError = false);

        await service.GetAsync(new ImageRequest(Uri));
        this.clock.Advance(TimeSpan.FromHours(2));
        downloader.Next = ScriptedDownloader.Answer.Throw;

        (await service.GetAsync(new ImageRequest(Uri))).Success.ShouldBeFalse();
    }


    [Fact]
    public async Task ManyViewsRevalidatingOneUri_SendOneRequest()
    {
        var downloader = new ScriptedDownloader();
        var service = this.Service(downloader);

        await service.GetAsync(new ImageRequest(Uri));
        this.clock.Advance(TimeSpan.FromHours(2));

        downloader.Next = ScriptedDownloader.Answer.NotModified;
        downloader.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var loads = Enumerable.Range(0, 12).Select(_ => service.GetAsync(new ImageRequest(Uri))).ToArray();

        await downloader.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        downloader.Gate.SetResult();

        var results = await Task.WhenAll(loads);

        results.ShouldAllBe(r => r.Success && r.Origin == ImageOrigin.Disk);
        downloader.Requests.Count.ShouldBe(2);
    }


    [Fact]
    public async Task ManyViewsReadingOneFreshUriFromDisk_StillSendNothing()
    {
        var downloader = new ScriptedDownloader();
        var service = this.Service(downloader);

        await service.GetAsync(new ImageRequest(Uri));

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => service.GetAsync(new ImageRequest(Uri))));

        results.ShouldAllBe(r => r.Success && r.Origin == ImageOrigin.Disk);
        downloader.Requests.Count.ShouldBe(1);
    }


    sealed class ScriptedDownloader : IImageDownloader
    {
        public enum Answer { Full, NotModified, Throw }

        public static readonly DateTimeOffset Modified = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        readonly Lock gate = new();
        readonly List<ImageRequest> requests = [];

        public Answer Next { get; set; } = Answer.Full;
        public TaskCompletionSource? Gate { get; set; }
        public TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<ImageRequest> Requests
        {
            get { lock (this.gate) return [.. this.requests]; }
        }

        public async Task<ImageDownloadResult> DownloadAsync(ImageRequest request, CancellationToken cancellationToken)
        {
            lock (this.gate)
                this.requests.Add(request);

            this.Waiting.TrySetResult();
            if (this.Gate is not null)
                await this.Gate.Task.ConfigureAwait(false);

            return this.Next switch
            {
                Answer.NotModified => new ImageDownloadResult(Stream.Null, NotModified: true),
                Answer.Throw => throw new HttpRequestException("offline"),
                _ => new ImageDownloadResult(new MemoryStream(new byte[256]), 256, "image/png", null, "\"v1\"", Modified)
            };
        }
    }
}
