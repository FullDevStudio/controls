using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Shiny.Blazor.Controls.QuickEntry;

/// <summary>
/// Renders the quick entry popup and the screen-edge glow. Place exactly one, near the root of your
/// layout — <see cref="IQuickEntryService"/> only holds state, and this is what puts it on screen.
/// </summary>
public partial class QuickEntryHost : ComponentBase, IDisposable
{
    PromptView? prompt;
    bool wasOpen;

    [Inject] public IQuickEntryService Service { get; set; } = null!;

    QuickEntryOptions Options => this.Service.Options;

    string PlacementClass => this.Options.Placement switch
    {
        QuickEntryPlacement.BottomCenter => "is-bottom",
        QuickEntryPlacement.Center => "is-center",
        _ => "is-top"
    };

    string CardStyle
    {
        get
        {
            var invariant = CultureInfo.InvariantCulture;
            var width = this.Options.Width.ToString(invariant);
            var maxHeight = this.Options.MaxHeight.ToString(invariant);
            var top = (this.Options.TopMarginRatio * 100).ToString(invariant);
            var bottom = (this.Options.BottomMarginRatio * 100).ToString(invariant);

            return $"--shiny-qe-width:{width}px;--shiny-qe-max-height:{maxHeight}px;--shiny-qe-top:{top}%;--shiny-qe-bottom:{bottom}%;";
        }
    }

    string GlowStyle
    {
        get
        {
            var invariant = CultureInfo.InvariantCulture;
            var glow = this.Options.Glow;
            var stops = String.Join(", ", glow.Palette);

            // Repeat the first stop at the end so the conic gradient has no seam where it wraps.
            if (glow.Palette.Count > 1)
                stops += ", " + glow.Palette[0];

            // The floor of the breath, so the pulse keyframe can be written once in CSS and driven
            // entirely by these two numbers.
            var depth = Math.Clamp(glow.PulseDepth, 0d, 1d);
            var low = (glow.Intensity * (1d - depth)).ToString(invariant);
            var pulse = glow.PulseSeconds <= 0 || depth <= 0 ? "0s" : glow.PulseSeconds.ToString(invariant) + "s";

            return
                $"--shiny-glow-thickness:{glow.Thickness.ToString(invariant)}px;" +
                $"--shiny-glow-stops:{stops};" +
                $"--shiny-glow-lap:{glow.LapSeconds.ToString(invariant)}s;" +
                $"--shiny-glow-opacity:{glow.Intensity.ToString(invariant)};" +
                $"--shiny-glow-opacity-low:{low};" +
                $"--shiny-glow-pulse:{pulse};" +
                $"--shiny-glow-fade:{glow.FadeDuration.TotalMilliseconds.ToString(invariant)}ms;";
        }
    }

    protected override void OnInitialized() => this.Service.Changed += this.OnServiceChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Focus on the transition into open, not on every render — re-focusing mid-typing would move
        // the caret back to the end on each keystroke.
        if (this.Service.IsOpen && !this.wasOpen && this.Options.FocusOnShow && this.prompt is not null)
            await this.prompt.FocusAsync();

        this.wasOpen = this.Service.IsOpen;
    }

    void OnServiceChanged(object? sender, EventArgs e) => this.InvokeAsync(this.StateHasChanged);

    void OnScrimClick()
    {
        if (this.Options.DismissOnScrimTap)
            this.Service.Hide();
    }

    async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key != "Escape" || !this.Options.DismissOnEscape)
            return;

        // The prompt gets first refusal, so the first Escape backs out of its own state and only an
        // Escape it declines closes the popup.
        if (this.prompt is not null)
        {
            // An Escape typed in the prompt's own field was already handled there and bubbled up here;
            // asking again would peel a second layer (or close) on one keypress.
            var consumed = this.prompt.TakeKeyEscapeOutcome() ?? await this.prompt.HandleEscapeAsync();
            if (consumed)
                return;
        }

        this.Service.Hide();
    }

    Task OnPromptTextChanged(string text)
    {
        this.Service.Prompt.Text = text;
        return Task.CompletedTask;
    }

    Task OnPromptSubmitted(PromptSubmittedEventArgs e)
    {
        this.Service.Prompt.RaiseSubmitted(e.Text, e.Suggestion);
        return Task.CompletedTask;
    }

    Task OnPromptCancelled()
    {
        this.Service.Prompt.RaiseCancelled();
        return Task.CompletedTask;
    }

    public void Dispose() => this.Service.Changed -= this.OnServiceChanged;
}
