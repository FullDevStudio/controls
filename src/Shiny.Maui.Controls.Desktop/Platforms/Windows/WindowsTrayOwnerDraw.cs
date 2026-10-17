using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using static Shiny.Maui.Controls.Desktop.TrayIcon.NativeMethods;
using SDBitmap = System.Drawing.Bitmap;
using SDColor = System.Drawing.Color;
using SDFont = System.Drawing.Font;
using SDFontStyle = System.Drawing.FontStyle;
using SDGraphics = System.Drawing.Graphics;
using SDGraphicsUnit = System.Drawing.GraphicsUnit;
using SDImage = System.Drawing.Image;
using SDRectangle = System.Drawing.Rectangle;
using SDRectangleF = System.Drawing.RectangleF;
using SDSolidBrush = System.Drawing.SolidBrush;
using SDStringAlignment = System.Drawing.StringAlignment;
using SDStringFormat = System.Drawing.StringFormat;
using SDStringFormatFlags = System.Drawing.StringFormatFlags;
using SDSystemFonts = System.Drawing.SystemFonts;
using SDTextRenderingHint = System.Drawing.Text.TextRenderingHint;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

/// <summary>
/// Measures and paints the tray menu rows Win32 cannot draw itself: items carrying a pill, and
/// progress rows. Text goes through GDI with the menu font so it matches the native rows around it;
/// the pill and bar go through GDI+ for antialiased rounded ends. The row background comes from the
/// visual style (uxtheme "MENU") so the hover highlight is the system's own.
/// </summary>
[SupportedOSPlatform("windows")]
static class WindowsTrayOwnerDraw
{
    // Metrics at 96 DPI, scaled per device.
    const int ItemMinHeight = 22;
    const int ItemVerticalPad = 4;
    const int PillGap = 8;
    const int PillHeight = 16;
    const int PillPadding = 6;
    const int AcceleratorGap = 24;
    const int TrailingPad = 22;   // room the native rows keep for a submenu arrow
    const int ProgressWidth = 240;
    const int ProgressTopPad = 5;
    const int ProgressBarGap = 5;
    const int ProgressBarHeight = 6;
    const int ProgressCaptionGap = 4;
    const int ProgressBottomPad = 7;

    /// <summary>
    /// The owner-drawn icon a row carries, decoded once when the menu is built.
    /// </summary>
    public sealed class Row(TrayMenuItemBase item, SDImage? icon) : IDisposable
    {
        public TrayMenuItemBase Item { get; } = item;
        public SDImage? Icon { get; } = icon;
        public void Dispose() => this.Icon?.Dispose();
    }

