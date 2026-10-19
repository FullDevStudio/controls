using Microsoft.Maui.Dispatching;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.Dialogs;

public sealed class DialogService(DialogOptions options) : IDialogService
{
    public async Task Alert(string title, string message, string okText = "OK", Action<DialogConfig>? configure = null)
    {
        var config = this.Build(DialogKind.Alert, title, message, okText, null, null, configure);
        await this.ShowAsync(config);
    }

    public async Task<bool> Confirm(string title, string message, string okText = "Yes", string cancelText = "No", Action<DialogConfig>? configure = null)
    {
        var config = this.Build(DialogKind.Confirm, title, message, okText, cancelText, null, configure);
        var outcome = await this.ShowAsync(config);
        return outcome.Ok;
    }

    public async Task<PromptResult> Prompt(string title, string message, string? placeholder = null, string okText = "OK", string? cancelText = "Cancel", string? initialValue = null, int? maxLength = null, Keyboard? keyboard = null, Action<DialogConfig>? configure = null)
    {
        var config = this.Build(DialogKind.Prompt, title, message, okText, cancelText, placeholder, c =>
        {
            c.InitialValue = initialValue;
            if (maxLength is > 0)
                c.MaxLength = maxLength.Value;
            if (keyboard is not null)
                c.Keyboard = keyboard;
            configure?.Invoke(c);
        });
        var outcome = await this.ShowAsync(config);
        return new PromptResult(outcome.Ok, outcome.Value);
    }

    public async Task<string?> ActionSheet(string title, IReadOnlyList<string> options, string? cancelText = "Cancel", string? destructive = null, Action<DialogConfig>? configure = null)
    {
        var config = this.Build(DialogKind.ActionSheet, title, string.Empty, "OK", cancelText, null, c =>
        {
            c.Actions = options;
            c.DestructiveAction = destructive;
            // action sheets read best sliding up from the bottom; per-call configure can still override
            c.Animation = DialogAnimation.SlideBottom;
            c.MaxWidth = 500;
            configure?.Invoke(c);
        });
        var outcome = await this.ShowAsync(config);
        return outcome.Ok ? outcome.Value : null;
    }

    DialogConfig Build(DialogKind kind, string title, string message, string okText, string? cancelText, string? placeholder, Action<DialogConfig>? configure)
    {
        var config = new DialogConfig
        {
            Kind = kind,
            Title = title,
            Message = message,
            OkText = okText,
            CancelText = cancelText,
            Placeholder = placeholder,
            Animation = options.DefaultAnimation
        };

        options.ConfigureDefaults?.Invoke(config);
        configure?.Invoke(config);
        return config;
    }

    Task<DialogOutcome> ShowAsync(DialogConfig config)
    {
        // The service is injected into view models and services, so it gets called from wherever an
        // await resumed - a thread-pool thread after ConfigureAwait(false) or inside Task.Run. Everything
        // past this point is UI work (the feedback hook, the per-window queue, the overlay on the page),
        // and off the UI thread the platform rejects the first view it is handed. The queue runs
        // fire-and-forget, so that failure reached nobody: the dialog never showed and the caller's task
        // never finished. Hopping here keeps "inject it anywhere" true from any thread.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is { IsDispatchRequired: true })
            return dispatcher.DispatchAsync<DialogOutcome>(() => this.ShowOnUiThreadAsync(config));

        return this.ShowOnUiThreadAsync(config);
    }

    Task<DialogOutcome> ShowOnUiThreadAsync(DialogConfig config)
    {
        if (config.UseFeedback)
            FeedbackHelper.Execute(this, "Show", config.Title);

        return DialogManager.ShowAsync(config, options);
    }
}
