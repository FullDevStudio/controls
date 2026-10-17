using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class BarcodesTests : BlazorTest
{
    // ── QR code page ───────────────────────────────────────────────────────

    ILocator QrHost => Page.Locator(".shiny-barcode");
    ILocator QrSvg => Page.Locator("div.shiny-barcode svg");
    ILocator ContentInput => Page.Locator("input[type=text]");
    ILocator SizeSlider => Page.Locator("input[type=range]");
    // the layout's own theme picker is a select too, so pick these by their options
    ILocator ErrorCorrection => Page.Locator("select", new() { Has = Page.Locator("option[value=Quartile]") });
    ILocator ImageFormat => Page.Locator("select", new() { Has = Page.Locator("option[value=Png]") });

    Task<string> QrMarkup() => QrSvg.EvaluateAsync<string>("el => el.innerHTML");

    [Test]
    public async Task Qr_Content_Change_Redraws_The_Code()
    {
        await Open("qrcode");
        await Expect(QrSvg).ToHaveCountAsync(1);
        var before = await QrMarkup();

        await ContentInput.FillAsync("https://example.com/a-completely-different-and-much-longer-value");
        await Page.WaitForFunctionAsync("before => document.querySelector('div.shiny-barcode svg')?.innerHTML !== before", before);

        // a longer payload needs a bigger symbol, so more modules are drawn
        await Assert.That((await QrMarkup()).Length).IsNotEqualTo(before.Length);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Qr_Empty_Content_Draws_Nothing()
    {
        await Open("qrcode");

        await ContentInput.FillAsync("");
        await Expect(QrSvg).ToHaveCountAsync(0);

        await ContentInput.FillAsync("back again");
        await Expect(QrSvg).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Qr_Size_Slider_Resizes_The_Code()
    {
        await Open("qrcode");
        await Expect(Page.GetByText("Size: 300 px")).ToBeVisibleAsync();

        await SizeSlider.FillAsync("400");
        await Expect(Page.GetByText("Size: 400 px")).ToBeVisibleAsync();
        await Expect(QrHost).ToHaveAttributeAsync("style", new Regex("width:400px;max-width:100%;height:auto;aspect-ratio:400/400"));
        await Expect(QrSvg).ToHaveAttributeAsync("viewBox", "0 0 400 400");

        var box = (await QrHost.BoundingBoxAsync())!;
        // 400px, unless its container is narrower (a phone): max-width:100% scales it down, square
        var room = await QrHost.EvaluateAsync<double>("e => e.parentElement.clientWidth - parseFloat(getComputedStyle(e.parentElement).paddingLeft) - parseFloat(getComputedStyle(e.parentElement).paddingRight)");
        await Assert.That(Math.Round(box.Width)).IsEqualTo(Math.Round(Math.Min(400d, room)));
        await Assert.That(Math.Abs(box.Height - box.Width)).IsLessThan(1.5f);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Qr_Higher_Error_Correction_Changes_The_Symbol()
    {
        await Open("qrcode");

        await ErrorCorrection.SelectOptionAsync("Low");
        await Expect(QrSvg).ToHaveCountAsync(1);
        var low = await QrMarkup();

        await ErrorCorrection.SelectOptionAsync("High");
        await Page.WaitForFunctionAsync("before => document.querySelector('div.shiny-barcode svg')?.innerHTML !== before", low);
        await Assert.That(await QrMarkup()).IsNotEqualTo(low);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Qr_Png_Format_Renders_An_Image()
    {
        await Open("qrcode");

        await ImageFormat.SelectOptionAsync("Png");
        var img = Page.Locator("img.shiny-barcode");
        await Expect(img).ToHaveAttributeAsync("src", new Regex("^data:image/png;base64,"));
        await Expect(QrSvg).ToHaveCountAsync(0);

        // the data URI decodes to a real image of the requested size
        await Page.WaitForFunctionAsync("() => document.querySelector('img.shiny-barcode')?.naturalWidth > 0");
        var natural = await img.EvaluateAsync<int>("el => el.naturalWidth");
        await Assert.That(natural).IsEqualTo(300);

        await ImageFormat.SelectOptionAsync("Svg");
        await Expect(QrSvg).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    // ── barcode gallery ────────────────────────────────────────────────────

    ILocator Card(string heading) => Page.Locator(".bc-card", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    [Test]
    [Arguments("Code 128")]
    [Arguments("EAN-13")]
    [Arguments("UPC-A")]
    [Arguments("Code 39")]
    [Arguments("PDF417")]
    [Arguments("Aztec")]
    [Arguments("Data Matrix")]
    public async Task Gallery_Draws_Every_Format(string format)
    {
        await Open("barcodes");

        var svg = Card(format).Locator(".shiny-barcode svg");
        await Expect(svg).ToHaveCountAsync(1);

        // dark modules are drawn - not just a blank background
        var marks = await svg.Locator("rect, path").CountAsync();
        await Assert.That(marks).IsGreaterThan(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Code 128", "0 0 500 150")]
    [Arguments("EAN-13", "0 0 500 160")]
    [Arguments("Aztec", "0 0 250 250")]
    public async Task Gallery_Honours_The_Pixel_Size(string format, string viewBox)
    {
        await Open("barcodes");

        await Expect(Card(format).Locator(".shiny-barcode svg")).ToHaveAttributeAsync("viewBox", viewBox);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Gallery_Css_Size_Sets_The_Displayed_Size()
    {
        await Open("barcodes");

        var aztec = (await Card("Aztec").Locator(".shiny-barcode").BoundingBoxAsync())!;
        await Assert.That(Math.Round(aztec.Width)).IsEqualTo(220d);
        await Assert.That(Math.Round(aztec.Height)).IsEqualTo(220d);

        var code128 = Card("Code 128");
        var host = (await code128.Locator(".shiny-barcode").BoundingBoxAsync())!;
        var card = (await code128.BoundingBoxAsync())!;
        await Assert.That(Math.Round(host.Height)).IsEqualTo(120d);
        // CssWidth="100%" fills the card's content box
        await Assert.That(host.Width).IsGreaterThan(card.Width - 40);
        await AssertNoConsoleErrors();
    }
}
