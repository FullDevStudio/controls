namespace Sample.UITests.Features;

/// <summary>
/// PasswordStrength: setting <c>Password</c> re-evaluates after the debounce; Score/Level/IsAcceptable
/// flow out to the view model, IsAcceptable gates the submit button, and StrengthChanged writes the
/// page's status line.
/// </summary>
[SkipOn(UIPlatform.Linux, KnownGaps.LinuxSiblingShellContentUnreachable)]
public class PasswordStrengthTests : UITest
{
    const string Field = "DefaultPasswordStrength";
    const string Breached = "Passw0rd!";
    const string Passphrase = "the slow red barn on nine";

    /// <summary>
    /// PasswordStrength is the second ShellContent under Security Pin; on AppKit Shell never presents it when
    /// arriving straight from SecurityPin (see <see cref="KnownGaps.AppKitSiblingShellContentSwitch"/>), so
    /// leave the section first there.
    /// </summary>
    async Task OpenPage()
    {
        if (Platform == UIPlatform.MacOS)
            await Open("home");
        await Open("passwordstrength", "PasswordStrengthPage");
    }

    async Task WaitForText(string automationId, Func<string, bool> condition, string description)
    {
        string? text = null;
        await Driver.WaitUntil(async () =>
        {
            text = await Driver.GetProperty("Text", automationId: automationId);
            return text != null && condition(text);
        }, $"{automationId}.Text {description} (last: '{text}')");
    }


    [Test]
    public async Task Common_Password_Is_Refused_And_Submit_Stays_Disabled()
    {
        await OpenPage();

        await Driver.SetProperty("Password", Breached, automationId: Field);

        await WaitForText("PasswordStatusLabel", t => t.Contains("most commonly used passwords"), "mentions a common password");
        await Driver.WaitForProperty(Field, "IsAcceptable", "False");
        await Driver.WaitForProperty("SubmitButton", "IsEnabled", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Long_Passphrase_Is_Acceptable_And_Submits()
    {
        await OpenPage();

        await Driver.SetProperty("Password", Passphrase, automationId: Field);

        await Driver.WaitForProperty(Field, "IsAcceptable", "True");
        await Driver.WaitForProperty("SubmitButton", "IsEnabled", "True");
        await Assert.That(await Driver.GetDouble("Score", Field) ?? 0).IsGreaterThan(0d);

        await Driver.Tap(automationId: "SubmitButton");
        await WaitForText("PasswordStatusLabel", t => t.StartsWith("Accepted a ") && t.EndsWith("/100)."), "reports the accepted passphrase");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Clearing_The_Password_Resets_The_Result()
    {
        await OpenPage();
        await Driver.SetProperty("Password", Passphrase, automationId: Field);
        await Driver.WaitForProperty(Field, "IsAcceptable", "True");

        await Driver.SetProperty("Password", "", automationId: Field);

        await Driver.WaitForProperty(Field, "IsAcceptable", "False");
        await Driver.WaitForProperty(Field, "Score", "0");
        await Driver.WaitForProperty(Field, "Level", "None");
        await Driver.WaitForProperty("SubmitButton", "IsEnabled", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Stronger_Input_Scores_Higher()
    {
        await OpenPage();

        await Driver.SetProperty("Password", "abc", automationId: Field);
        await Driver.WaitForProperty(Field, "Level", "Weak");
        var weak = await Driver.GetDouble("Score", Field) ?? -1;

        await Driver.SetProperty("Password", Passphrase, automationId: Field);
        await Driver.WaitForProperty(Field, "IsAcceptable", "True");
        var strong = await Driver.GetDouble("Score", Field) ?? -1;

        await Assert.That(strong).IsGreaterThan(weak);
        await AssertNoAppErrors();
    }
}
