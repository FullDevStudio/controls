using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Shiny.Blazor.Controls.QuickEntry;

/// <summary>
/// An assistant-style prompt bar — animated orb, single-line prompt, and an expanding area beneath
/// it for suggestions and a response.
/// </summary>
/// <remarks>
/// Deliberately AI-shaped but AI-agnostic: it raises <see cref="Submitted"/> and leaves the request
/// to you. Push results back by setting <see cref="IsBusy"/> while you work and
/// <see cref="ResponseContent"/> when you have something to show. Usable on an ordinary page as well
/// as inside <c>QuickEntryHost</c> — the popup is just where it usually lives.
/// </remarks>
public partial class PromptView : ComponentBase, IDisposable
{
    ElementReference field;
    int highlightIndex = -1;
    bool preventDefault;
    INotifyCollectionChanged? observedSuggestions;
    readonly List<PromptTool> attachedLeadingTools = new();
    readonly List<PromptTool> attachedTrailingTools = new();
    INotifyCollectionChanged? observedLeadingTools;
    INotifyCollectionChanged? observedTrailingTools;
    string? lastResponse;
    RenderFragment? lastResponseContent;
    bool responseSeen;

    [Inject] IServiceProvider Services { get; set; } = null!;

    [Parameter] public string? Class { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? Attributes { get; set; }

    /// <summary>The prompt text. Two-way via <c>@bind-Text</c>.</summary>
    [Parameter] public string Text { get; set; } = String.Empty;

    [Parameter] public EventCallback<string> TextChanged { get; set; }

    /// <summary>Placeholder shown in the empty prompt.</summary>
    [Parameter] public string Placeholder { get; set; } = "Ask anything…";

    /// <summary>Spins the orb, shows a spinner and swaps submit for a stop button.</summary>
    [Parameter] public bool IsBusy { get; set; }

    /// <summary>Status line shown while busy and no response has arrived.</summary>
    [Parameter] public string BusyText { get; set; } = "Thinking…";

    /// <summary>Leading glyph in place of the animated orb — an emoji or an icon-font character.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Arbitrary markup in the leading slot. Wins over <see cref="Icon"/>.</summary>
    [Parameter] public RenderFragment? IconContent { get; set; }

    /// <summary>Show the leading slot at all. Default true.</summary>
    [Parameter] public bool ShowIcon { get; set; } = true;

    /// <summary>Size of the orb or icon in CSS pixels. Default 26.</summary>
    [Parameter] public double IconSize { get; set; } = 26;

    /// <summary>
    /// Suggestion rows. <see cref="PromptSuggestion"/> renders with the built-in template; any other
    /// type needs <see cref="SuggestionTemplate"/>. Honours <see cref="INotifyCollectionChanged"/>,
    /// so an <c>ObservableCollection</c> updated as the user types behaves like autocomplete.
    /// </summary>
    [Parameter] public IEnumerable? Suggestions { get; set; }

    /// <summary>Render your own suggestion rows.</summary>
    [Parameter] public RenderFragment<object>? SuggestionTemplate { get; set; }

    /// <summary>How many suggestion rows to render. Default 6 — it is a HUD, not a list view.</summary>
    [Parameter] public int MaxVisibleSuggestions { get; set; } = 6;

    /// <summary>Arbitrary content for the expanding dropdown area. Renders above the suggestions.</summary>
    [Parameter] public RenderFragment? DropdownContent { get; set; }

    /// <summary>
    /// Fixed height for the dropdown in CSS pixels. Null (the default) sizes it to its content; a
    /// value pins it and scrolls, which is what you want for a list that changes length as the user
    /// types and would otherwise make the popup jump under the pointer.
    /// </summary>
    [Parameter] public double? DropdownHeight { get; set; }

    /// <summary>The answer as plain text. For rich content use <see cref="ResponseContent"/>.</summary>
    [Parameter] public string? Response { get; set; }

    /// <summary>The answer as markup — a rendered markdown component, a chat transcript, anything.</summary>
    [Parameter] public RenderFragment? ResponseContent { get; set; }

    /// <summary>Optional strip along the bottom — a model picker, a keyboard legend.</summary>
    [Parameter] public RenderFragment? Footer { get; set; }

    /// <summary>
    /// Tools docked beside the orb, at the leading edge of the prompt row. An
    /// <c>ObservableCollection</c> is honoured, so a tool added later shows up without a re-render
    /// of the parent.
    /// </summary>
    [Parameter] public IEnumerable<PromptTool>? LeadingTools { get; set; }

    /// <summary>Tools docked at the trailing edge, before the built-in microphone and submit buttons.</summary>
    [Parameter] public IEnumerable<PromptTool>? TrailingTools { get; set; }

    /// <summary>Show the microphone button. Default false — there is no speech engine in this package.</summary>
    [Parameter] public bool ShowMicrophone { get; set; }

    /// <summary>Show the submit / stop button. Default true.</summary>
    [Parameter] public bool ShowSubmitButton { get; set; } = true;

    /// <summary>Empty the prompt after a successful submit. Default true.</summary>
    [Parameter] public bool ClearOnSubmit { get; set; } = true;

    /// <summary>Card width in CSS pixels. Null stretches to the container.</summary>
    [Parameter] public double? Width { get; set; }

    /// <summary>Raised on submit — Enter, the submit button, or picking a suggestion.</summary>
    [Parameter] public EventCallback<PromptSubmittedEventArgs> Submitted { get; set; }

    /// <summary>Raised when a suggestion is chosen, before <see cref="Submitted"/>.</summary>
    [Parameter] public EventCallback<PromptSubmittedEventArgs> SuggestionSelected { get; set; }

    /// <summary>Raised when the stop button is pressed while busy. Cancel your request here.</summary>
    [Parameter] public EventCallback Cancelled { get; set; }

    /// <summary>Raised by the microphone button.</summary>
    [Parameter] public EventCallback Microphone { get; set; }

    /// <summary>
    /// Raised whenever the response area changes — <see cref="Response"/> or
    /// <see cref="ResponseContent"/>, set or cleared. A tool that reacts to the answer arriving
    /// listens here.
    /// </summary>
    public event EventHandler? ResponseChanged;

    /// <summary>The index of the keyboard-highlighted suggestion, or -1 when the prompt itself has focus.</summary>
    public int HighlightedIndex => this.highlightIndex;

    internal List<object> VisibleSuggestions { get; private set; } = new();

    bool HasBody =>
        this.DropdownContent is not null
        || this.VisibleSuggestions.Count > 0
        || this.ResponseContent is not null
        || !String.IsNullOrEmpty(this.Response)
        || this.Footer is not null
        || (this.IsBusy && this.ResponseContent is null && String.IsNullOrEmpty(this.Response));

    string RootStyle
    {
        get
        {
            var width = this.Width is null
                ? null
                : $"width:{this.Width.Value.ToString(CultureInfo.InvariantCulture)}px;";

            return $"{width}--shiny-prompt-icon-size:{this.IconSize.ToString(CultureInfo.InvariantCulture)}px;";
        }
    }

    /// <summary>
    /// No height at all when unset, so the dropdown shrink-wraps its content. A height turns it into
    /// a scroller — the two cannot both be expressed by one rule, which is why this is a style rather
    /// than a class.
    /// </summary>
    string? DropdownStyle => this.DropdownHeight is null
        ? null
        : $"height:{this.DropdownHeight.Value.ToString(CultureInfo.InvariantCulture)}px;overflow-y:auto;";

    protected override void OnParametersSet()
    {
        this.RebuildSuggestions();
        this.Observe(this.Suggestions);
        this.SyncTools();
        this.RaiseResponseChanged();
    }

    // ---- Tools -------------------------------------------------------------------------------

    // Internal rather than private so the behaviour OnParametersSet drives can be pinned without a
    // renderer — the same reason this project suppresses BL0005.
    internal void SyncTools()
    {
        this.Observe(this.LeadingTools, ref this.observedLeadingTools, this.OnLeadingToolsChanged);
        this.Observe(this.TrailingTools, ref this.observedTrailingTools, this.OnTrailingToolsChanged);

        this.SyncTools(this.LeadingTools, this.attachedLeadingTools);
        this.SyncTools(this.TrailingTools, this.attachedTrailingTools);
    }

    void Observe(
        IEnumerable<PromptTool>? tools,
        ref INotifyCollectionChanged? observed,
        NotifyCollectionChangedEventHandler handler
    )
    {
        var incoming = tools as INotifyCollectionChanged;
        if (ReferenceEquals(observed, incoming))
            return;

        if (observed is not null)
            observed.CollectionChanged -= handler;

        observed = incoming;

        if (observed is not null)
            observed.CollectionChanged += handler;
    }

    /// <summary>
    /// Reconciles what is attached against what is in the collection now. Diffed rather than torn
    /// down and rebuilt because <see cref="OnParametersSet"/> runs on every render of the parent —
    /// re-attaching a tool that never left would double whatever it subscribed to.
    /// </summary>
    void SyncTools(IEnumerable<PromptTool>? tools, List<PromptTool> attached)
    {
        var current = tools?.ToList() ?? new List<PromptTool>();

        foreach (var gone in attached.Where(t => !current.Contains(t)).ToList())
            gone.InternalDetach();

        foreach (var added in current.Where(t => !attached.Contains(t)))
            added.InternalAttach(this, this.Services);

        attached.Clear();
        attached.AddRange(current);
    }

    void OnLeadingToolsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => this.InvokeAsync(() =>
        {
            this.SyncTools(this.LeadingTools, this.attachedLeadingTools);
            this.StateHasChanged();
        });

    void OnTrailingToolsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => this.InvokeAsync(() =>
        {
            this.SyncTools(this.TrailingTools, this.attachedTrailingTools);
            this.StateHasChanged();
        });

    async Task OnToolClickAsync(PromptTool tool)
    {
        await tool.InternalClickAsync();
        this.StateHasChanged();
    }

    /// <summary>Re-render after a tool has changed its own appearance from off the renderer.</summary>
    internal Task RefreshToolsAsync() => this.InvokeAsync(this.StateHasChanged);

    internal void RaiseResponseChanged()
    {
        var changed = !String.Equals(this.lastResponse, this.Response, StringComparison.Ordinal)
            || !ReferenceEquals(this.lastResponseContent, this.ResponseContent);

        this.lastResponse = this.Response;
        this.lastResponseContent = this.ResponseContent;

        // The first pass is the initial parameter set, not a change — a tool reads the current
        // answer in OnAttached, and firing here would make AutoSpeak read whatever was already there.
        if (!this.responseSeen)
        {
            this.responseSeen = true;
            return;
        }

        if (changed)
            this.ResponseChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (this.observedSuggestions is not null)
            this.observedSuggestions.CollectionChanged -= this.OnSuggestionsChanged;

        if (this.observedLeadingTools is not null)
            this.observedLeadingTools.CollectionChanged -= this.OnLeadingToolsChanged;

        if (this.observedTrailingTools is not null)
            this.observedTrailingTools.CollectionChanged -= this.OnTrailingToolsChanged;

        this.SyncTools(null, this.attachedLeadingTools);
        this.SyncTools(null, this.attachedTrailingTools);
    }

    void Observe(IEnumerable? suggestions)
    {
        if (ReferenceEquals(this.observedSuggestions, suggestions))
            return;

        if (this.observedSuggestions is not null)
            this.observedSuggestions.CollectionChanged -= this.OnSuggestionsChanged;

        this.observedSuggestions = suggestions as INotifyCollectionChanged;
        if (this.observedSuggestions is not null)
            this.observedSuggestions.CollectionChanged += this.OnSuggestionsChanged;
    }

    void OnSuggestionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => this.InvokeAsync(() =>
        {
            this.RebuildSuggestions();
            this.StateHasChanged();
        });

    void RebuildSuggestions()
    {
        var rows = new List<object>();
        if (this.Suggestions is not null)
        {
            var max = Math.Max(0, this.MaxVisibleSuggestions);
            foreach (var item in this.Suggestions)
            {
                if (rows.Count >= max)
                    break;
                if (item is null)
                    continue;

                rows.Add(item);
            }
        }

        this.VisibleSuggestions = rows;
        if (this.highlightIndex >= rows.Count)
            this.highlightIndex = -1;
    }

    async Task OnInput(ChangeEventArgs e)
    {
        this.Text = e.Value?.ToString() ?? String.Empty;
        await this.TextChanged.InvokeAsync(this.Text);

        // A new prompt invalidates whatever row was highlighted for the old one.
        this.highlightIndex = -1;
    }

    internal async Task OnKeyDown(KeyboardEventArgs e)
    {
        // Arrow keys move the caret in a text input, so they have to be cancelled to navigate the
        // list instead. Everything else must stay cancellable-free or typing breaks.
        this.preventDefault = e.Key is "ArrowDown" or "ArrowUp";

        switch (e.Key)
        {
            case "ArrowDown":
                this.MoveHighlight(1);
                break;

            case "ArrowUp":
                this.MoveHighlight(-1);
                break;

            case "Enter":
                await this.SubmitAsync();
                break;

            case "Escape":
                // Recorded before the first await: the same keydown bubbles on to a host (QuickEntryHost)
                // that would otherwise ask again, find nothing left to peel and close the popup.
                this.keyEscapeOutcome = this.WillConsumeEscape;
                await this.HandleEscapeAsync();
                break;
        }
    }


    bool? keyEscapeOutcome;

    /// <summary>Whether <see cref="HandleEscapeAsync"/> would consume an Escape right now.</summary>
    internal bool WillConsumeEscape
        => this.IsBusy
           || this.highlightIndex >= 0
           || this.ResponseContent is not null
           || !String.IsNullOrEmpty(this.Response)
           || !String.IsNullOrEmpty(this.Text);

    /// <summary>
    /// For a host the keydown bubbles to: whether the prompt already handled this Escape from its own
    /// field — true consumed, false declined — or null when the key never reached the field. Reading it
    /// clears it, so a stale answer cannot leak into the next keypress.
    /// </summary>
    internal bool? TakeKeyEscapeOutcome()
    {
        var outcome = this.keyEscapeOutcome;
        this.keyEscapeOutcome = null;
        return outcome;
    }

    /// <summary>
    /// Escape peels one layer of state at a time and only bubbles — letting the host close the popup —
    /// once there is nothing left to back out of.
    /// </summary>
    /// <returns>True when the key was consumed here.</returns>
    public async Task<bool> HandleEscapeAsync()
    {
        if (this.IsBusy)
        {
            await this.Cancelled.InvokeAsync();
            return true;
        }

        if (this.highlightIndex >= 0)
        {
            this.highlightIndex = -1;
            this.StateHasChanged();
            return true;
        }

        if (this.ResponseContent is not null || !String.IsNullOrEmpty(this.Response))
        {
            this.Response = null;
            this.ResponseContent = null;
            this.StateHasChanged();
            return true;
        }

        if (!String.IsNullOrEmpty(this.Text))
        {
            this.Text = String.Empty;
            await this.TextChanged.InvokeAsync(this.Text);
            return true;
        }

        return false;
    }

    void MoveHighlight(int delta)
    {
        if (this.VisibleSuggestions.Count == 0)
            return;

        var next = this.highlightIndex + delta;
        if (next < -1)
            next = this.VisibleSuggestions.Count - 1;
        else if (next >= this.VisibleSuggestions.Count)
            next = -1;

        this.highlightIndex = next;
    }

    /// <summary>Submit the current prompt. Same path as pressing Enter.</summary>
    public async Task SubmitAsync()
    {
        if (this.IsBusy)
        {
            await this.Cancelled.InvokeAsync();
            return;
        }

        if (this.highlightIndex >= 0 && this.highlightIndex < this.VisibleSuggestions.Count)
        {
            await this.ChooseAsync(this.VisibleSuggestions[this.highlightIndex]);
            return;
        }

        if (String.IsNullOrWhiteSpace(this.Text))
            return;

        await this.RaiseSubmitAsync(this.Text, null);
    }

    async Task ChooseAsync(object item)
    {
        var text = item is PromptSuggestion s ? s.Text : item.ToString() ?? String.Empty;
        await this.SuggestionSelected.InvokeAsync(new PromptSubmittedEventArgs(text, item));

        this.Text = text;
        await this.TextChanged.InvokeAsync(text);
        this.highlightIndex = -1;

        await this.RaiseSubmitAsync(text, item);
    }

    async Task RaiseSubmitAsync(string text, object? suggestion)
    {
        await this.Submitted.InvokeAsync(new PromptSubmittedEventArgs(text, suggestion));

        if (this.ClearOnSubmit)
        {
            this.Text = String.Empty;
            await this.TextChanged.InvokeAsync(this.Text);
        }
    }

    Task OnMicrophone() => this.Microphone.InvokeAsync();

    /// <summary>Move keyboard focus into the prompt.</summary>
    public ValueTask FocusAsync() => this.field.FocusAsync();
}
