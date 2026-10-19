using System.Collections.ObjectModel;
using Shiny.Maui.Controls.FloatingPanel;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.SignaturePad;

public partial class SignaturePad : ContentView
{
    /// <summary>
    /// Appended to the pad's own <see cref="Element.AutomationId"/> to name the sheet's content. The sheet
    /// is a <see cref="FloatingPanel.FloatingPanel"/>, so it is not under the pad in the tree.
    /// </summary>
    public const string PopupAutomationIdSuffix = "_Popup";

    /// <summary>Names the drawing surface.</summary>
    public const string CanvasAutomationIdSuffix = "_Canvas";

    /// <summary>Names the sign (accept) button.</summary>
    public const string SignAutomationIdSuffix = "_Sign";

    /// <summary>Names the cancel button.</summary>
    public const string CancelAutomationIdSuffix = "_Cancel";

    /// <summary>Names the clear button.</summary>
    public const string ClearAutomationIdSuffix = "_Clear";

    readonly SignaturePadDrawable drawable;
    readonly GraphicsView graphicsView;
    readonly Button signButton;
    readonly Button cancelButton;
    readonly FloatingPanel.FloatingPanel floatingPanel;
    bool isSyncing;

    public SignaturePad()
    {
        // Hide the wrapper when the panel is closed so it doesn't block
        // touches to the page content underneath.
        IsVisible = false;

        drawable = new SignaturePadDrawable();

        graphicsView = new GraphicsView
        {
            Drawable = drawable,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        graphicsView.StartInteraction += OnStartInteraction;
        graphicsView.DragInteraction += OnDragInteraction;
        graphicsView.EndInteraction += OnEndInteraction;

        signButton = new Button
        {
            Text = "Sign",
            IsEnabled = false,
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.Fill
        }.Neutralize();
        signButton.SetDynamicResource(Button.TextColorProperty, ShinyThemeKeys.Color.OnPrimary);
        this.ApplySignButtonColor(this.SignButtonColor);
        signButton.Clicked += OnSignClicked;

        cancelButton = new Button
        {
            Text = "Cancel",
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.Fill
        }.Neutralize();
        cancelButton.SetDynamicResource(Button.TextColorProperty, ShinyThemeKeys.Color.OnSecondaryContainer);
        this.ApplyCancelButtonColor(this.CancelButtonColor);
        cancelButton.Clicked += OnCancelClicked;

        var clearButton = new Button
        {
            Text = "Clear",
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Padding = new Thickness(8, 2)
        }.Neutralize().WithFontSize(ShinyThemeKeys.Type.BodySmallSize);
        clearButton.SetDynamicResource(Button.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
        clearButton.Clicked += OnClearClicked;

        // Canvas area with clear button overlaid
        var canvasGrid = new Grid();
        canvasGrid.Children.Add(graphicsView);
        canvasGrid.Children.Add(clearButton);

        // Button bar
        var buttonBar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            Padding = new Thickness(16, 8, 16, 16)
        };
        buttonBar.Children.Add(cancelButton);
        Grid.SetColumn(cancelButton, 0);
        buttonBar.Children.Add(signButton);
        Grid.SetColumn(signButton, 1);

        var titleLabel = new Label
        {
            Text = "Draw your signature below",
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 4)
        }.WithFontSize(ShinyThemeKeys.Type.BodyLargeSize);

        var contentGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Padding = new Thickness(16, 0)
        };
        contentGrid.Children.Add(titleLabel);
        Grid.SetRow(titleLabel, 0);
        contentGrid.Children.Add(canvasGrid);
        Grid.SetRow(canvasGrid, 1);
        contentGrid.Children.Add(buttonBar);
        Grid.SetRow(buttonBar, 2);

        AutomationIds.Link(this, contentGrid, PopupAutomationIdSuffix);
        AutomationIds.Link(this, graphicsView, CanvasAutomationIdSuffix);
        AutomationIds.Link(this, signButton, SignAutomationIdSuffix);
        AutomationIds.Link(this, cancelButton, CancelAutomationIdSuffix);
        AutomationIds.Link(this, clearButton, ClearAutomationIdSuffix);

