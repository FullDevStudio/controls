using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The Mermaid page (/mermaid): a source textarea bound on input, a theme select, and the
/// <c>MermaidDiagram</c> preview, which renders through mermaid.js into <c>.shiny-mermaid</c> and
/// shows a red <c>pre</c> instead when the source does not parse.
/// </summary>
public class MermaidTests : BlazorTest
{
    ILocator Source => Page.Locator("#mm-source");
    ILocator Preview => Page.Locator(".shiny-mermaid");
    ILocator Svg => Preview.Locator("svg");

    [Test]
    public async Task Renders_The_Initial_Flowchart()
    {
        await Open("mermaid");
        await Expect(Svg).ToHaveAttributeAsync("aria-roledescription", new Regex("flowchart"), new() { Timeout = 30_000 });
        await Expect(Preview).ToContainTextAsync("Has account?");
        await Expect(Preview).ToContainTextAsync("Dashboard");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Editing_The_Source_Rerenders_The_Diagram()
    {
        await Open("mermaid");
        await Expect(Preview).ToContainTextAsync("Dashboard", new() { Timeout = 30_000 });

        await Source.FillAsync("flowchart LR\n    Q[Queue] --> W[Worker]\n    W --> S[Storage]");

        await Expect(Preview).ToContainTextAsync("Worker");
        await Expect(Preview).ToContainTextAsync("Storage");
        await Expect(Preview).Not.ToContainTextAsync("Dashboard");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Other_Diagram_Types_Render()
    {
        await Open("mermaid");
        await Expect(Svg).ToHaveCountAsync(1, new() { Timeout = 30_000 });

        await Source.FillAsync("sequenceDiagram\n    Alice->>Bob: Hello Bob\n    Bob-->>Alice: Hi Alice");

        await Expect(Svg).ToHaveAttributeAsync("aria-roledescription", "sequence");
        await Expect(Preview).ToContainTextAsync("Hello Bob");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Invalid_Source_Shows_An_Error_And_Recovers()
    {
        await Open("mermaid");
        await Expect(Svg).ToHaveCountAsync(1, new() { Timeout = 30_000 });

        await Source.FillAsync("flowchart TD\n    A[Start --> ((broken");

        var error = Preview.Locator("pre");
        await Expect(error).ToBeVisibleAsync();
        await Expect(Svg).ToHaveCountAsync(0);

        await Source.FillAsync("flowchart TD\n    A[Fixed] --> B[Again]");
        await Expect(Preview).ToContainTextAsync("Fixed");
        await Expect(error).ToHaveCountAsync(0);

        // mermaid.js itself logs the parse failure to the console; that is the library, not the page.
        await AssertNoConsoleErrors("Parse error", "Syntax error", "Expecting", "mermaid");
    }

    [Test]
    public async Task Changing_Theme_Rerenders_With_That_Theme()
    {
        await Open("mermaid");
        await Expect(Svg).ToHaveCountAsync(1, new() { Timeout = 30_000 });
        var before = await Svg.Locator("style").TextContentAsync() ?? "";

        await Page.Locator("#mm-theme").SelectOptionAsync("dark");

        await Expect(Svg.Locator("style")).Not.ToHaveTextAsync(before);
        await AssertNoConsoleErrors();
    }
}
