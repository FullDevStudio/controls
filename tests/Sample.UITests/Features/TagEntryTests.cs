namespace Sample.UITests.Features;

/// <summary>
/// TagEntry: committing typed text into the bound collection (delimiters, Enter-only mode),
/// MaxTags, duplicates, the cancellable TagAdding validator, chip removal, read-only, and the
/// page's clear-and-refocus button. Text goes in through the inner BorderlessEntry; a DevFlow fill
/// that adds several characters at once is read by the control as a paste and committed, trailing
/// fragment included, while a one-character change is ordinary typing. Fills are prefixed with the
/// zero-width sentinel the control keeps in a focused field, so a replacement is never mistaken
/// for Backspace at the start of the field. Shell caches the page, so counts are compared against
/// what each test finds on arrival.
/// </summary>
public class TagEntryTests : UITest
{
    const string Route = "tagentry";
    const string PageType = "TagEntryPage";
    const string Sentinel = "​";

    async Task<int> TagCount(string tagEntryId)
        => Int32.Parse(await Driver.GetProperty("Tags.Count", automationId: tagEntryId) ?? "0");

    Task Paste(string tagEntryId, string text)
        => Driver.FillCss($"#{tagEntryId} BorderlessEntry", Sentinel + text);

    static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    /// <summary>The chip captions, in order - each TagChipView's own label (kept, hidden, under a ChipTemplate).</summary>
    async Task<string[]> ChipTexts(string tagEntryId)
        => (await Driver.CssNow($"#{tagEntryId} TagChipView Grid > HorizontalStackLayout > Label"))
            .Select(l => (l.Text ?? "").Trim())
            .ToArray();

    /// <summary>The ✕ of each chip: the ContentView sitting directly in the chip's row.</summary>
    static string RemoveButtons(string tagEntryId) => $"#{tagEntryId} TagChipView Grid > HorizontalStackLayout > ContentView";

    /// <summary>Types one character at a time, so nothing reads as a paste and nothing commits.</summary>
    async Task Type(string tagEntryId, string text)
    {
        for (var i = 1; i <= text.Length; i++)
            await Driver.FillCss($"#{tagEntryId} BorderlessEntry", Sentinel + text[..i]);
    }