    public static void Measure(Row row, ref MEASUREITEMSTRUCT mis)
    {
        var hdc = GetDC(IntPtr.Zero);
        try
        {
            var scale = GetDeviceCaps(hdc, LOGPIXELSY) / 96f;
            using var menuFont = SDSystemFonts.MenuFont!;
            var hFont = menuFont.ToHfont();
            var old = SelectObject(hdc, hFont);
            try
            {
                var textLeft = TextLeft(scale);
                switch (row.Item)
                {
                    case TrayProgressItem progress:
                    {
                        var labelH = TextSize(hdc, "Ag").Height;
                        var h = S(ProgressTopPad, scale) + labelH + S(ProgressBarGap, scale) + S(ProgressBarHeight, scale) + S(ProgressBottomPad, scale);
                        if (progress.HasCaption)
                            h += S(ProgressCaptionGap, scale) + CaptionFontHeight(menuFont, scale);
                        mis.itemWidth = (uint)Math.Max(S(ProgressWidth, scale), textLeft + TextSize(hdc, progress.Label).Width + S(PillGap * 6, scale));
                        mis.itemHeight = (uint)h;
                        break;
                    }
                    case TrayMenuItem mi:
                    {
                        var label = TextSize(hdc, mi.Label);
                        var width = textLeft + label.Width + S(TrailingPad, scale);
                        if (mi.Pill != null)
                            width += S(PillGap, scale) + PillWidth(mi.Pill.Text, scale);
                        if (!string.IsNullOrEmpty(mi.Accelerator))
                            width += S(AcceleratorGap, scale) + TextSize(hdc, mi.Accelerator).Width;
                        mis.itemWidth = (uint)width;
                        mis.itemHeight = (uint)Math.Max(S(ItemMinHeight, scale), label.Height + S(ItemVerticalPad * 2, scale));
                        break;
                    }
                }
            }
            finally
            {
                SelectObject(hdc, old);
                DeleteObject(hFont);
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    public static void Draw(Row row, IntPtr theme, ref DRAWITEMSTRUCT dis)
    {
        var hdc = dis.hDC;
        var rc = dis.rcItem;
        var scale = GetDeviceCaps(hdc, LOGPIXELSY) / 96f;
        var disabled = (dis.itemState & (ODS_GRAYED | ODS_DISABLED)) != 0;
        // a progress row is informational: it never takes the hover highlight
        var selected = row.Item is not TrayProgressItem && (dis.itemState & ODS_SELECTED) != 0;
        if (row.Item is TrayProgressItem p) disabled = !p.IsEnabled;

        var state = (selected, disabled) switch
        {
            (true, true) => MPI_DISABLEDHOT,
            (true, false) => MPI_HOT,
            (false, true) => MPI_DISABLED,
            _ => MPI_NORMAL
        };

        int textColor;
        if (theme != IntPtr.Zero)
        {
            DrawThemeBackground(theme, hdc, MENU_POPUPBACKGROUND, 0, ref rc, IntPtr.Zero);
            if (selected)
                DrawThemeBackground(theme, hdc, MENU_POPUPITEM, state, ref rc, IntPtr.Zero);
            if (GetThemeColor(theme, MENU_POPUPITEM, state, TMT_TEXTCOLOR, out textColor) != 0)
                textColor = GetSysColor(disabled ? COLOR_GRAYTEXT : COLOR_MENUTEXT);
        }
        else
        {
            // classic (unthemed) menus: the system highlight colours
            FillRect(hdc, ref rc, GetSysColorBrush(selected ? COLOR_HIGHLIGHT : COLOR_MENU));
            textColor = GetSysColor(disabled ? COLOR_GRAYTEXT : selected ? COLOR_HIGHLIGHTTEXT : COLOR_MENUTEXT);
        }
        var secondaryColor = disabled ? textColor : GetSysColor(COLOR_GRAYTEXT);

        using var menuFont = SDSystemFonts.MenuFont!;
        var hFont = menuFont.ToHfont();
        var oldFont = SelectObject(hdc, hFont);
        SetBkMode(hdc, TRANSPARENT);
        try
        {
            var textLeft = rc.Left + TextLeft(scale);
            var right = rc.Right - S(TrailingPad, scale);
            switch (row.Item)
            {
                case TrayProgressItem progress:
                    DrawProgress(hdc, progress, menuFont, textLeft, rc, scale, textColor, secondaryColor, disabled);
                    break;
                case TrayMenuItem mi:
                    DrawItem(hdc, row, mi, textLeft, right, rc, scale, textColor);
                    break;
            }
        }
        finally
        {
            SelectObject(hdc, oldFont);
            DeleteObject(hFont);
        }
    }

    static void DrawItem(IntPtr hdc, Row row, TrayMenuItem mi, int textLeft, int right, RECT rc, float scale, int textColor)
    {
        if (row.Icon != null)
        {
            var size = Math.Max(16, GetSystemMetrics(SM_CXMENUCHECK));
            using var g = SDGraphics.FromHdc(hdc);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var x = rc.Left + (TextLeft(scale) - size) / 2 - S(2, scale);
            var y = rc.Top + (rc.Bottom - rc.Top - size) / 2;
            g.DrawImage(row.Icon, new SDRectangle(x, y, size, size));
        }

        // accelerator right-aligned, label in what remains, pill straight after the label
        var labelRight = right;
        if (!string.IsNullOrEmpty(mi.Accelerator))
        {
            var accRect = new RECT { Left = textLeft, Top = rc.Top, Right = right, Bottom = rc.Bottom };
            SetTextColor(hdc, textColor);
            DrawText(hdc, mi.Accelerator, -1, ref accRect, DT_RIGHT | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
            labelRight = right - TextSize(hdc, mi.Accelerator).Width - S(AcceleratorGap, scale);
        }

        var pillW = mi.Pill != null ? PillWidth(mi.Pill.Text, scale) + S(PillGap, scale) : 0;
        var labelWidth = Math.Min(TextSize(hdc, mi.Label).Width, Math.Max(0, labelRight - textLeft - pillW));
        var labelRect = new RECT { Left = textLeft, Top = rc.Top, Right = textLeft + labelWidth, Bottom = rc.Bottom };
        SetTextColor(hdc, textColor);
        DrawText(hdc, mi.Label, -1, ref labelRect, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);

        if (mi.Pill != null)
        {
            using var g = SDGraphics.FromHdc(hdc);
            var h = S(PillHeight, scale);
            var pillRect = new SDRectangleF(textLeft + labelWidth + S(PillGap, scale), rc.Top + (rc.Bottom - rc.Top - h) / 2f, PillWidth(mi.Pill.Text, scale), h);
            DrawPill(g, pillRect, mi.Pill, scale, mi.IsEnabled);
        }
    }

    static void DrawProgress(IntPtr hdc, TrayProgressItem item, SDFont menuFont, int left, RECT rc, float scale, int textColor, int secondaryColor, bool disabled)
    {
        var right = rc.Right - S(TrailingPad, scale) / 2;
        var labelH = TextSize(hdc, "Ag").Height;
        var top = rc.Top + S(ProgressTopPad, scale);

        // label row: label left, detail right
        var detail = item.DisplayDetail;
        var detailWidth = string.IsNullOrEmpty(detail) ? 0 : TextSize(hdc, detail).Width;
        if (detailWidth > 0)
        {
            var dr = new RECT { Left = left, Top = top, Right = right, Bottom = top + labelH };
            SetTextColor(hdc, secondaryColor);
            DrawText(hdc, detail, -1, ref dr, DT_RIGHT | DT_SINGLELINE | DT_NOPREFIX);
        }
        var lr = new RECT { Left = left, Top = top, Right = right - (detailWidth > 0 ? detailWidth + S(PillGap, scale) : 0), Bottom = top + labelH };
        SetTextColor(hdc, textColor);
        DrawText(hdc, item.Label, -1, ref lr, DT_LEFT | DT_SINGLELINE | DT_END_ELLIPSIS);

        var barTop = top + labelH + S(ProgressBarGap, scale);
        var barH = S(ProgressBarHeight, scale);
        var width = Math.Max(0, right - left);
        using (var g = SDGraphics.FromHdc(hdc))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new SDRectangleF(left, barTop, width, barH);
            using (var trackBrush = new SDSolidBrush(SDColor.FromArgb(40, 0, 0, 0)))
                FillRounded(g, trackBrush, track);
            if (item.Value > 0)
            {
                var fillW = Math.Min(width, Math.Max(barH, (float)(width * item.Value)));
                var tone = ToneColor(item.Tone);
                using var fillBrush = new SDSolidBrush(disabled ? SDColor.FromArgb(100, tone) : tone);
                FillRounded(g, fillBrush, new SDRectangleF(left, barTop, fillW, barH));
            }

            if (item.HasCaption)
            {
                // GDI+ text for the smaller caption font; GDI's menu font has no small variant to hand
                using var captionFont = CaptionFont(menuFont, scale);
                using var brush = new SDSolidBrush(FromColorRef(secondaryColor));
                g.TextRenderingHint = SDTextRenderingHint.ClearTypeGridFit;
                using var fmt = new SDStringFormat { Trimming = System.Drawing.StringTrimming.EllipsisCharacter, FormatFlags = SDStringFormatFlags.NoWrap };
                var captionTop = barTop + barH + S(ProgressCaptionGap, scale);
                g.DrawString(item.Caption, captionFont, brush, new SDRectangleF(left, captionTop, width, CaptionFontHeight(menuFont, scale) + 2), fmt);
            }
        }
    }

    static void DrawPill(SDGraphics g, SDRectangleF rect, TrayPill pill, float scale, bool enabled)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = SDTextRenderingHint.AntiAliasGridFit;
        var tone = ToneColor(pill.Tone);
        using (var bg = new SDSolidBrush(enabled ? tone : SDColor.FromArgb(110, tone)))
            FillRounded(g, bg, rect);
        using var font = PillFont(scale);
        using var fg = new SDSolidBrush(SDColor.White);
        using var fmt = new SDStringFormat
        {
            Alignment = SDStringAlignment.Center,
            LineAlignment = SDStringAlignment.Center,
            FormatFlags = SDStringFormatFlags.NoWrap
        };
        g.DrawString(pill.Text, font, fg, rect, fmt);
    }

    /// <summary>Solid pill / bar colours, chosen to carry white text on a light Windows menu.</summary>
    static SDColor ToneColor(TrayTone tone) => tone switch
    {
        TrayTone.Accent => SDColor.FromArgb(0x00, 0x67, 0xC0),
        TrayTone.Success => SDColor.FromArgb(0x0F, 0x7B, 0x0F),
        TrayTone.Warning => SDColor.FromArgb(0xC2, 0x5E, 0x00),
        TrayTone.Danger => SDColor.FromArgb(0xC4, 0x2B, 0x1C),
        _ => SDColor.FromArgb(0x6E, 0x6E, 0x6E)
    };

    static void FillRounded(SDGraphics g, SDSolidBrush brush, SDRectangleF r)
    {
        var radius = Math.Min(r.Height, r.Width) / 2f;
        if (radius <= 0) return;
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    static int PillWidth(string text, float scale)
    {
        using var font = PillFont(scale);
        using var bmp = new SDBitmap(1, 1);
        using var g = SDGraphics.FromImage(bmp);
        using var fmt = new SDStringFormat(SDStringFormat.GenericTypographic);
        var w = g.MeasureString(text, font, int.MaxValue, fmt).Width;
        return (int)Math.Ceiling(Math.Max(S(PillHeight, scale), w + S(PillPadding * 2, scale)));
    }

    static SDFont PillFont(float scale)
        => new(SDSystemFonts.MenuFont!.FontFamily, 10f * scale, SDFontStyle.Bold, SDGraphicsUnit.Pixel);

    static SDFont CaptionFont(SDFont menuFont, float scale)
        => new(menuFont.FontFamily, 11f * scale, SDFontStyle.Regular, SDGraphicsUnit.Pixel);

    static int CaptionFontHeight(SDFont menuFont, float scale)
    {
        using var f = CaptionFont(menuFont, scale);
        return (int)Math.Ceiling(f.GetHeight(96f * scale));
    }

    /// <summary>Where text starts, past the check / icon gutter, matching native themed rows.</summary>
    static int TextLeft(float scale) => GetSystemMetrics(SM_CXMENUCHECK) * 2 + S(2, scale);

    static (int Width, int Height) TextSize(IntPtr hdc, string text)
    {
        var r = new RECT();
        DrawText(hdc, string.IsNullOrEmpty(text) ? " " : text, -1, ref r, DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX);
        return (r.Right - r.Left, r.Bottom - r.Top);
    }

    static int S(int value, float scale) => (int)Math.Round(value * scale);

    static SDColor FromColorRef(int colorRef)
        => SDColor.FromArgb(colorRef & 0xFF, (colorRef >> 8) & 0xFF, (colorRef >> 16) & 0xFF);
}
