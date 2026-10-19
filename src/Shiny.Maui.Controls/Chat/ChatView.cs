using System.Collections.ObjectModel;
using Shiny.Maui.Controls.Chat.Internal;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Chat;

/// <summary>
/// A provider-driven chat surface. Bind <see cref="Provider"/> + <see cref="SessionId"/> and the
/// control resolves an <see cref="IChatSession"/>, subscribes to its live events, performs cursor
/// paging, renders bubbles, and disposes the session on detach. The control is styles + layout
/// only; all data and behavior live behind the provider.
/// </summary>
public partial class ChatView : ContentView
{
    /// <summary>Raised when a non-image bubble is tapped (image taps open the viewer).</summary>
    public event EventHandler<ChatMessage>? MessageTapped;

    readonly ObservableCollection<ChatMessage> messages = new();

    readonly CollectionView collectionView;
    readonly ChatEntryView defaultInputBar;
    ChatEntryView inputBar;
    readonly VerticalStackLayout typingBubbleHost;
    readonly Border toastPill;
    readonly Label toastNewMessagesLabel;
    readonly Label toastTypingLabel;
    readonly Grid messageArea;
    readonly Grid rootGrid;
    readonly ImageViewer imageViewer;

    // connection banner
    readonly Border connectionBanner;
    readonly Label connectionBannerLabel;

    // loader / error overlays
    readonly Grid loaderOverlay;
    readonly VerticalStackLayout errorOverlay;
    readonly Label errorLabel;

    // session state
    IChatSession? session;

    // scroll / unread
    bool isNearBottom = true;
    int unreadCount;

    // paging
    bool isLoadingOlder;
    bool hasMoreOlder = true;
    bool isReloading;
    CancellationTokenSource? loadCts;

    // typing
    readonly Dictionary<string, long> typingUsers = new();
    IDispatcherTimer? typingExpiryTimer;

    // read receipts
    readonly HashSet<string> markReadRequested = new();

    // edit mode
    string? editingMessageId;

