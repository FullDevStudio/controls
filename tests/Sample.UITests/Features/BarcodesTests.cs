namespace Sample.UITests.Features;

[SkipOn(UIPlatform.Linux, "barcode pages not in the Linux head")]
public class BarcodesTests : UITest
{
    // BarcodeView = ContentView > Image; the agent reports a rendered PNG as "StreamImageSource"
    // and an unrenderable value as null.
    Task<string?> Rendered(string automationId) => Driver.GetProperty("Content.Source", automationId);


    // ── QR code ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Qr_Follows_The_Entry()
    {
        await Open("qrcode", "QRCodePage");

        await Driver.SetProperty("Text", "https://example.com/ui-test", automationId: "QRValueEntry");
        await Driver.WaitForProperty("QRCodeView", "Value", "https://example.com/ui-test");
        await Driver.WaitUntil(async () => await Rendered("QRCodeView") != null, "the QR code is drawn");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Qr_Empty_Value_Draws_Nothing()
    {
        await Open("qrcode", "QRCodePage");

        await Driver.SetProperty("Text", "", automationId: "QRValueEntry");
        await Driver.WaitUntil(async () => await Rendered("QRCodeView") == null, "an empty value clears the image");

        await Driver.SetProperty("Text", "https://shinylib.net/controls/", automationId: "QRValueEntry");
        await Driver.WaitUntil(async () => await Rendered("QRCodeView") != null, "the code comes back");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Qr_Size_Slider_Resizes_The_Code()
    {
        await Open("qrcode", "QRCodePage");

        await Driver.SetProperty("Value", "400", automationId: "QRSizeSlider");
        await Driver.WaitForProperty("QRSizeLabel", "Text", "400 px");
        await Driver.WaitForProperty("QRCodeView", "Size", "400");
        await Driver.WaitForProperty("QRCodeView", "WidthRequest", "400");
        await Driver.WaitForProperty("QRCodeView", "HeightRequest", "400");

        await Driver.SetProperty("Value", "300", automationId: "QRSizeSlider");
        await Driver.WaitForProperty("QRCodeView", "Size", "300");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Qr_Error_Correction_Picker_Sets_The_Level()
    {
        await Open("qrcode", "QRCodePage");

        // ErrorCorrectionLevels = Low, Medium, Quartile, High
        await Driver.SetProperty("SelectedIndex", "3", automationId: "QRErrorPicker");
        await Driver.WaitForProperty("QRCodeView", "ErrorCorrection", "High");
        await Driver.WaitUntil(async () => await Rendered("QRCodeView") != null, "still drawn at High");

        await Driver.SetProperty("SelectedIndex", "1", automationId: "QRErrorPicker");
        await Driver.WaitForProperty("QRCodeView", "ErrorCorrection", "Medium");
        await AssertNoAppErrors();
    }


    // ── barcode gallery ────────────────────────────────────────────────────

    [Test]
    public async Task Gallery_Renders_Each_Format()
    {
        await Open("barcodegallery", "BarcodeGalleryPage");

        foreach (var id in new[] { "Code128Barcode", "Ean13Barcode", "AztecBarcode" })
            await Driver.WaitUntil(async () => await Rendered(id) != null, $"{id} is drawn");
    }


    [Test]
    public async Task Invalid_Value_For_The_Format_Renders_Nothing()
    {
        await Open("barcodegallery", "BarcodeGalleryPage");
        await Driver.WaitUntil(async () => await Rendered("Ean13Barcode") != null, "EAN-13 is drawn");

        // EAN-13 is digits only: the renderer refuses and the view shows nothing rather than throwing
        await Driver.SetProperty("Value", "NOT-A-NUMBER", automationId: "Ean13Barcode");
        await Driver.WaitUntil(async () => await Rendered("Ean13Barcode") == null, "the invalid value draws nothing");

        await Driver.SetProperty("Value", "5901234123457", automationId: "Ean13Barcode");
        await Driver.WaitUntil(async () => await Rendered("Ean13Barcode") != null, "a valid value draws again");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Changing_Format_Redraws()
    {
        await Open("barcodegallery", "BarcodeGalleryPage");

        // the Code 128 payload is also valid Code 39
        await Driver.SetProperty("Format", "Code39", automationId: "Code128Barcode");
        await Driver.WaitForProperty("Code128Barcode", "Format", "Code39");
        await Driver.WaitUntil(async () => await Rendered("Code128Barcode") != null, "re-encoded as Code 39");

        await Driver.SetProperty("Format", "Code128", automationId: "Code128Barcode");
        await Driver.WaitForProperty("Code128Barcode", "Format", "Code128");
        await AssertNoAppErrors();
    }
}
