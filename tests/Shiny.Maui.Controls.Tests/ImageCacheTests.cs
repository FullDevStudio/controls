using System.Net.Http.Headers;
using Shiny.Maui.Controls.Images;
using Shiny.Maui.Controls.Images.Caching;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// The parts of image loading that have nothing to do with a view: cache keys, expiry, eviction, and
/// the header parsing that decides how long an entry lives.
/// </summary>
public class ImageCacheTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "shinyimage-tests-" + Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(this.root))
            Directory.Delete(this.root, true);
    }

    ImageOptions Options(Action<ImageOptions>? configure = null)
    {
        var options = new ImageOptions { CacheDirectory = this.root };
        configure?.Invoke(options);
        return options;
    }

    static ImageDownloadResult Download(byte[] bytes, DateTimeOffset? expires = null)
        => new(new MemoryStream(bytes), bytes.Length, "image/png", expires);

    static byte[] Bytes(int size, byte fill = 1) => Enumerable.Repeat(fill, size).ToArray();


    [Fact]
    public void GetKey_IsStableAndDistinct()
    {
        const string a = "https://example.com/a.png?token=1";
        const string b = "https://example.com/a.png?token=2";

        DiskImageCache.GetKey(a).ShouldBe(DiskImageCache.GetKey(a));
        DiskImageCache.GetKey(a).ShouldNotBe(DiskImageCache.GetKey(b));

        // The key becomes a filename, so it must never carry the URL's own characters - a signed URL
        // would otherwise leak its credentials into the cache directory listing.
        DiskImageCache.GetKey(a).ShouldNotContain("token");
        DiskImageCache.GetKey(a).ShouldAllBe(c => Uri.IsHexDigit(c));
    }


    [Fact]
    public async Task Write_ThenGet_RoundTrips()
    {
        var cache = new DiskImageCache(this.Options());
        var payload = Bytes(2048);

        var written = await cache.WriteAsync("https://example.com/a.png", new MemoryStream(payload), Download(payload), TimeSpan.FromDays(1));
        written.ContentLength.ShouldBe(payload.Length);

        var read = await cache.GetAsync("https://example.com/a.png");
        read.ShouldNotBeNull();
        read.ContentType.ShouldBe("image/png");

        var file = cache.GetImagePath("https://example.com/a.png");
        File.Exists(file).ShouldBeTrue();
        (await File.ReadAllBytesAsync(file)).ShouldBe(payload);
    }


    [Fact]
    public async Task Get_ReturnsNull_WhenExpired()
    {
        var cache = new DiskImageCache(this.Options());
        var payload = Bytes(64);

        // A server-supplied expiry in the past. The entry is still written - the write path stays
        // uniform - but it must never be served.
        await cache.WriteAsync(
            "https://example.com/stale.png",
            new MemoryStream(payload),
            Download(payload, DateTimeOffset.UtcNow.AddMinutes(-5)),
            TimeSpan.FromDays(7)
        );

        (await cache.GetAsync("https://example.com/stale.png")).ShouldBeNull();
    }


    [Fact]
    public async Task Write_UsesFallbackDuration_WhenServerSaysNothing()
    {
        var cache = new DiskImageCache(this.Options());
        var payload = Bytes(64);

        var meta = await cache.WriteAsync(
            "https://example.com/plain.png",
            new MemoryStream(payload),
            Download(payload),
            TimeSpan.FromHours(3)
        );

        meta.ExpiresUtc.ShouldBeInRange(
            DateTimeOffset.UtcNow.AddHours(3).AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(3).AddMinutes(1)
        );
    }


    [Fact]
    public async Task Trim_EvictsLeastRecentlyUsedFirst()
    {
        // Three 4KB entries against an 9KB ceiling: the trim target is 80% of that, so two have to go.
        var cache = new DiskImageCache(this.Options(o => o.MaxDiskCacheBytes = 9 * 1024));

        foreach (var name in new[] { "oldest", "middle", "newest" })
        {
            var payload = Bytes(4 * 1024);
            await cache.WriteAsync($"https://example.com/{name}.png", new MemoryStream(payload), Download(payload), TimeSpan.FromDays(1));

            // WriteAsync stamps LastAccessUtc from the clock, and three writes inside one tick would
            // make the ordering arbitrary.
            await Task.Delay(15);
        }

        // Touching the oldest entry has to move it to the back of the eviction queue, or the cache
        // is a FIFO wearing an LRU's name.
        (await cache.GetAsync("https://example.com/oldest.png")).ShouldNotBeNull();
        await Task.Delay(15);

        await cache.TrimAsync();

        (await cache.GetAsync("https://example.com/oldest.png")).ShouldNotBeNull();
        (await cache.GetAsync("https://example.com/middle.png")).ShouldBeNull();
        (await cache.GetSizeAsync()).ShouldBeLessThanOrEqualTo(9 * 1024);
    }


    [Fact]
    public async Task Clear_RemovesEverything()
    {
        var cache = new DiskImageCache(this.Options());
        var payload = Bytes(512);

        await cache.WriteAsync("https://example.com/a.png", new MemoryStream(payload), Download(payload), TimeSpan.FromDays(1));
        await cache.WriteAsync("https://example.com/b.png", new MemoryStream(payload), Download(payload), TimeSpan.FromDays(1));

        (await cache.GetSizeAsync()).ShouldBe(1024);
        await cache.ClearAsync();
        (await cache.GetSizeAsync()).ShouldBe(0);
    }


    [Fact]
    public async Task Write_LeavesNoEntry_WhenTheStreamFaults()
    {
        var cache = new DiskImageCache(this.Options());

        await Should.ThrowAsync<IOException>(() => cache.WriteAsync(
            "https://example.com/torn.png",
            new FaultingStream(),
            Download([]),
            TimeSpan.FromDays(1)
        ));

        // A truncated file left behind here would read back as a perfectly valid cached image and
        // stay wrong until it expired days later.
        File.Exists(cache.GetImagePath("https://example.com/torn.png")).ShouldBeFalse();
        (await cache.GetSizeAsync()).ShouldBe(0);
    }


    [Fact]
    public void MemoryCache_EvictsLeastRecentlyUsed()
    {
        var cache = new MemoryImageCache(this.Options(o =>
        {
            o.MaxMemoryCacheBytes = 3000;
            o.MaxMemoryItemBytes = 2000;
        }));

        cache.Set("a", Bytes(1000, 1));
        cache.Set("b", Bytes(1000, 2));
        cache.Get("a").ShouldNotBeNull();   // 'a' is now the most recent, 'b' the least
        cache.Set("c", Bytes(1500, 3));

        cache.SizeInBytes.ShouldBeLessThanOrEqualTo(3000);
        cache.Get("b").ShouldBeNull();
        cache.Get("a").ShouldNotBeNull();
        cache.Get("c").ShouldNotBeNull();
    }


    [Fact]
    public void MemoryCache_RefusesOversizedItems()
    {
        var cache = new MemoryImageCache(this.Options(o =>
        {
            o.MaxMemoryCacheBytes = 10_000;
            o.MaxMemoryItemBytes = 1000;
        }));

        cache.Set("small", Bytes(500));
        cache.Set("huge", Bytes(5000));

        // Admitting the huge one would evict several small entries to hold the single thing least
        // likely to be asked for again.
        cache.Get("huge").ShouldBeNull();
        cache.Get("small").ShouldNotBeNull();
        cache.Count.ShouldBe(1);
    }


    [Fact]
    public void MemoryCache_IsInertWhenDisabled()
    {
        var cache = new MemoryImageCache(this.Options(o => o.MemoryCacheEnabled = false));

        cache.Set("a", Bytes(100));
        cache.Get("a").ShouldBeNull();
        cache.Count.ShouldBe(0);
    }


    [Theory]
    [InlineData(null, false)]
    [InlineData("https://example.com/a.png", true)]
    [InlineData("http://example.com/a.png", true)]
    [InlineData("file:///tmp/a.png", false)]
    [InlineData("dotnet_bot.png", false)]
    [InlineData("/var/mobile/a.png", false)]
    public void IsRemote_OnlyMatchesHttp(string? uri, bool expected)
        => ShinyImage.IsRemote(uri ?? String.Empty).ShouldBe(expected);


    [Fact]
    public void ResolveExpiry_PrefersMaxAgeOverExpiresHeader()
    {
        var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var cacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromMinutes(30) };

        HttpImageDownloader
            .ResolveExpiry(cacheControl, now.AddDays(5), now)
            .ShouldBe(now.AddMinutes(30));
    }


    [Fact]
    public async Task Expiry_RunsOnTheInjectedClock()
    {
        var clock = new ManualTimeProvider();
        var cache = new DiskImageCache(this.Options(), clock);
        var payload = Bytes(64);

        var meta = await cache.WriteAsync("https://example.com/a.png", new MemoryStream(payload), Download(payload), TimeSpan.FromHours(3));

        meta.ExpiresUtc.ShouldBe(clock.GetUtcNow().AddHours(3));
        (await cache.GetAsync("https://example.com/a.png")).ShouldNotBeNull();

        clock.Advance(TimeSpan.FromHours(3));

        (await cache.GetAsync("https://example.com/a.png")).ShouldBeNull();

        // Expired is not gone: the validators and bytes are still there to revalidate against.
        var stale = await cache.GetEntryAsync("https://example.com/a.png");
        stale.ShouldNotBeNull();
        stale.IsExpiredAt(clock.GetUtcNow()).ShouldBeTrue();
    }


    [Fact]
    public void ResolveExpiry_TreatsNoStoreAsAlreadyExpired()
    {
        // Not null. Null would mean "you decide", and deciding to cache something the server
        // explicitly asked us not to is the wrong answer.
        HttpImageDownloader
            .ResolveExpiry(new CacheControlHeaderValue { NoStore = true }, DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow)
            .ShouldBe(DateTimeOffset.MinValue);
    }


    [Fact]
    public void ResolveExpiry_FallsBackToNull_WhenNothingIsSaid()
        => HttpImageDownloader.ResolveExpiry(null, null, DateTimeOffset.UtcNow).ShouldBeNull();


    sealed class FaultingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("connection reset");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
            => ValueTask.FromException<int>(new IOException("connection reset"));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