    public ChatView()
    {
        // ItemsUpdatingScrollMode - not ScrollTo - is what actually keeps a chat pinned to the newest
        // message: the handler applies it after the layout pass, whereas ScrollTo against an item the
        // platform has not measured yet is silently dropped. SyncScrollMode flips it to
        // KeepScrollOffset whenever the user has scrolled away or older history is being prepended.
        //
        // RemainingItemsThreshold is deliberately NOT used for paging here. It fires as you approach
        // the *end* of the list, which in a chat is the newest message - so it loaded older history
        // every time you reached the bottom. Paging is driven off the scroll position instead.
        this.collectionView = new CollectionView
        {
            ItemsSource = this.messages,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
            ItemTemplate = new ChatBubbleTemplateSelector(this),
            ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView
        };
        this.collectionView.Scrolled += this.OnCollectionViewScrolled;

        // Shared toast pill — new messages on top, typing below
        this.toastNewMessagesLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            IsVisible = false
        };
        this.toastTypingLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            IsVisible = false
        }.WithFontSize(ShinyThemeKeys.Type.BodySmallSize);
        this.toastPill = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle().WithCornerRadius(ShinyThemeKeys.Shape.CornerLargeRadius),
            Padding = new Thickness(16, 8),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 12),
            IsVisible = false,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children = { this.toastNewMessagesLabel, this.toastTypingLabel }
            }
        };
        this.toastNewMessagesLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnPrimary);
        this.toastTypingLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnPrimary);
        this.toastTypingLabel.Opacity = 0.85;
        this.toastPill.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Primary);

        var pillTap = new TapGestureRecognizer();
        pillTap.Tapped += this.OnToastPillTapped;
        this.toastPill.GestureRecognizers.Add(pillTap);

        // connection banner (top)
        this.connectionBannerLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        }.WithFontSize(ShinyThemeKeys.Type.BodySmallSize);
        this.connectionBanner = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(8, 4),
            VerticalOptions = LayoutOptions.Start,
            IsVisible = false,
            Content = this.connectionBannerLabel
        };
        this.connectionBannerLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnError);
        this.connectionBanner.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Error);

        // loader overlay
        this.loaderOverlay = new Grid
        {
            IsVisible = false,
            Children =
            {
                new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };

        // error overlay
        this.errorLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(24, 0)
        };
        var errorRetry = new Button { Text = "Retry" }.Neutralize();
        errorRetry.Clicked += (_, _) => this.ReloadSession();
        this.errorOverlay = new VerticalStackLayout
        {
            IsVisible = false,
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { this.errorLabel, errorRetry }
        };

        this.messageArea = new Grid
        {
            IsClippedToBounds = true,
            Children = { this.collectionView, this.connectionBanner, this.toastPill, this.loaderOverlay, this.errorOverlay }
        };

        this.typingBubbleHost = new VerticalStackLayout { IsVisible = false, Spacing = 0 };

        this.defaultInputBar = new ChatEntryView();
        this.inputBar = this.defaultInputBar;
        this.HookInputBar(this.inputBar);

        // Never visible. An ImageViewer is a thumbnail *plus* a lightbox, and the chat wants only the
        // lightbox - which is injected into the page's overlay layer, not into this control, so
        // hiding the viewer costs nothing. Left visible, assigning Source on a tap paints that photo
        // full-bleed across every row of the chat (this spans all three) and, because SyncSource
        // clears InputTransparent once there is an image, swallows every touch over it - a second
        // copy of the picture with no way to dismiss it. See issue #11.
        this.imageViewer = new ImageViewer { OpenViewerOnTap = false, IsVisible = false };

        this.rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        this.rootGrid.Add(this.messageArea, 0, 0);
        this.rootGrid.Add(this.typingBubbleHost, 0, 1);
        this.rootGrid.Add(this.inputBar, 0, 2);

        // In the tree, but only so the viewer has a parent chain to walk up when it looks for the
        // page to draw its lightbox on. It paints nothing itself.
        this.rootGrid.Add(this.imageViewer, 0, 0);

        this.Content = this.rootGrid;

        // collections initialised so XAML can Add() directly
        this.InputActions = new ObservableCollection<ChatInputAction>();
        this.CustomBubbleActions = new ObservableCollection<ChatBubbleAction>();

        this.Loaded += (_, _) => this.OnLoaded();
        this.Unloaded += (_, _) => this.OnUnloaded();

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(ChatView));
    }

    // ------- input bar hosting -------

    void HookInputBar(ChatEntryView bar)
    {
        bar.SendRequested += this.OnSendRequested;
        bar.AttachRequested += this.OnAttachRequested;
        bar.ActionsRequested += this.OnInputActionsRequested;
        bar.LinkRequested += this.OnLinkRequested;
        bar.EditCancelled += this.OnEditCancelled;
    }

    void UnhookInputBar(ChatEntryView bar)
    {
        bar.SendRequested -= this.OnSendRequested;
        bar.AttachRequested -= this.OnAttachRequested;
        bar.ActionsRequested -= this.OnInputActionsRequested;
        bar.LinkRequested -= this.OnLinkRequested;
        bar.EditCancelled -= this.OnEditCancelled;
    }

    void SwapInputBar(ChatEntryView? replacement)
    {
        var next = replacement ?? this.defaultInputBar;
        if (ReferenceEquals(next, this.inputBar))
            return;

        this.UnhookInputBar(this.inputBar);
        this.rootGrid.Remove(this.inputBar);

        this.inputBar = next;
        this.HookInputBar(next);
        this.rootGrid.Add(next, 0, 2);

        // The replacement starts blank, so re-apply everything the session already told us.
        next.IsVisible = this.IsInputBarVisible;
        this.ApplyInputBarStyling();
        this.ApplyInfo();
        this.UpdateConnectionUi(this.connectionState);
    }

    // ------- public entry helpers -------

    /// <summary>Gets or sets the current text in the input bar entry field.</summary>
    public string EntryText
    {
        get => this.inputBar.Text;
        set => this.inputBar.Text = value;
    }

    /// <summary>Programmatically submits the current entry text as if Send was pressed.</summary>
    public void SubmitEntry() => this.inputBar.Submit();

    // ------- internal accessors used by bubbles -------

    internal IList<ChatMessage> Items => this.messages;
    internal ChatSessionInfo? Info => this.session?.Info;
    internal string? CurrentUserId => this.session?.CurrentUserId;
    internal bool IsMultiPerson => (this.session?.Info.Users.Length ?? 0) > 2;

    internal bool IsOwnMessage(ChatMessage message)
        => this.session is not null && message.SenderId == this.session.CurrentUserId;

    internal ChatSessionUserInfo? GetUser(string userId)
    {
        var users = this.session?.Info.Users;
        if (users is null)
            return null;

        for (var i = 0; i < users.Length; i++)
        {
            if (users[i].UserId == userId)
                return users[i];
        }
        return null;
    }

    /// <summary>
    /// Forces every bubble to be rebuilt - the only way to pick up a changed template, font or colour,
    /// since the bubbles are plain views rather than bound ones.
    /// </summary>
    internal void RefreshBubbles()
    {
        // Skipped mid-reload: the initial load re-sources the view anyway, and doing it here would land
        // between the load and the initial scroll and reset the offset to the top.
        if (this.isReloading)
            return;

        var wasNearBottom = this.isNearBottom;

        this.collectionView.ItemsSource = null;
        this.Dispatcher.Dispatch(() =>
        {
            this.collectionView.ItemsSource = this.messages;

            // Re-sourcing drops the scroll offset, so put the reader back where they were.
            if (wasNearBottom)
                this.ScrollToEnd();
        });
    }

    Page? GetPage()
    {
        Element? e = this;
        while (e is not null)
        {
            if (e is Page page)
                return page;
            e = e.Parent;
        }
        return Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
    }

    /// <summary>
    /// Displays a centered grid of tappable glyphs over the chat area and returns the glyph the
    /// user selects, or null if the backdrop is tapped to cancel.
    /// </summary>
    internal Task<string?> ShowGlyphPickerAsync(IReadOnlyList<string> glyphs, int columns = 6)
    {
        var tcs = new TaskCompletionSource<string?>();

        var backdrop = new BoxView
        {
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.4),
            Opacity = 0
        };
        var backdropTap = new TapGestureRecognizer();
        backdrop.GestureRecognizers.Add(backdropTap);

        var pickerGrid = new Grid
        {
            ColumnSpacing = 4,
            RowSpacing = 4,
            Padding = new Thickness(8)
        };
        for (var c = 0; c < columns; c++)
            pickerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var rowCount = (int)Math.Ceiling(glyphs.Count / (double)columns);
        for (var r = 0; r < rowCount; r++)
            pickerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var container = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle().WithCornerRadius(ShinyThemeKeys.Shape.CornerLargeRadius),
            Padding = 4,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Opacity = 0,
            Scale = 0.85,
            Content = pickerGrid
        };
        container.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.SurfaceContainerLowest);

        void Close(string? result)
        {
            backdrop.GestureRecognizers.Remove(backdropTap);
            if (this.rootGrid.Children.Contains(backdrop))
                this.rootGrid.Children.Remove(backdrop);
            if (this.rootGrid.Children.Contains(container))
                this.rootGrid.Children.Remove(container);
            tcs.TrySetResult(result);
        }

        backdropTap.Tapped += (_, _) => Close(null);

        for (var i = 0; i < glyphs.Count; i++)
        {
            var glyph = glyphs[i];
            var label = new Label
            {
                Text = glyph,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }.WithFontSize(ShinyThemeKeys.Type.HeadlineMediumSize);
            var cell = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Colors.Transparent,
                Padding = new Thickness(8, 6),
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => Close(glyph);
            cell.GestureRecognizers.Add(tap);

            pickerGrid.Add(cell, i % columns, i / columns);
        }

        this.rootGrid.Add(backdrop, 0, 0);
        Grid.SetRowSpan(backdrop, 3);
        this.rootGrid.Add(container, 0, 0);
        Grid.SetRowSpan(container, 3);

        _ = Task.WhenAll(
            backdrop.FadeToAsync(1, 150u),
            container.FadeToAsync(1, 150u),
            container.ScaleToAsync(1, 150u, Easing.SpringOut));

        return tcs.Task;
    }

    partial void HookKeyboard();
    partial void UnhookKeyboard();

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.OldHandler != null)
            this.UnhookKeyboard();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (this.Handler != null)
            this.HookKeyboard();
    }
}
