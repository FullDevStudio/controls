# Barcodes & QR Codes

[← All Shiny Controls](../../README.md)

> Separate NuGet packages: `Shiny.Maui.Controls.Barcodes` / `Shiny.Blazor.Controls.Barcodes`

Pure-managed 1D and 2D barcode renderer powered by ZXing.Net. No SkiaSharp, no `System.Drawing`, AOT-safe on every TFM. MAUI renders to PNG bytes via a built-in PNG encoder and feeds an `Image`. Blazor renders inline SVG by default (crisp at any size) or a PNG `data:` URI. Need raw bytes or markup? Call the static `BarcodeRenderer` directly.

**Supported formats:** QR Code, Aztec, Data Matrix, PDF417, Code 128, Code 39, Code 93, Codabar, EAN-8, EAN-13, UPC-A, UPC-E, ITF.

```xml
xmlns:bc="http://shiny.net/maui/barcodes"
```

```xml
<!-- Any supported 1D/2D barcode -->
<bc:BarcodeView Value="5901234123457"
                Format="Ean13"
                PixelWidth="400"
                PixelHeight="150"
                ForegroundColor="Black"
                BarcodeBackgroundColor="White" />

<!-- QR code shortcut with error correction -->
<bc:QRCodeView Value="https://shinylib.net"
               Size="300"
               ErrorCorrection="High" />
```

```razor
<!-- Blazor: SVG by default; switch to PNG with ImageFormat="BarcodeImageFormat.Png" -->
<BarcodeView Value="5901234123457"
             Format="BarcodeFormat.Ean13"
             PixelWidth="400"
             PixelHeight="150" />

<QRCodeView Value="https://shinylib.net"
            Size="300"
            QRErrorCorrection="QRErrorCorrection.High" />
```

**BarcodeView properties (MAUI):**

| Property | Type | Default | Description |
|---|---|---|---|
| Value | string | "" | Content to encode. Empty clears the image |
| Format | BarcodeFormat | Code128 | Symbology (see list above) |
| PixelWidth | int | 400 | Output bitmap width in pixels |
| PixelHeight | int | 150 | Output bitmap height in pixels |
| MarginPixels | int | 10 | Quiet zone around the symbol (in pixels) |
| ForegroundColor | Color | Black | Bar / module color |
| BarcodeBackgroundColor | Color | White | Background fill |

**QRCodeView additional properties (MAUI):** inherits everything from `BarcodeView`; locks `Format` to `QRCode` and adds:

| Property | Type | Default | Description |
|---|---|---|---|
| Size | int | 300 | Square output edge length in px (sets both `PixelWidth` and `PixelHeight`) |
| ErrorCorrection | QRErrorCorrection | Medium | `Low` / `Medium` / `Quartile` / `High` — higher tolerates more damage at the cost of capacity |

**BarcodeView parameters (Blazor):**

| Parameter | Type | Default | Description |
|---|---|---|---|
| Value | string? | null | Content to encode |
| Format | BarcodeFormat | Code128 | Symbology |
| ImageFormat | BarcodeImageFormat | Svg | `Svg` (inline `<svg>`) or `Png` (`<img>` with `data:` URI) |
| PixelWidth / PixelHeight | int | 400 / 150 | Encoder pixel size (also default CSS size when `CssWidth`/`CssHeight` unset) |
| MarginPixels | int | 10 | Quiet zone in pixels |
| ForegroundColor / BackgroundColor | string | "#000000" / "#FFFFFF" | CSS hex colors |
| CssWidth / CssHeight | string? | null | CSS sizing overrides for the host element (e.g. `"100%"`, `"4cm"`) |
| AltText | string? | null | `alt` attribute when rendered as PNG `<img>` |
| QRErrorCorrection | QRErrorCorrection | Medium | Only honored when `Format=QRCode` |

**QRCodeView (Blazor):** inherits everything from `BarcodeView` and exposes `Size` (default `300`) which sets `PixelWidth`/`PixelHeight`. `Format` is forced to `QRCode`.

**Render directly from code (no view needed):**

```csharp
using Shiny.Controls.Barcodes;

var opts = new BarcodeRenderOptions
{
    PixelWidth = 600,
    PixelHeight = 200,
    Margin = 10,
    ForegroundColor = "#000000",
    BackgroundColor = "#FFFFFF",
    QRErrorCorrection = QRErrorCorrection.High // QR only
};

byte[] png  = BarcodeRenderer.RenderPng("Hello", BarcodeFormat.QRCode, opts);
string svg  = BarcodeRenderer.RenderSvg("Hello", BarcodeFormat.QRCode, opts);
string dataUri = BarcodeRenderer.RenderDataUri("Hello", BarcodeFormat.QRCode, BarcodeImageFormat.Png, opts);
```

**Notes:**
- The PNG encoder is pure managed (zlib stored blocks + CRC32 + Adler32) — no SkiaSharp / `System.Drawing` dependency, ships clean on iOS / Android / Mac Catalyst / Windows / Blazor WebAssembly.
- Blazor: the code is capped at `max-width: 100%`, and without a `CssHeight` it keeps its aspect ratio (`aspect-ratio` + `height: auto`), so a 400px barcode scales down inside a 360px phone instead of overflowing. An explicit `CssWidth`/`CssHeight` still wins.
- SVG output uses a single horizontal-run `<path>` (with `shape-rendering="crispEdges"`), so it scales infinitely without aliasing and stays tiny in DOM size — preferred for Blazor.
- `ErrorCorrection.High` adds ~30% redundancy to a QR code — use it for printed labels, stickers, or anything that might be partially obscured.
- 1D formats (EAN, UPC, Code 128, etc.) require a valid payload for the chosen symbology — invalid input clears the image silently rather than throwing.
