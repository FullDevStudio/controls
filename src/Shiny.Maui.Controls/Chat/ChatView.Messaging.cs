using System.IO;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.Chat;

public partial class ChatView
{
    static readonly string[] DefaultEmojis =
    {
        "👍", "👎", "❤️", "😂", "😮", "😢", "😡", "🔥", "👏", "🙏", "💯", "🎉"
    };

    // ------- sending -------

    async void OnSendRequested(object? sender, string text)
    {
        if (this.session is null)
            return;

        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        if (this.inputBar.IsEditing && this.editingMessageId is not null)
        {
            var id = this.editingMessageId;
            this.editingMessageId = null;
            this.inputBar.ExitEditMode();
            try { await this.session.EditMessageAsync(id, trimmed); }
            catch { /* MessageUpdated event reconciles */ }
            return;
        }

        this.inputBar.ClearText();
        if (this.UseFeedback)
            FeedbackHelper.Execute(this, "MessageSent");

        await this.SendTextAsync(trimmed);
    }

    async Task SendTextAsync(string body)
    {
        if (this.session is null)
            return;

        var clientId = Guid.NewGuid().ToString();
        this.messages.Add(this.CreateOptimistic(clientId, body, null));
        this.Dispatcher.Dispatch(() => this.ScrollToEnd(true));

        await this.DispatchSendAsync(new OutgoingMessage(body, null, clientId), clientId);
    }

    async Task DispatchSendAsync(OutgoingMessage outgoing, string clientId)
    {
        if (this.session is null)
            return;

        try
        {
            var result = await this.session.SendMessageAsync(outgoing);
            this.MergeMessage(result);
        }
        catch (ChatSendRejectedException ex)
        {
            this.UpdateOptimisticStatus(clientId, MessageStatus.Rejected, ex.Message);
        }
        catch (Exception ex)
        {
            this.UpdateOptimisticStatus(clientId, MessageStatus.Failed, ex.Message);
        }
    }

    async Task ResendAsync(ChatMessage message)
    {
        if (this.session is null || string.IsNullOrEmpty(message.ClientMessageId))
            return;

        var clientId = message.ClientMessageId!;
        this.UpdateOptimisticStatus(clientId, MessageStatus.Sending, null);
        try
        {
            var result = await this.session.ResendMessageAsync(clientId);
            this.MergeMessage(result);
        }
        catch (ChatSendRejectedException ex)
        {
            this.UpdateOptimisticStatus(clientId, MessageStatus.Rejected, ex.Message);
        }
        catch (Exception ex)
        {
            this.UpdateOptimisticStatus(clientId, MessageStatus.Failed, ex.Message);
        }
    }

    ChatMessage CreateOptimistic(string clientId, string? body, string? imageUrl)
        => new(
            MessageId: clientId,
            ClientMessageId: clientId,
            SenderId: this.session!.CurrentUserId,
            Body: body,
            ImageUrl: imageUrl,
            Status: MessageStatus.Sending,
            StatusReason: null,
            Timestamp: ShinyTime.For(this).GetLocalNow(),
            EditedTimestamp: null,
            Reactions: Array.Empty<Reaction>(),
            ReadReceipts: Array.Empty<ReadReceipt>()
        );

    void UpdateOptimisticStatus(string clientId, MessageStatus status, string? reason)
    {
        var idx = this.IndexByClientId(clientId);
        if (idx < 0)
            return;
        this.messages[idx] = this.messages[idx] with { Status = status, StatusReason = reason };
    }

    // ------- image attachment -------

    async void OnAttachRequested(object? sender, EventArgs args)
    {
        var page = this.GetPage();
        if (page is null || this.session is null)
            return;

        if (this.UseFeedback)
            FeedbackHelper.Execute(this, "AttachImage");

        var options = new List<string> { "Gallery" };
        if (MediaPicker.Default.IsCaptureSupported)
            options.Add("Camera");

        var choice = await page.DisplayActionSheet("Attach Image", "Cancel", null, options.ToArray());
        FileResult? file = null;
        try
        {
            if (choice == "Gallery")
                file = await MediaPicker.Default.PickPhotoAsync();
            else if (choice == "Camera")
                file = await MediaPicker.Default.CapturePhotoAsync();
        }
        catch
        {
            return;
        }

        if (file is not null)
            await this.SendImageAsync(file);
    }

    async Task SendImageAsync(FileResult file)
    {
        if (this.session is null)
            return;

        var clientId = Guid.NewGuid().ToString();
        string previewPath;
        Stream uploadStream;
        try
        {
            using var source = await file.OpenReadAsync();
            previewPath = Path.Combine(FileSystem.CacheDirectory, $"chat_{clientId}_{file.FileName}");
            using (var fs = File.Create(previewPath))
                await source.CopyToAsync(fs);
            uploadStream = File.OpenRead(previewPath);
        }
        catch
        {
            return;
        }

        this.messages.Add(this.CreateOptimistic(clientId, null, previewPath));
        this.Dispatcher.Dispatch(() => this.ScrollToEnd(true));

        var attachment = new OutgoingAttachment(
            ChatAttachmentKind.Image,
            uploadStream,
            file.FileName,
            file.ContentType ?? "image/jpeg");

        await this.DispatchSendAsync(new OutgoingMessage(null, attachment, clientId), clientId);
    }

    // ------- input-bar overflow actions + markdown link -------

