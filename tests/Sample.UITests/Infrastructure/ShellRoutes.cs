using System.Text.RegularExpressions;

namespace Sample.UITests;

/// <summary>A Shell route and the page class its ContentTemplate builds.</summary>
public sealed record ShellRoute(string Route, string PageType)
{
    public override string ToString() => Route;
}

/// <summary>
/// Reads the routes straight out of the head's AppShell.xaml so the sweep can never fall behind the
/// app: a page added to the Shell is swept the next run, and a head that drops one stops asking for it.
/// </summary>
public static partial class ShellRoutes
{
    static readonly Dictionary<string, IReadOnlyList<ShellRoute>> cache = new();

    public static IReadOnlyList<ShellRoute> Load(string appShellPath)
    {
        lock (cache)
        {
            if (!cache.TryGetValue(appShellPath, out var routes))
            {
                routes = Parse(File.ReadAllText(appShellPath));
                cache[appShellPath] = routes;
            }
            return routes;
        }
    }

    /// <summary>The routes of the head this run targets, for test data sources.</summary>
    public static IReadOnlyList<ShellRoute> ForCurrentPlatform()
    {
        var project = UITestSettings.Platform switch
        {
            UIPlatform.MacOS => "Sample.MacOS",
            UIPlatform.Linux => "Sample.Linux",
            _ => "Sample"
        };
        return Load(Path.Combine(UITestSettings.RepoRoot, "samples", project, "AppShell.xaml"));
    }

    public static IReadOnlyList<ShellRoute> Parse(string xaml)
    {
        var withoutComments = CommentRegex().Replace(xaml, "");
        var routes = new List<ShellRoute>();

        foreach (Match element in ShellContentRegex().Matches(withoutComments))
        {
            var attributes = element.Value;
            var route = RouteRegex().Match(attributes);
            var template = TemplateRegex().Match(attributes);
            if (route.Success && template.Success)
                routes.Add(new ShellRoute(route.Groups[1].Value, template.Groups[1].Value));
        }
        return routes;
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"<ShellContent\b[^>]*>", RegexOptions.Singleline)]
    private static partial Regex ShellContentRegex();

    [GeneratedRegex(@"\bRoute=""([^""]+)""")]
    private static partial Regex RouteRegex();

    [GeneratedRegex(@"ContentTemplate=""\{DataTemplate\s+(?:\w+:)?(\w+)\}""")]
    private static partial Regex TemplateRegex();
}
