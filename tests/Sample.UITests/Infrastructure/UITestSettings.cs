using System.Runtime.InteropServices;

namespace Sample.UITests;

public enum UIPlatform
{
    iOS,
    Android,
    MacOS,
    Linux,
    Windows
}

/// <summary>
/// One run drives exactly one MAUI head. Everything that varies between runs comes from the
/// environment, so the same suite is driven by hand, by a script, or by CI without a code change.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>SHINY_UITEST_PLATFORM</term><description>ios | android | macos | linux | windows. Defaults to the host OS's desktop head (macOS → macos).</description></item>
/// <item><term>SHINY_UITEST_ATTACH</term><description>1 = the app is already running; connect to it and never build, install or launch.</description></item>
/// <item><term>SHINY_UITEST_NOBUILD</term><description>1 = launch the last build instead of rebuilding first.</description></item>
/// <item><term>SHINY_UITEST_PORT</term><description>Host port of the DevFlow agent. Otherwise discovered through the broker, then 9223.</description></item>
/// <item><term>SHINY_UITEST_DEVICE</term><description>iOS simulator UDID or name, or Android adb serial.</description></item>
/// <item><term>SHINY_UITEST_AVD</term><description>Android emulator to boot when no device is attached (default devflow-tests-api35).</description></item>
/// <item><term>SHINY_UITEST_CONFIGURATION</term><description>Build configuration (default Debug — the DevFlow agent is Debug-only).</description></item>
/// </list>
/// </remarks>
public static class UITestSettings
{
    public static UIPlatform Platform { get; } = ParsePlatform(Env("SHINY_UITEST_PLATFORM"));
    public static bool Attach { get; } = Flag("SHINY_UITEST_ATTACH");
    public static bool NoBuild { get; } = Flag("SHINY_UITEST_NOBUILD");
    public static int? Port { get; } = int.TryParse(Env("SHINY_UITEST_PORT"), out var p) ? p : null;
    public static string? Device { get; } = Env("SHINY_UITEST_DEVICE");
    public static string Avd { get; } = Env("SHINY_UITEST_AVD") ?? "devflow-tests-api35";
    public static string Configuration { get; } = Env("SHINY_UITEST_CONFIGURATION") ?? "Debug";

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string ResultsDirectory { get; } = Path.Combine(
        AppContext.BaseDirectory,
        "uitest-results",
        Platform.ToString().ToLowerInvariant()
    );

    public static string PlatformName => Platform.ToString().ToLowerInvariant();

    /// <summary>Whether the selected head can be built and launched from the machine running the tests.</summary>
    public static bool CanLaunchOnThisHost => Platform switch
    {
        UIPlatform.iOS or UIPlatform.MacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
        UIPlatform.Windows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
        // macOS too, against Homebrew's gtk4 — see LinuxAppHost
        UIPlatform.Linux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && LinuxAppHost.HomebrewGtkInstalled),
        UIPlatform.Android => true,
        _ => false
    };

    static UIPlatform ParsePlatform(string? value) => value?.ToLowerInvariant() switch
    {
        "ios" => UIPlatform.iOS,
        "android" => UIPlatform.Android,
        "macos" or "appkit" or "mac" => UIPlatform.MacOS,
        "linux" or "gtk" => UIPlatform.Linux,
        "windows" or "winui" => UIPlatform.Windows,
        null or "" => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? UIPlatform.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? UIPlatform.Linux
                : UIPlatform.MacOS,
        _ => throw new InvalidOperationException(
            $"SHINY_UITEST_PLATFORM '{value}' is not one of ios, android, macos, linux, windows"
        )
    };

    static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    static bool Flag(string name) => Env(name) is "1" or "true" or "yes";

    static string FindRepoRoot()
    {
        // set when the suite is built with --artifacts-path outside the repo
        if (Environment.GetEnvironmentVariable("SHINY_REPO_ROOT") is { Length: > 0 } explicitRoot)
            return explicitRoot;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Shiny.Controls.slnx")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException(
            "Could not find the repo root (Shiny.Controls.slnx) above " + AppContext.BaseDirectory
        );
    }
}
