namespace Sample.Blazor.UITests.Features;

public class MarkdownViewTests : BlazorTest
{
    ILocator Preview => Page.Locator(".md-preview .shiny-md-content");
    ILocator Source => Page.Locator("#md-source");

    [Test]
    public async Task Renders_The_Markdown_Constructs()
    {
        await Open("markdown");

        await Expect(Preview.Locator("h1")).ToHaveTextAsync("Hello, Shiny.Blazor.Controls.Markdown");
        await Expect(Preview.Locator("h2")).ToHaveTextAsync("Features");
        await Expect(Preview.Locator("ul > li")).ToHaveCountAsync(5);
        await Expect(Preview.Locator("pre code.language-csharp")).ToContainTextAsync("Markdig.Markdown.ToHtml");
        await Expect(Preview.Locator("blockquote")).ToContainTextAsync("Tip:");
        await Expect(Preview.Locator("table tbody tr")).ToHaveCountAsync(3);
        await Expect(Preview.Locator("strong").First).ToHaveTextAsync("Markdown viewer");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Typing_In_The_Source_Rerenders_The_Preview()
    {
        await Open("markdown");

        await Source.FillAsync("# Typed heading\n\nSome *emphasis* and a [link](https://example.com)\n\n1. one\n2. two");

        await Expect(Preview.Locator("h1")).ToHaveTextAsync("Typed heading");
        await Expect(Preview.Locator("em")).ToHaveTextAsync("emphasis");
        await Expect(Preview.Locator("ol > li")).ToHaveCountAsync(2);
        await Expect(Preview.Locator("h2")).ToHaveCountAsync(0); // the old document is gone
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_A_Link_Raises_LinkTapped()
    {
        // The sample leaves Handled false, so the view would also open the URL in a new tab; keep the
        // test off the internet by stubbing window.open.
        await Page.AddInitScriptAsync("window.open = () => null;");
        await Open("markdown");

        var lastLink = Page.Locator("p.last-link strong");
        await Expect(lastLink).ToHaveTextAsync("(none)");

        await Preview.GetByRole(AriaRole.Link, new() { Name = "Visit shinylib.net" }).ClickAsync();

        await Expect(lastLink).ToHaveTextAsync("https://shinylib.net");
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/markdown$"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Dark_Theme_Switches_The_Palette()
    {
        await Open("markdown");

        var view = Page.Locator(".md-preview .shiny-md");
        var before = await view.EvaluateAsync<string>("e => getComputedStyle(e).color");

        await Page.GetByLabel("Dark theme").CheckAsync();

        await Page.WaitForFunctionAsync(
            "before => getComputedStyle(document.querySelector('.md-preview .shiny-md')).color !== before",
            before
        );
        await AssertNoConsoleErrors();
    }

}

public class MarkdownEditorTests : BlazorTest
{
    ILocator SplitEditor => Page.Locator(".shiny-md-editor.is-split");
    ILocator SplitInput => SplitEditor.Locator("textarea.shiny-md-editor-textarea");
    ILocator SplitPreview => SplitEditor.Locator(".shiny-md-editor-preview .shiny-md-content");
    ILocator PlainEditor => Page.Locator(".shiny-md-editor:not(.is-split)");

    [Test]
    public async Task Split_Editor_Previews_The_Bound_Markdown()
    {
        await Open("markdown-editor");

        await Expect(SplitInput).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(@"^# Hello, Markdown editor!"));
        await Expect(SplitPreview.Locator("h1")).ToHaveTextAsync("Hello, Markdown editor!");
        await Expect(SplitPreview.Locator("blockquote")).ToContainTextAsync("Block quotes work too.");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Typing_Updates_The_Live_Preview()
    {
        await Open("markdown-editor");

        await SplitInput.FillAsync("");
        await SplitInput.PressSequentiallyAsync("## Live **typing**");

        await Expect(SplitPreview.Locator("h2")).ToHaveTextAsync("Live typing");
        await Expect(SplitPreview.Locator("h2 strong")).ToHaveTextAsync("typing");
        await Expect(SplitPreview.Locator("h1")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Two_Editors_Bound_To_One_Value_Stay_In_Sync()
    {
        await Open("markdown-editor");

        await SplitInput.FillAsync("shared text");
        await Expect(PlainEditor.Locator("textarea")).ToHaveValueAsync("shared text");

        await PlainEditor.Locator("textarea").FillAsync("# From the plain one");
        await Expect(SplitInput).ToHaveValueAsync("# From the plain one");
        await Expect(SplitPreview.Locator("h1")).ToHaveTextAsync("From the plain one");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Editor_Without_Split_Has_No_Preview_And_Shows_Its_Placeholder()
    {
        await Open("markdown-editor");

        await Expect(PlainEditor.Locator(".shiny-md-editor-preview")).ToHaveCountAsync(0);

        var input = PlainEditor.Locator("textarea");
        await Expect(input).ToHaveAttributeAsync("placeholder", "Markdown only — no preview");
        await input.FillAsync("");
        await Expect(input).ToHaveValueAsync("");
        await AssertNoConsoleErrors();
    }
}
