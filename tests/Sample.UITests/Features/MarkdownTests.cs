using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

/// <summary>
/// MarkdownView renders native Labels. Headings, paragraphs and task items are FormattedText spans,
/// which a text query cannot see, so these assert on the blocks that render plain Label.Text: fenced
/// code blocks and list markers.
/// </summary>
public class MarkdownViewTests : UITest
{
    const string ViewId = "SampleMarkdownView";

    async Task<bool> LabelWithTextExists(string text)
        => (await Driver.Query(text: text)).Any(e => e.Type == "Label");

    [Test]
    public async Task Setting_Markdown_Rerenders_The_View()
    {
        await Open("markdownview", "MarkdownViewPage");

        await Driver.SetProperty("Markdown", "# Title\n\n```\nMDVIEW-FIRST\n```", automationId: ViewId);
        await Driver.WaitUntil(() => LabelWithTextExists("MDVIEW-FIRST"), "the first code block to render");

        await Driver.SetProperty("Markdown", "```\nMDVIEW-SECOND\n```", automationId: ViewId);
        await Driver.WaitUntil(() => LabelWithTextExists("MDVIEW-SECOND"), "the second code block to render");
        await Driver.WaitUntil(async () => !await LabelWithTextExists("MDVIEW-FIRST"), "the old document to be gone");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Ordered_List_Is_Numbered()
    {
        await Open("markdownview", "MarkdownViewPage");

        await Driver.SetProperty("Markdown", "1. alpha\n2. beta\n3. gamma\n\n```\nMDVIEW-LIST\n```", automationId: ViewId);
        await Driver.WaitUntil(() => LabelWithTextExists("MDVIEW-LIST"), "the document to render");

        await Assert.That(await LabelWithTextExists("1.")).IsTrue();
        await Assert.That(await LabelWithTextExists("3.")).IsTrue();
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Theme_Change_Keeps_The_Content()
    {
        await Open("markdownview", "MarkdownViewPage");

        await Driver.SetProperty("Markdown", "```\nMDVIEW-THEME\n```", automationId: ViewId);
        await Driver.WaitUntil(() => LabelWithTextExists("MDVIEW-THEME"), "the document to render");

        await Driver.SetProperty("IsScrollEnabled", "False", automationId: ViewId);
        await Driver.WaitForProperty(ViewId, "IsScrollEnabled", "False");
        await Driver.SetProperty("IsScrollEnabled", "True", automationId: ViewId);

        await Assert.That(await LabelWithTextExists("MDVIEW-THEME")).IsTrue();
        await AssertNoAppErrors();
    }
}

public class MarkdownEditorTests : UITest
{
    const string EditorId = "SampleMarkdownEditor";

    // The control composes a plain Editor (the only one on the page) plus a toolbar of Buttons.
    Task<ElementInfo> InnerEditor() => Driver.FindByType("Editor");

    async Task OpenEditor()
    {
        await Open("markdowneditor", "MarkdownEditorPage");
        await Driver.WaitUntilExists(EditorId);
        await Driver.SetProperty("IsPreviewVisible", "False", automationId: EditorId);
    }

    [Test]
    public async Task Typing_Updates_The_Markdown_Property()
    {
        await OpenEditor();

        var editor = await InnerEditor();
        await Driver.Clear(elementId: editor.Id);
        await Driver.Fill("Hello **typed** world", elementId: (await InnerEditor()).Id);

        await Driver.WaitForProperty(EditorId, "Markdown", "Hello **typed** world");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Setting_Markdown_Fills_The_Editor()
    {
        await OpenEditor();

        await Driver.SetProperty("Markdown", "Set from outside", automationId: EditorId);

        await Driver.WaitUntil(
            async () => await Driver.GetProperty("Text", elementId: (await InnerEditor()).Id) == "Set from outside",
            "the inner editor to show the new markdown"
        );
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Bold_Toolbar_Button_Inserts_Markers()
    {
        await OpenEditor();

        await Driver.SetProperty("Markdown", "", automationId: EditorId);
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("Markdown", automationId: EditorId)), "the editor to clear");

        var bold = (await Driver.Query(type: "Button")).FirstOrDefault(b => b.Text == "B");
        await Assert.That(bold).IsNotNull();
        await Driver.Tap(elementId: bold!.Id);

        await Driver.WaitForProperty(EditorId, "Markdown", "****");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Preview_Renders_The_Markdown_And_Toggles_Back()
    {
        await OpenEditor();

        await Driver.SetProperty("Markdown", "```\nMDE-PREVIEW\n```", automationId: EditorId);
        await Driver.SetProperty("IsPreviewVisible", "True", automationId: EditorId);
        await Driver.WaitForProperty(EditorId, "IsPreviewVisible", "True");

        await Driver.WaitUntil(
            async () => (await Driver.Query(text: "MDE-PREVIEW")).Any(e => e.Type == "Label"),
            "the preview to render the code block"
        );

        await Driver.SetProperty("IsPreviewVisible", "False", automationId: EditorId);
        await Driver.WaitForProperty(EditorId, "IsPreviewVisible", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Preview_Button_Toggles_Preview()
    {
        await OpenEditor();

        // the eye button is the toolbar's only Button whose text is not a format glyph
        var eye = (await Driver.Query(type: "Button")).FirstOrDefault(b => b.Text == "👁️");
        await Assert.That(eye).IsNotNull();
        await Driver.Tap(elementId: eye!.Id);
        await Driver.WaitForProperty(EditorId, "IsPreviewVisible", "True");

        // now showing the pencil, which goes back to editing
        var pencil = (await Driver.Query(type: "Button")).FirstOrDefault(b => b.Text == "✏️");
        await Assert.That(pencil).IsNotNull();
        await Driver.Tap(elementId: pencil!.Id);
        await Driver.WaitForProperty(EditorId, "IsPreviewVisible", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Keyboard_Toolbar_Switch_Drives_The_Control()
    {
        await OpenEditor();

        try
        {
            await Driver.SetProperty("IsToggled", "False", automationId: "MarkdownKeyboardToolbarSwitch");
            await Driver.WaitForProperty(EditorId, "ShowToolbarInKeyboard", "False");

            await Driver.SetProperty("IsToggled", "True", automationId: "MarkdownKeyboardToolbarSwitch");
            await Driver.WaitForProperty(EditorId, "ShowToolbarInKeyboard", "True");
        }
        finally
        {
            await Driver.SetProperty("IsToggled", "True", automationId: "MarkdownKeyboardToolbarSwitch");
        }
        await AssertNoAppErrors();
    }
}
