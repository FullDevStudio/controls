namespace Shiny.Blazor.Controls.Dialogs;

public enum DialogKind
{
    Alert,
    Confirm,
    Prompt,
    ActionSheet
}

public class DialogConfig
{
    public DialogKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string OkText { get; set; } = "OK";

    /// <summary>Cancel/secondary button text. Null hides the button (Alert).</summary>
    public string? CancelText { get; set; }

    /// <summary>ActionSheet options, in display order. The chosen one is returned from <c>ActionSheet</c>.</summary>
    public IReadOnlyList<string> Actions { get; set; } = [];

    /// <summary>ActionSheet option to render destructively (red). Must match one of the <see cref="Actions"/>.</summary>
    public string? DestructiveAction { get; set; }

    /// <summary>Prompt placeholder text.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Prompt initial value.</summary>
    public string? InitialValue { get; set; }

    /// <summary>Prompt input type (e.g. "text", "password", "email", "number").</summary>
    public string InputType { get; set; } = "text";

    /// <summary>Maximum length of the Prompt input. <c>null</c> (the default) means no limit.</summary>
    public int? MaxLength { get; set; }

    // --- Appearance ---

    public DialogAnimation Animation { get; set; } = DialogAnimation.Pop;

    /// <summary>Tapping the dimmed backdrop cancels the dialog.</summary>
    public bool DismissOnBackdrop { get; set; } = true;

    /// <summary>Pressing Escape cancels the dialog.</summary>
    public bool DismissOnEscape { get; set; } = true;

    // CSS color strings; null inherits the theme tokens.
    public string? BackgroundColor { get; set; }
    public string? TitleColor { get; set; }
    public string? MessageColor { get; set; }
    public string? OkButtonColor { get; set; }
    public string? OkButtonTextColor { get; set; }
    public string? CancelButtonColor { get; set; }
    public string? CancelButtonTextColor { get; set; }

    /// <summary>ActionSheet option button fill (any CSS colour). Null follows the theme.</summary>
    public string? ActionSheetButtonColor { get; set; }

    /// <summary>ActionSheet option button text (any CSS colour). Null follows the theme.</summary>
    public string? ActionSheetButtonTextColor { get; set; }

    /// <summary>Text of the <see cref="DestructiveAction"/> option (any CSS colour). Null follows the theme's error colour.</summary>
    public string? DestructiveButtonTextColor { get; set; }

    public double CornerRadius { get; set; } = 16;

    /// <summary>Opacity of the dimmed backdrop (0-1).</summary>
    public double BackdropOpacity { get; set; } = 0.45;

    /// <summary>Max width of the dialog card, in pixels.</summary>
    public double MaxWidth { get; set; } = 360;

    /// <summary>Extra CSS class applied to the dialog card for custom styling.</summary>
    public string? CssClass { get; set; }
}

/// <summary>
/// Result of <see cref="IDialogService.Prompt"/>.
/// </summary>
public sealed class PromptResult
{
    public PromptResult(bool ok, string? value)
    {
        this.Ok = ok;
        this.Value = value;
    }

    /// <summary>True when the user confirmed; false when cancelled.</summary>
    public bool Ok { get; }

    /// <summary>True when the user cancelled.</summary>
    public bool Cancelled => !this.Ok;

    /// <summary>The entered text. Null when cancelled.</summary>
    public string? Value { get; }
}
