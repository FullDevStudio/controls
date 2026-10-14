using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Shiny.Blazor.Controls.Cells;

public class EntryCell : CellBase
{
    protected override bool AccessoryHandlesClicks => true;

    [Parameter] public string? ValueText { get; set; }
    [Parameter] public EventCallback<string> ValueTextChanged { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public bool IsPassword { get; set; }
    [Parameter] public int MaxLength { get; set; } = -1;
    [Parameter] public string TextAlignment { get; set; } = "right";
    [Parameter] public string? ValueTextColor { get; set; }
    [Parameter] public double ValueTextFontSize { get; set; } = -1;

    /// <summary>
    /// Input mask — <c>#</c> is a digit slot, everything else is a literal inserted as the user types.
    /// <see cref="ValueText"/> stays the raw digits; the field shows the formatted value.
    /// </summary>
    [Parameter] public string? Mask { get; set; }

    /// <summary>
    /// When false, the browser's autofill, autocorrect, auto-capitalisation and spell check are all
    /// switched off, and the common password managers are told to keep out.
    /// </summary>
    [Parameter] public bool IsAutoCompleteEnabled { get; set; } = true;

    [Parameter] public EventCallback<string?> Completed { get; set; }

    /// <summary>The masked display value. Empty when no <see cref="Mask"/> is set.</summary>
    public string FormattedValueText => string.IsNullOrEmpty(Mask)
        ? string.Empty
        : TextEntryMaskHelper.ApplyMask(ValueText, Mask);

    string DisplayText => string.IsNullOrEmpty(Mask) ? ValueText ?? "" : FormattedValueText;

    protected override Task OnTapped() => Task.CompletedTask;

    async Task HandleInput(ChangeEventArgs e)
    {
        var v = e.Value as string ?? string.Empty;

        // With a mask the raw value is what the caller binds to; the literals are re-derived for
        // display, so whatever the browser put in the box is stripped back first.
        if (!string.IsNullOrEmpty(Mask))
        {
            var raw = TextEntryMaskHelper.StripMask(v, Mask);
            var maxRaw = TextEntryMaskHelper.CalculateRawMaxLength(Mask);
            if (raw.Length > maxRaw)
                raw = raw[..maxRaw];
            v = raw;
        }

        ValueText = v;
        if (ValueTextChanged.HasDelegate)
            await ValueTextChanged.InvokeAsync(v);
    }

    async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && Completed.HasDelegate)
            await Completed.InvokeAsync(ValueText);
    }

    protected override void BuildAccessory(RenderTreeBuilder builder, int sequence)
    {
        var color = ValueTextColor ?? ResolveValueColor();
        var size = ResolveDouble(ValueTextFontSize, ParentTableView?.CellValueTextFontSize ?? -1, 14);
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(color)) sb.Append("color:").Append(color).Append(';');
        sb.Append("font-size:").Append(size).Append("px;");
        sb.Append("text-align:").Append(TextAlignment).Append(';');

        builder.OpenElement(sequence, "input");
        builder.AddAttribute(sequence + 1, "type", IsPassword ? "password" : "text");
        builder.AddAttribute(sequence + 2, "class", "shiny-tv-input");
        builder.AddAttribute(sequence + 3, "style", sb.ToString());
        builder.AddAttribute(sequence + 4, "value", DisplayText);
        builder.AddAttribute(sequence + 5, "placeholder", Placeholder ?? "");

        // A mask fixes both the accepted characters and the rendered length.
        if (!string.IsNullOrEmpty(Mask))
        {
            builder.AddAttribute(sequence + 6, "maxlength", Mask.Length);
            builder.AddAttribute(sequence + 7, "inputmode", "numeric");
        }
        else if (MaxLength > 0)
        {
            builder.AddAttribute(sequence + 6, "maxlength", MaxLength);
        }

        // autocomplete="off" alone is widely ignored by Chrome and by the password managers, so the
        // opt-out has to be spelled out - same set TextEntry uses.
        if (!IsAutoCompleteEnabled)
        {
            builder.AddAttribute(sequence + 8, "autocomplete", IsPassword ? "new-password" : "off");
            builder.AddAttribute(sequence + 9, "autocorrect", "off");
            builder.AddAttribute(sequence + 10, "autocapitalize", "off");
            builder.AddAttribute(sequence + 11, "spellcheck", "false");
            builder.AddAttribute(sequence + 12, "data-lpignore", "true");
            builder.AddAttribute(sequence + 13, "data-1p-ignore", "true");
            builder.AddAttribute(sequence + 14, "data-form-type", "other");
        }

        builder.AddAttribute(sequence + 15, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, HandleInput));
        builder.AddAttribute(sequence + 16, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, HandleKeyDown));
        builder.CloseElement();
    }
}
