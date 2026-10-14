using System.Text.RegularExpressions;

namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, "chat pages not in the macOS head")]
[SkipOn(UIPlatform.Linux, "chat pages not in the Linux head")]
public class ChatTests : UITest
{
    // ChatView keeps its messages in the ItemsSource of its inner CollectionView. Everything is scoped
    // to the ChatView by AutomationId: Shell keeps other pages alive (with their own CollectionViews
    // and "Send" buttons), and ChatPage and ChatTemplatePage each host one.
    string chat = "ChatView";

    async Task<int?> MessageCount()
    {
        var list = (await Driver.QuerySelector($"#{chat} CollectionView")).FirstOrDefault();
        if (list == null)
            return null;
        return Int32.TryParse(await Driver.GetProperty("ItemsSource.Count", elementId: list.Id), out var n) ? n : null;
    }

    async Task<int> WaitForMessages()
    {
        int? count = null;
        await Driver.WaitUntil(async () => (count = await MessageCount()) > 0, "the chat has loaded its history", 15);
        return count!.Value;
    }

    /// <summary>The message count once it has held still for 3s - a reply to an earlier test's message can still be on its way (~2.3s).</summary>
    async Task<int> WaitForQuiet()
    {
        var count = await WaitForMessages();
        var stableSince = DateTime.UtcNow;
        while (DateTime.UtcNow - stableSince < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(500);
            var now = await MessageCount() ?? count;
            if (now != count)
            {
                count = now;
                stableSince = DateTime.UtcNow;
            }
        }
        return count;
    }

    async Task TapChatButton(string text)
        => await Driver.Tap(elementId: (await Driver.FindBySelector($"#{chat} Button[Text={MauiDevFlowDriver.Quote(text)}]", timeoutSeconds: 10)).Id);

    static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";


    [Test]
    public async Task Send_Adds_The_Message_And_Clears_The_Entry()
    {
        await Open("chat", "ChatPage");
        var before = await WaitForMessages();

        await Driver.SetProperty("EntryText", Unique("from the ui test"), automationId: "ChatView");
        await TapChatButton("Send");

        await Driver.WaitUntil(async () => await MessageCount() >= before + 1, "the sent message is in the list");
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("EntryText", automationId: "ChatView")), "the entry is cleared");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Sending_Gets_A_Simulated_Reply()
    {
        await Open("chat", "ChatPage");
        var before = await WaitForMessages();

        await Driver.SetProperty("EntryText", Unique("ping"), automationId: "ChatView");
        await TapChatButton("Send");

        // the demo provider types for ~2.3s and then answers
        await Driver.WaitUntil(async () => await MessageCount() >= before + 2, "the reply arrives", 15);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Blank_Entry_Sends_Nothing()
    {
        await Open("chat", "ChatPage");
        var before = await WaitForQuiet();

        await Driver.SetProperty("EntryText", "   ", automationId: "ChatView");
        await TapChatButton("Send");
        await Task.Delay(1000);

        await Assert.That(await MessageCount()).IsEqualTo(before);
        await Driver.SetProperty("EntryText", "", automationId: "ChatView");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Template_Selector_Picks_A_Template_Per_Message_Kind()
    {
        await Open("chattemplates", "ChatTemplatePage");
        chat = "TemplateChatView";
        await WaitForMessages();

        // "action" messages get the button template, its label from Metadata["actionText"]
        await Driver.WaitUntil(async () => (await Driver.Query(type: "Button", text: "Schedule")).Count > 0, "the Schedule action button");
        await Driver.WaitUntil(async () => (await Driver.Query(type: "Button", text: "Book Now")).Count > 0, "the Book Now action button");
        // "card" messages get the card template, titled from Metadata["cardTitle"]
        await Driver.WaitUntil(async () => (await Driver.Query(type: "Label", text: "Mountain Resort Package")).Count > 0, "the card title");
        // plain messages get the text template
        await Driver.WaitUntil(async () => (await Driver.Query(type: "Label", text: "different message types")).Count > 0, "the plain text message");
    }


    [Test]
    public async Task Custom_Input_Bar_Sends()
    {
        await Open("chattemplates", "ChatTemplatePage");
        chat = "TemplateChatView";
        var before = await WaitForMessages();

        // the page swaps in its own ChatEntryView whose send button reads "↑"
        await Driver.WaitForProperty("TemplateChatView", "InputBar.SendButtonText", "↑");
        await Driver.SetProperty("EntryText", Unique("custom bar"), automationId: "TemplateChatView");
        await TapChatButton("↑");

        await Driver.WaitUntil(async () => await MessageCount() >= before + 1, "the sent message is in the list");
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("EntryText", automationId: "TemplateChatView")), "the entry is cleared");
        await AssertNoAppErrors();
    }
}