    [Test]
    public async Task Comma_Commits_A_Tag_Into_The_Bound_Collection()
    {
        await Open(Route, PageType);
        var before = await TagCount("TeamTags");
        var tag = Unique("qa");

        await Paste("TeamTags", tag + ",");

        await Driver.WaitForValue("TeamTags", "Tags.Count", (before + 1).ToString());
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: "TeamSummary") ?? "").Contains(tag),
            "the team summary to list the new tag"
        );
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: "TagStatus") ?? "").StartsWith("Teams changed"),
            "the status line to report the change"
        );
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Pasted_List_Splits_On_Every_Delimiter()
    {
        await Open(Route, PageType);
        var before = await TagCount("RecipientTags");

        await Paste("RecipientTags", $"{Unique("a")}; {Unique("b")}, {Unique("c")}");

        await Driver.WaitForValue("RecipientTags", "Tags.Count", (before + 3).ToString());
    }

    [Test]
    public async Task Enter_Only_Mode_Keeps_Commas_Inside_The_Tag()
    {
        await Open(Route, PageType);
        var before = await TagCount("PhraseTags");
        var phrase = $"{Unique("x")}, {Unique("y")}";

        // An empty Delimiters list never splits: the pasted phrase is one tag, comma and all
        await Paste("PhraseTags", phrase);

        await Driver.WaitForValue("PhraseTags", "Tags.Count", (before + 1).ToString());
        await Driver.WaitUntil(async () => (await ChipTexts("PhraseTags")).LastOrDefault() == phrase, "the phrase to be the newest chip");
    }

    [Test]
    public async Task Enter_Commits_Typed_Text()
    {
        await Open(Route, PageType);
        var before = await TagCount("RecipientTags");
        var word = Unique("t");

        await Type("RecipientTags", word);
        await Driver.Client.KeyAsync("Enter", (await Driver.CssAt("#RecipientTags BorderlessEntry")).Id);

        await Driver.WaitForValue("RecipientTags", "Tags.Count", (before + 1).ToString());
        await Driver.WaitUntil(async () => (await ChipTexts("RecipientTags")).LastOrDefault() == word, "the typed word to be the newest chip");
        await AssertNoAppErrors();
    }

    // Verified on the macOS head with a real mouse click + real Backspace (cliclick): the NSTextField shows
    // its focus ring and caret, but the Entry's IsFocused stays False, the placeholder stays up (no sentinel
    // seeded) and Tags.Count does not change.
    [Test, SkipOn(UIPlatform.MacOS, "BUG: AppKit Entry never raises Focused, so TagEntry never seeds its Backspace sentinel - Backspace removes nothing")]
    public async Task Backspace_In_An_Empty_Field_Removes_The_Newest_Tag()
    {
        await Open(Route, PageType);
        await Paste("DuplicateTags", Unique("bk") + ",");
        await Driver.WaitUntil(async () => await TagCount("DuplicateTags") > 0, "a tag to be present");
        var before = await TagCount("DuplicateTags");
        var survivors = (await ChipTexts("DuplicateTags"))[..^1];

        // Focus seeds the sentinel; Backspace deleting it is the control's "Backspace at the start" signal
        var entry = await Driver.CssAt("#DuplicateTags BorderlessEntry");
        await Driver.Client.FocusAsync(entry.Id);
        await Task.Delay(300);
        await Driver.Client.KeyAsync("Backspace", (await Driver.CssAt("#DuplicateTags BorderlessEntry")).Id);

        await Driver.WaitForValue("DuplicateTags", "Tags.Count", (before - 1).ToString());
        await Driver.WaitUntil(async () => (await ChipTexts("DuplicateTags")).SequenceEqual(survivors), "only the newest chip to have gone");
    }

    [Test]
    public async Task MaxTags_Refuses_Commits_Past_The_Cap()
    {
        await Open(Route, PageType);
        var before = await TagCount("LimitedTags");
        await Assert.That(before).IsLessThanOrEqualTo(3);

        await Paste("LimitedTags", $"{Unique("p")}, {Unique("q")}, {Unique("r")}, {Unique("s")},");

        await Driver.WaitForValue("LimitedTags", "Tags.Count", "3");
        await Task.Delay(500);
        await Assert.That(await TagCount("LimitedTags")).IsEqualTo(3);
    }

    [Test]
    [Skip("BUG: pasting a tag that already exists deletes the newest chip. TryCommit refuses the duplicate, ProcessInput then writes the field "
        + "back from inside TextChanged; MAUI defers that same-property write until suppressTextChanged is released, so it arrives as a change that "
        + "dropped the sentinel and OnEntryTextChanged treats it as Backspace → RemoveAt(last). iOS: TeamTags [design, yyyq] + paste 'design,' → [design]")]
    public async Task Duplicates_Are_Dropped()
    {
        await Open(Route, PageType);

        var teams = await TagCount("TeamTags");
        var existing = (await ChipTexts("TeamTags")).FirstOrDefault();
        await Assert.That(existing).IsNotNull();
        await Paste("TeamTags", existing + ",");
        await Task.Delay(500);
        await Assert.That(await TagCount("TeamTags")).IsEqualTo(teams);
    }

    [Test]
    public async Task Duplicates_Are_Kept_When_Allowed()
    {
        await Open(Route, PageType);

        var dupes = await TagCount("DuplicateTags");
        await Paste("DuplicateTags", "echo,");
        await Driver.WaitForValue("DuplicateTags", "Tags.Count", (dupes + 1).ToString());
    }

    [Test]
    public async Task TagAdding_Validator_Refuses_Short_Tags()
    {
        await Open(Route, PageType);
        var before = await TagCount("ValidatedTags");

        await Paste("ValidatedTags", "ab,");
        await Task.Delay(500);
        await Assert.That(await TagCount("ValidatedTags")).IsEqualTo(before);

        await Paste("ValidatedTags", Unique("abc") + ",");
        await Driver.WaitForValue("ValidatedTags", "Tags.Count", (before + 1).ToString());
    }

    [Test]
    public async Task Chip_Remove_Button_Takes_The_Tag_Out()
    {
        await Open(Route, PageType);
        // make sure there is something to remove, whatever earlier runs left behind
        await Paste("LabelTags", Unique("lbl") + ",");
        await Driver.Css("#LabelTags TagChipView");
        var before = await TagCount("LabelTags");
        var remaining = (await ChipTexts("LabelTags"))[1..];

        await Driver.TapCss(RemoveButtons("LabelTags"));

        await Driver.WaitForValue("LabelTags", "Tags.Count", (before - 1).ToString());
        await Driver.WaitUntil(async () => (await ChipTexts("LabelTags")).SequenceEqual(remaining), "the first chip to have gone");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task ReadOnly_Blocks_Adding_And_Removing()
    {
        await Open(Route, PageType);
        var before = await TagCount("ReadOnlyTags");

        await Paste("ReadOnlyTags", "nope,");
        await Driver.TapCss(RemoveButtons("ReadOnlyTags"));
        await Task.Delay(500);

        await Assert.That(await TagCount("ReadOnlyTags")).IsEqualTo(before);
    }

    [Test]
    public async Task Clear_Button_Empties_The_Topics()
    {
        await Open(Route, PageType);
        await Paste("TopicTags", Unique("topic") + ",");
        await Driver.WaitUntil(async () => await TagCount("TopicTags") > 0, "a topic to be present");

        await Driver.Tap(automationId: "ClearTopics");

        await Driver.WaitForValue("TopicTags", "Tags.Count", "0");
        await Driver.WaitUntil(async () => (await Driver.CssNow("#TopicTags TagChipView")).Count == 0, "the topic chips to go");
        await AssertNoAppErrors();
    }
}
