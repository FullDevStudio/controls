using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TUnit.Core.Interfaces;

namespace Sample.Blazor.UITests;

/// <summary>
/// Serves the Blazor sample once per session and owns the one browser every test opens pages in.
/// </summary>
public sealed class BlazorAppFixture : IAsyncInitializer, IAsyncDisposable
{
    Process? devServer;
    WebApplication? staticHost;
    IPlaywright? playwright;

    public string BaseUrl { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>
    /// One context for the session so the WASM runtime downloads once into its HTTP cache; each test
    /// still gets its own page. Tests must not rely on (or leave behind) localStorage state.
    /// </summary>
    public IBrowserContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(BlazorTestSettings.ResultsDirectory);

        BaseUrl = BlazorTestSettings.Url is string url
            ? url.TrimEnd('/') + "/"
            : BlazorTestSettings.Publish ? await StartPublishedAsync() : await StartDevServerAsync();

        if (BlazorTestSettings.Url == null)
            await WaitForHttpAsync(BaseUrl, TimeSpan.FromMinutes(5));

        // Installs only what is missing, so after the first run this is a quick no-op.
        var exit = Microsoft.Playwright.Program.Main(["install", BlazorTestSettings.Browser]);
        if (exit != 0)
            throw new InvalidOperationException($"playwright install {BlazorTestSettings.Browser} failed ({exit})");

        playwright = await Playwright.CreateAsync();
        var browserType = BlazorTestSettings.Browser switch
        {
            "firefox" => playwright.Firefox,
            "webkit" => playwright.Webkit,
            _ => playwright.Chromium
        };
        Browser = await browserType.LaunchAsync(new() { Headless = !BlazorTestSettings.Headed });
        Context = await Browser.NewContextAsync(new()
        {
            BaseURL = BaseUrl,
            ViewportSize = new() { Width = 1366, Height = 900 }
        });
    }

    async Task<string> StartDevServerAsync()
    {
        var url = $"http://127.0.0.1:{FreePort()}/";
        var logFile = Path.Combine(BlazorTestSettings.ResultsDirectory, "devserver.log");
        devServer = StartLogged(
            $"run --project {Quote(BlazorTestSettings.SampleProject)} --urls {url.TrimEnd('/')} --no-launch-profile",
            logFile
        );
        await Task.Yield();
        return url;
    }

    /// <summary>
    /// A Release publish is trimmed and (for WASM) relinked — the build a user gets. Interop DTOs that
    /// lose their constructors and anything else the trimmer breaks only fail here, never under `dotnet run`.
    /// </summary>
    async Task<string> StartPublishedAsync()
    {
        var output = Path.Combine(AppContext.BaseDirectory, "published-sample");
        var publish = StartLogged(
            $"publish {Quote(BlazorTestSettings.SampleProject)} -c Release -o {Quote(output)}",
            Path.Combine(BlazorTestSettings.ResultsDirectory, "publish.log")
        );
        await publish.WaitForExitAsync();
        if (publish.ExitCode != 0)
            throw new InvalidOperationException($"Publishing the Blazor sample failed — see {BlazorTestSettings.ResultsDirectory}/publish.log");

        var root = Path.Combine(output, "wwwroot");
        var port = FreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.ClearProviders();

        var files = new PhysicalFileProvider(root);
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".dat"] = "application/octet-stream";
        contentTypes.Mappings[".wasm"] = "application/wasm";

        staticHost = builder.Build();
        staticHost.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        staticHost.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = true
        });
        staticHost.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
        await staticHost.StartAsync();

        return $"http://127.0.0.1:{port}/";
    }

    static Process StartLogged(string dotnetArgs, string logFile)
    {
        ProcessStartInfo info;
        if (OperatingSystem.IsWindows())
        {
            info = new ProcessStartInfo("dotnet", dotnetArgs);
        }
        else
        {
            // The dotnet with the wasm workloads is the login shell's, not whatever PATH a test host
            // inherited. ArgumentList, not Arguments: .NET splits Arguments with Windows quoting rules.
            info = new ProcessStartInfo("/bin/zsh");
            info.ArgumentList.Add("-lc");
            info.ArgumentList.Add("exec dotnet " + dotnetArgs);
        }

        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.UseShellExecute = false;
        info.WorkingDirectory = BlazorTestSettings.RepoRoot;

        var writer = new StreamWriter(logFile) { AutoFlush = true };
        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (writer) writer.WriteLine(e.Data); };
        process.Exited += (_, _) => { lock (writer) writer.Dispose(); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    async Task WaitForHttpAsync(string url, TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (devServer is { HasExited: true })
                throw new InvalidOperationException($"The Blazor dev server exited — see {BlazorTestSettings.ResultsDirectory}/devserver.log");

            try
            {
                using var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // not listening yet
            }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"{url} did not answer within {timeout}");
    }

    /// <summary>Double quotes: the one form both .NET's argument splitter and zsh -lc read the same way.</summary>
    static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (Context != null)
            await Context.DisposeAsync();
        if (Browser != null)
            await Browser.DisposeAsync();
        playwright?.Dispose();

        if (staticHost != null)
            await staticHost.DisposeAsync();

        if (devServer is { HasExited: false })
        {
            try { devServer.Kill(entireProcessTree: true); } catch { }
        }
        devServer?.Dispose();
    }
}
