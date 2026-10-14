namespace Sample.UITests.Features;

/// <summary>
/// FrostedGlassView on <c>frostedglass</c>. The blur is native and only visible in pixels, so these
/// assert that the effect re-applies at runtime without the platform effect throwing, and that the
/// values stick. Each test puts the card back the way the XAML declared it.
/// </summary>
public class FrostedGlassTests : UITest
{
    const string Card = "GlassCard";

    [Test]
    public async Task Blur_Radius_Can_Change_At_Runtime()
    {
        await Open("frostedglass", "FrostedGlassPage");
        try
        {
            foreach (var radius in new[] { "0", "45", "5" })
            {
                await Driver.SetProperty("BlurRadius", radius, automationId: Card);
                await Driver.WaitForProperty(Card, "BlurRadius", radius);
            }
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("BlurRadius", "20", automationId: Card);
        }
    }

    [Test]
    public async Task Tint_And_Corners_Can_Change_At_Runtime()
    {
        await Open("frostedglass", "FrostedGlassPage");
        try
        {
            await Driver.SetProperty("TintOpacity", "0.2", automationId: Card);
            await Assert.That(Math.Abs((await Driver.GetDouble("TintOpacity", Card) ?? -1) - 0.2) < 0.001).IsTrue();

            await Driver.SetProperty("CornerRadius", "4", automationId: Card);
            await Assert.That(await Driver.GetDouble("CornerRadius", Card)).IsEqualTo(4);
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("TintOpacity", "0.6", automationId: Card);
            await Driver.SetProperty("CornerRadius", "16", automationId: Card);
        }
    }
}
