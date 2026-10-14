using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests;

/// <summary>
/// Every <c>@page</c> route in the sample, read from the .razor files so the sweep can't drift from
/// the app. Parameterised routes are left out — they need a value only a specific test knows.
/// </summary>
public static partial class BlazorRoutes
{
    public static IReadOnlyList<string> All { get; } = Load();

    static IReadOnlyList<string> Load()
    {
        var pages = Path.Combine(BlazorTestSettings.RepoRoot, "samples", "Sample.Blazor");
        return Directory
            .EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => PageRegex().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(r => !r.Contains('{'))
            .Distinct()
            .Order()
            .ToList();
    }

    [GeneratedRegex(@"^@page\s+""([^""]*)""", RegexOptions.Multiline)]
    private static partial Regex PageRegex();
}