    void SyncInputActionsVisibility()
        => this.inputBar.ShowActionsButton = this.InputActions is { Count: > 0 };

    async void OnInputActionsRequested(object? sender, EventArgs args)
    {
        var page = this.GetPage();
        var actions = this.InputActions;
        if (page is null || actions is null || actions.Count == 0)
            return;

        var titles = actions.Where(a => !string.IsNullOrEmpty(a.Text)).Select(a => a.Text!).ToArray();
        if (titles.Length == 0)
            return;

        var choice = await page.DisplayActionSheet(null, "Cancel", null, titles);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel")
            return;

        var match = actions.FirstOrDefault(a => a.Text == choice);
        if (match is not null)
            await match.InvokeAsync(this);
    }

    async void OnLinkRequested(object? sender, EventArgs args)
    {
        var page = this.GetPage();
        if (page is null)
            return;

        var text = await page.DisplayPromptAsync("Link", "Display text", initialValue: string.Empty);
        if (text is null)
            return; // cancelled

        var url = await page.DisplayPromptAsync("Link", "URL", initialValue: "https://");
        if (string.IsNullOrWhiteSpace(url))
            return;

        this.inputBar.InsertLink(text, url);
    }

    void OnEditCancelled(object? sender, EventArgs args)
    {
        this.editingMessageId = null;
        this.inputBar.ExitEditMode();
    }

    // ------- bubble actions (permission-driven) -------

    internal void ShowBubbleActions(ChatMessage message) => _ = this.ShowBubbleActionsAsync(message);

    async Task ShowBubbleActionsAsync(ChatMessage message)
    {
        var page = this.GetPage();
        if (page is null)
            return;

        var info = this.session?.Info;
        var own = this.IsOwnMessage(message);

        const string React = "React", Edit = "Edit", Delete = "Delete", Copy = "Copy", Retry = "Retry";
        var actions = new List<string>();

        if (info is not null
            && info.Permissions.HasFlag(ChatSessionPermissions.CanReactToMessages)
            && info.PermittedEmojis is not { Length: 0 })
            actions.Add(React);

        if (own && info is not null && info.Permissions.HasFlag(ChatSessionPermissions.CanEditMessages) && !string.IsNullOrEmpty(message.Body))
            actions.Add(Edit);

        if (own && info is not null && info.Permissions.HasFlag(ChatSessionPermissions.CanDeleteMessages))
            actions.Add(Delete);

        if (own && message.Status == MessageStatus.Failed)
            actions.Add(Retry);

        actions.Add(Copy);

        var custom = this.CustomBubbleActions;
        if (custom is not null)
        {
            foreach (var a in custom)
            {
                if (!string.IsNullOrEmpty(a.Text))
                    actions.Add(a.Text!);
            }
        }

        var choice = await page.DisplayActionSheet(null, "Cancel", null, actions.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Cancel")
            return;

        switch (choice)
        {
            case React: await this.ReactAsync(message); break;
            case Edit: this.BeginEdit(message); break;
            case Delete: await this.DeleteAsync(message, page); break;
            case Retry: await this.ResendAsync(message); break;
            case Copy: await CopyAsync(message); break;
            default:
                if (custom is not null)
                {
                    var match = custom.FirstOrDefault(a => a.Text == choice);
                    if (match is not null)
                        await match.InvokeAsync(message);
                }
                break;
        }
    }

    async Task ReactAsync(ChatMessage message)
    {
        if (this.session is null)
            return;

        var permitted = this.session.Info.PermittedEmojis;
        IReadOnlyList<string> set = permitted is { Length: > 0 } ? permitted : DefaultEmojis;
        if (set.Count == 0)
            return;

        var chosen = await this.ShowGlyphPickerAsync(set);
        if (string.IsNullOrEmpty(chosen))
            return;

        var me = this.session.CurrentUserId;
        var already = message.Reactions.Any(r => r.UserId == me && r.Emoji == chosen);
        try { await this.session.ReactToMessageAsync(message.MessageId, chosen, !already); }
        catch { /* provider revalidates */ }
    }

    void BeginEdit(ChatMessage message)
    {
        this.editingMessageId = message.MessageId;
        this.inputBar.EnterEditMode(message.Body ?? string.Empty);
    }

    async Task DeleteAsync(ChatMessage message, Page page)
    {
        var confirm = await page.DisplayAlert("Delete", "Delete this message?", "Delete", "Cancel");
        if (!confirm)
            return;
        try { if (this.session is not null) await this.session.DeleteMessageAsync(message.MessageId); }
        catch { /* MessageDeleted event reconciles */ }
    }

    static async Task CopyAsync(ChatMessage message)
    {
        var text = message.Body ?? message.ImageUrl;
        if (!string.IsNullOrEmpty(text))
            await Clipboard.Default.SetTextAsync(text);
    }

    // ------- taps -------

    internal void OnImageTapped(ChatMessage message, ImageSource? source)
    {
        if (this.OpenImagesInViewer && source is not null)
        {
            this.imageViewer.Source = source;
            this.imageViewer.IsOpen = true;
        }
        else
        {
            this.OnMessageTapped(message);
        }
    }

    internal void OnMessageTapped(ChatMessage message)
    {
        if (this.UseFeedback)
            FeedbackHelper.Execute(this, "MessageTapped", message);
        this.MessageTapped?.Invoke(this, message);
    }
}
