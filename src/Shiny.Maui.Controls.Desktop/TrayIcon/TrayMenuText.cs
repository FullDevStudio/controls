namespace Shiny.Maui.Controls.Desktop.TrayIcon;

/// <summary>
/// Text renderings of pills and progress rows, for menus that can only show a label — the Linux
/// app indicator, whose menu is serialised over DBusMenu and drawn by the desktop shell.
/// </summary>
static class TrayMenuText
{
    public const int BarSegments = 10;

    public static string PillLabel(string label, TrayPill pill)
        => string.IsNullOrEmpty(pill.Text) ? label : $"{label}  [{pill.Text}]";

    /// <summary>A fixed-width bar of filled and empty segments, e.g. "▰▰▰▰▰▰▱▱▱▱".</summary>
    public static string Bar(double value, int segments = BarSegments)
    {
        var filled = (int)Math.Round(Math.Clamp(double.IsNaN(value) ? 0 : value, 0, 1) * segments, MidpointRounding.AwayFromZero);
        return new string('▰', filled) + new string('▱', segments - filled);
    }

    /// <summary>"Label   ▰▰▰▰▰▰▱▱▱▱  62%" — the detail is dropped when it is empty.</summary>
    public static string ProgressLine(TrayProgressItem item)
    {
        var detail = item.DisplayDetail;
        var line = $"{item.Label}   {Bar(item.Value)}";
        return string.IsNullOrEmpty(detail) ? line : $"{line}  {detail}";
    }
}