        floatingPanel = new FloatingPanel.FloatingPanel
        {
            IsLocked = true,
            HasBackdrop = true,
            CloseOnBackdropTap = false,
            ShowHandle = false,
            IsContentScrollEnabled = false,
            PanelCornerRadius = 16,
            Position = FloatingPanelPosition.Bottom,
            PanelContent = contentGrid,
            Detents = new ObservableCollection<DetentValue> { DetentValue.Half }
        };

        floatingPanel.Opened += (_, _) =>
        {
            // Prevent system navigation gestures (iOS interactive "swipe back"
            // pop; Android edge-swipe back and TabbedPage swipe-between-tabs)
            // from hijacking strokes that start near the screen edges.
            SetBackGestureEnabled(false);

            if (isSyncing) return;
            isSyncing = true;
            SetValue(IsOpenProperty, true);
            isSyncing = false;
        };

        floatingPanel.Closed += (_, _) =>
        {
            SetBackGestureEnabled(true);

            if (isSyncing) return;
            isSyncing = true;
            SetValue(IsOpenProperty, false);
            isSyncing = false;
            IsVisible = false;
            ResetCanvas();
        };

        Content = floatingPanel;

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(SignaturePad));
    }

    void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        drawable.BeginStroke(e.Touches[0]);
        graphicsView.Invalidate();
    }

    void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0) return;
        drawable.AddPoint(e.Touches[0]);
        graphicsView.Invalidate();
    }

    void OnEndInteraction(object? sender, TouchEventArgs e)
    {
        drawable.EndStroke();
        graphicsView.Invalidate();
        signButton.IsEnabled = drawable.HasSignature;
    }

    void OnSignClicked(object? sender, EventArgs e)
    {
        var stream = drawable.ExportToPng(ExportWidth, ExportHeight);
        var args = new SignatureImageEventArgs(stream);

        Signed?.Invoke(this, args);
        if (SignCommand?.CanExecute(args) == true)
            SignCommand.Execute(args);

        ResetCanvas();
        IsOpen = false;
        IsVisible = false;
    }

    void OnCancelClicked(object? sender, EventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        if (CancelCommand?.CanExecute(null) == true)
            CancelCommand.Execute(null);

        ResetCanvas();
        IsOpen = false;
        IsVisible = false;
    }

    void OnClearClicked(object? sender, EventArgs e)
    {
        ResetCanvas();
    }

    void ResetCanvas()
    {
        drawable.Clear();
        graphicsView.Invalidate();
        signButton.IsEnabled = false;
    }

    // Implemented per-platform to suppress system navigation gestures that
    // would otherwise steal edge-started strokes while the pad is open:
    //   iOS     - the navigation controller's interactive "swipe back" pop.
    //   Android - the API 29+ system back edge-swipe (via gesture exclusion
    //             rects) and a hosting TabbedPage's ViewPager2 swipe.
    // No-op on platforms without such gestures (Windows).
    partial void SetBackGestureEnabled(bool enabled);

    void ApplySignButtonColor(Color? explicitColor)
        => Tint(signButton, Button.BackgroundColorProperty, explicitColor, ShinyThemeKeys.Color.Primary);

    void ApplyCancelButtonColor(Color? explicitColor)
        => Tint(cancelButton, Button.BackgroundColorProperty, explicitColor, ShinyThemeKeys.Color.SecondaryContainer);

    /// <summary>Uses the explicit colour when one was supplied, otherwise binds to the theme token.</summary>
    static void Tint(Element target, BindableProperty property, Color? explicitColor, string themeKey)
    {
        if (explicitColor is null)
        {
            target.SetDynamicResource(property, themeKey);
        }
        else
        {
            target.RemoveDynamicResource(property);
            target.SetValue(property, explicitColor);
        }
    }
}
