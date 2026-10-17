using TUnit.Core.Interfaces;

namespace Sample.Blazor.UITests;

/// <summary>
/// Everything that varies between runs comes from the environment.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>SHINY_BLAZOR_URL</term><description>Test an app that is already being served (a dev server, a deployed site). Nothing is built or started.</description></item>
/// <item><term>SHINY_BLAZOR_MODE</term><description>dev (default) = `dotnet run` the sample; publish = Release publish, trimmed, served by Kestrel — the build users actually get, and the only one that shows trimming/interop failures.</description></item>
/// <item><term>SHINY_BLAZOR_BROWSER</term><description>chromium (default) | firefox | webkit.</description></item>
/// <item><term>SHINY_BLAZOR_HEADED</term><description>1 = show the browser.</description></item>
/// <item><term>SHINY_BLAZOR_PARALLEL</term><description>Concurrent pages (default 4).</description></item>
/// <item><term>SHINY_BLAZOR_DEVICE</term><description>Run the whole suite as a phone: iphone (iPhone 13, 390 wide) | android (Galaxy S8, 360 wide) | any Playwright device name. Touch, mobile viewport meta and a mobile user agent come with it.</description></item>
/// </list>
/// </remarks>
public static class BlazorTestSettings
{
    public static string? Url { get; } = Env("SHINY_BLAZOR_URL");
    public static bool Publish { get; } = Env("SHINY_BLAZOR_MODE") is "publish" or "release";
    public static string Browser { get; } = (Env("SHINY_BLAZOR_BROWSER") ?? "chromium").ToLowerInvariant();
    public static bool Headed { get; } = Env("SHINY_BLAZOR_HEADED") is "1" or "true";
    public static string? Device { get; } = Env("SHINY_BLAZOR_DEVICE") switch
    {
        null => null,
        var d when d.Equals("iphone", StringComparison.OrdinalIgnoreCase) => "iPhone 13",
        var d when d.Equals("android", StringComparison.OrdinalIgnoreCase) => "Galaxy S8",
        var d => d
    };
    public static int Parallel { get; } = int.TryParse(Env("SHINY_BLAZOR_PARALLEL"), out var p) && p > 0 ? p : 4;

    public static string RepoRoot { get; } = FindRepoRoot();
    public static string SampleProject => Path.Combine(RepoRoot, "samples", "Sample.Blazor", "Sample.Blazor.csproj");

    public static string ResultsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "uitest-results", Device == null ? Browser : $"{Browser}-{Device.Replace(' ', '-').ToLowerInvariant()}");

    static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    static string FindRepoRoot()
    {
        // set when the suite is built with --artifacts-path outside the repo
        if (Environment.GetEnvironmentVariable("SHINY_REPO_ROOT") is { Length: > 0 } explicitRoot)
            return explicitRoot;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Shiny.Controls.slnx")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repo root above " + AppContext.BaseDirectory);
    }
}

public sealed class BrowserParallelism : IParallelLimit
{
    public int Limit => BlazorTestSettings.Parallel;
}
