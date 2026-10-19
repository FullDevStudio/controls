using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;
using MauiCommand = Microsoft.Maui.Controls.Command;

namespace Shiny.Maui.Controls;

/// <summary>
/// A multi-step flow built on <see cref="StateView"/>: the steps are named branches, and the wizard
/// adds the parts that make it a wizard — an order, a progress indicator, a Back/Next bar that knows
/// where it is, and a validation gate on leaving a step.
/// </summary>
/// <example>
/// <code language="xaml">
/// &lt;shiny:Wizard x:Name="Checkout" CurrentStep="{Binding CurrentStep}" ShowCancel="True"&gt;
///     &lt;shiny:WizardStep Name="Cart" Title="Cart"&gt;
///         &lt;Label Text="Your basket" /&gt;
///     &lt;/shiny:WizardStep&gt;
///     &lt;shiny:WizardStep Name="Pay" Title="Payment" IsValid="{Binding CardIsValid}"&gt;
///         &lt;shiny:TextEntry Text="{Binding CardNumber}" /&gt;
///     &lt;/shiny:WizardStep&gt;
/// &lt;/shiny:Wizard&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Steps))]
public partial class Wizard : ContentView
{
    /// <summary>Appended to the wizard's <see cref="Element.AutomationId"/> to name the built-in Next button.</summary>
    public const string NextAutomationIdSuffix = "_Next";

    /// <summary>Appended to the wizard's <see cref="Element.AutomationId"/> to name the built-in Back button.</summary>
    public const string BackAutomationIdSuffix = "_Back";

    /// <summary>Appended to the wizard's <see cref="Element.AutomationId"/> to name the built-in Cancel button.</summary>
    public const string CancelAutomationIdSuffix = "_Cancel";

    readonly ObservableCollection<WizardStep> steps = new();
    readonly Grid root;
    readonly StateView stateView;
    readonly WizardProgressBar builtInProgress;
    readonly ContentView progressHost;
    readonly ContentView navigationHost;
    readonly Grid builtInNavigation;
    readonly ShinyButton cancelButton;
    readonly ShinyButton backButton;
    readonly ShinyButton nextButton;

    WizardStep? current;
    int suppress;

    public Wizard()
    {
        this.stateView = new StateView
        {
            Transition = StateTransition.Slide,
            TransitionDuration = 220u
        };

        this.builtInProgress = new WizardProgressBar();
        this.builtInProgress.StepSelected += this.OnProgressStepSelected;
        this.progressHost = new ContentView();

        this.cancelButton = new ShinyButton
        {
            Text = "Cancel",
            Appearance = ButtonAppearance.Text,
            Type = ButtonType.Secondary,
            IsVisible = false
        };
        this.backButton = new ShinyButton
        {
            Text = "Back",
            Appearance = ButtonAppearance.Outlined,
            Type = ButtonType.Secondary
        };
        this.nextButton = new ShinyButton
        {
            Text = "Next",
            Appearance = ButtonAppearance.Filled,
            Type = ButtonType.Primary
        };

        // Named after the wizard, so two on one page do not share ids. The fixed ids are what shipped
        // before the wizard's own id was used, and stay for a wizard that has none.
        AutomationIds.Link(this, this.cancelButton, CancelAutomationIdSuffix, "WizardCancelButton");
        AutomationIds.Link(this, this.backButton, BackAutomationIdSuffix, "WizardBackButton");
        AutomationIds.Link(this, this.nextButton, NextAutomationIdSuffix, "WizardNextButton");

        this.cancelButton.Clicked += (_, _) => this.Cancel();
        this.backButton.Clicked += (_, _) => this.GoBack();
        this.nextButton.Clicked += (_, _) => this.GoNext();

        this.builtInNavigation = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        this.builtInNavigation.SetDynamicResource(Grid.ColumnSpacingProperty, ShinyThemeKeys.Spacing.Space2);
        this.builtInNavigation.Add(this.cancelButton, 0);
        this.builtInNavigation.Add(this.backButton, 2);
        this.builtInNavigation.Add(this.nextButton, 3);

        this.navigationHost = new ContentView { Content = this.builtInNavigation };

        this.root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),  // progress, when at the top
                new RowDefinition(GridLength.Star),  // step content
                new RowDefinition(GridLength.Auto),  // progress, when at the bottom
                new RowDefinition(GridLength.Auto)   // navigation bar
            }
        };
        this.root.SetDynamicResource(Grid.RowSpacingProperty, ShinyThemeKeys.Spacing.Space3);
        this.root.Add(this.progressHost, 0, 0);
        this.root.Add(this.stateView, 0, 1);
        this.root.Add(this.navigationHost, 0, 3);

        this.steps.CollectionChanged += this.OnStepsChanged;

        this.GoNextCommand = new MauiCommand(() => this.GoNext(), () => this.IsNextEnabled);
        this.GoBackCommand = new MauiCommand(() => this.GoBack(), () => this.IsBackEnabled);
        this.FinishCommand = new MauiCommand(() => this.Finish(), () => this.IsNextEnabled);
        this.CancelCommand = new MauiCommand(() => this.Cancel(), () => this.CanCancel);
        this.GoToStepCommand = new Microsoft.Maui.Controls.Command<object?>(target =>
        {
            switch (target)
            {
                case int index:
                    this.GoTo(index);
                    break;

                case string name:
                    this.GoTo(name);
                    break;
            }
        });

        base.Content = this.root;

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(Wizard));

        this.BuildLayout();
    }


    /// <summary>The declared steps, in order.</summary>
    public IList<WizardStep> Steps => this.steps;

    /// <summary>The step on screen, or null before the wizard has settled on one.</summary>
    public WizardStep? CurrentStepItem => this.current;

    /// <summary>Only the steps that count — <see cref="WizardStep.IsVisible"/> ones, in order.</summary>
    public IReadOnlyList<WizardStep> VisibleSteps => this.Visible();

    /// <summary>Moves to the next step, or finishes on the last one. Honours every gate.</summary>
    public ICommand GoNextCommand { get; }

    /// <summary>Moves to the previous step.</summary>
    public ICommand GoBackCommand { get; }

    /// <summary>Finishes from wherever the wizard is.</summary>
    public ICommand FinishCommand { get; }

    /// <summary>Abandons the run.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Jumps to a step. The parameter is a step name or a zero-based visible index.</summary>
    public ICommand GoToStepCommand { get; }

    /// <summary>Raised before a step is left. Cancel it to stay put.</summary>
    public event EventHandler<WizardStepChangingEventArgs>? StepChanging;

    /// <summary>Raised once the new step is on screen.</summary>
    public event EventHandler<WizardStepChangedEventArgs>? StepChanged;

    /// <summary>Raised when Finish is taken. Cancel it to stay on the last step.</summary>
    public event EventHandler<WizardFinishingEventArgs>? Finishing;

    /// <summary>Raised after <see cref="Finishing"/> was not cancelled.</summary>
    public event EventHandler? Finished;

    /// <summary>Raised when the run is abandoned.</summary>
    public event EventHandler? Cancelled;


    // -------------------------------------------------------------------------------------------
    // Navigation
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Validate the current step and move forward — or, on the last step, finish. Returns false when a
    /// gate, the step's validity, or a cancelled <see cref="StepChanging"/> stopped the move.
    /// </summary>
    public bool GoNext()
    {
        if (this.current == null || !this.CanGoNext)
            return false;

        if (!this.ValidateCurrent())
            return false;

        var next = this.Adjacent(this.current, forward: true);
        if (next == null)
            return this.Finish();

        return this.Navigate(next, WizardDirection.Forward, complete: true);
    }

    /// <summary>Move back a step. Returns false when there is nowhere to go or a gate stopped it.</summary>
    public bool GoBack()
    {
        if (this.current == null || !this.CanGoBack)
            return false;

        var previous = this.Adjacent(this.current, forward: false);
        return previous != null && this.Navigate(previous, WizardDirection.Backward, complete: false);
    }

    /// <summary>Jump to a named step. Not subject to <see cref="LinearNavigation"/>.</summary>
    public bool GoTo(string name)
    {
        var target = this.Find(name);
        return target != null && this.Navigate(target, this.DirectionTo(target), complete: false);
    }

    /// <summary>Jump to a step by its zero-based index among the visible steps.</summary>
    public bool GoTo(int visibleIndex)
    {
        var visible = this.Visible();
        if (visibleIndex < 0 || visibleIndex >= visible.Count)
            return false;

        var target = visible[visibleIndex];
        return target.IsEnabled && this.Navigate(target, this.DirectionTo(target), complete: false);
    }

    /// <summary>
    /// Take the finish. Marks the current step complete, raises <see cref="Finishing"/> (cancellable)
    /// and then <see cref="Finished"/>.
    /// </summary>
    public bool Finish()
    {
        var args = new WizardFinishingEventArgs(this.current);
        this.Finishing?.Invoke(this, args);
        if (args.Cancel)
            return false;

        if (this.current != null)
            this.current.IsCompleted = true;

        this.Refresh();
        this.Finished?.Invoke(this, EventArgs.Empty);

        var command = this.FinishedCommand;
        if (command?.CanExecute(this.current?.Name) == true)
            command.Execute(this.current?.Name);

        return true;
    }

    /// <summary>Abandon the run.</summary>
    public void Cancel()
    {
        if (!this.CanCancel)
            return;

        this.Cancelled?.Invoke(this, EventArgs.Empty);

        var command = this.CancelledCommand;
        if (command?.CanExecute(this.current?.Name) == true)
            command.Execute(this.current?.Name);
    }

    /// <summary>Clear every step's completion and return to the first one.</summary>
    public void Reset()
    {
        foreach (var step in this.steps)
            step.IsCompleted = false;

        var first = this.Visible().FirstOrDefault(s => s.IsEnabled);
        if (first != null)
            this.Navigate(first, WizardDirection.Backward, complete: false);

        this.Refresh();
    }


    bool ValidateCurrent()
    {
        var step = this.current;
        if (step == null)
            return true;

        // Run first, then re-read IsValid: a view-model that validates inside the command and sets the
        // flag is the shape this is built for, and it only works if the read happens afterwards.
        var validate = step.ValidateCommand;
        if (validate?.CanExecute(step.Name) == true)
            validate.Execute(step.Name);

        return step.IsOptional || step.IsValid;
    }


    bool Navigate(WizardStep target, WizardDirection direction, bool complete)
    {
        if (ReferenceEquals(target, this.current))
            return true;

        if (!target.IsVisible || !target.IsEnabled)
            return false;

        var from = this.current;
        var args = new WizardStepChangingEventArgs(from, target, direction);
        this.StepChanging?.Invoke(this, args);
        if (args.Cancel)
            return false;

        if (complete && from != null)
            from.IsCompleted = true;

        this.current = target;
        this.stateView.CurrentState = target.Name;
        this.Refresh();

        var changed = new WizardStepChangedEventArgs(from, target, direction);
        this.StepChanged?.Invoke(this, changed);

        var command = this.StepChangedCommand;
        if (command?.CanExecute(target.Name) == true)
            command.Execute(target.Name);

        return true;
    }


    WizardStep? Adjacent(WizardStep from, bool forward)
    {
        var visible = this.Visible();
        var index = visible.IndexOf(from);
        if (index < 0)
            return null;

        var step = forward ? 1 : -1;
        for (var i = index + step; i >= 0 && i < visible.Count; i += step)
        {
            if (visible[i].IsEnabled)
                return visible[i];
        }
        return null;
    }

    WizardDirection DirectionTo(WizardStep target)
    {
        if (this.current == null)
            return WizardDirection.None;

        var visible = this.Visible();
        var fromIndex = visible.IndexOf(this.current);
        var toIndex = visible.IndexOf(target);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
            return WizardDirection.None;

        return toIndex > fromIndex ? WizardDirection.Forward : WizardDirection.Backward;
    }

    List<WizardStep> Visible()
    {
        var result = new List<WizardStep>(this.steps.Count);
        foreach (var step in this.steps)
        {
            if (step.IsVisible)
                result.Add(step);
        }
        return result;
    }

    WizardStep? Find(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        foreach (var step in this.steps)
        {
            if (step.IsVisible && string.Equals(step.Name, name, StringComparison.OrdinalIgnoreCase))
                return step;
        }
        return null;
    }


    // -------------------------------------------------------------------------------------------
    // Property plumbing
    // -------------------------------------------------------------------------------------------

    void OnCurrentStepChanged(string? oldValue, string? newValue)
    {
        if (this.suppress > 0)
            return;

        var target = this.Find(newValue);
        if (target == null || !target.IsEnabled || !this.Navigate(target, this.DirectionTo(target), complete: false))
        {
            // The assignment was refused - put the property back so a two-way binding reflects where
            // the wizard actually is rather than where the caller wished it were.
            //
            // It has to be *where the wizard is*, never the previous value: MAUI delays a SetValue
            // made for the property currently being set until the outer set unwinds, so this revert
            // arrives as a fresh change with `suppress` already back to zero. Reverting to the old
            // value would then be refused in turn (nothing is named that either, when there are no
            // steps at all) and revert to the new one - an infinite ping-pong that hangs the process
            // rather than failing anything.
            this.Write(() => this.CurrentStep = this.current?.Name);
        }
    }

    void OnCurrentStepIndexChanged(int newValue)
    {
        if (this.suppress > 0)
            return;

        if (newValue >= 0 && newValue < this.Visible().Count)
            this.GoTo(newValue);

        // Re-read the collection: the move may have changed which steps are visible.
        this.Write(() => this.CurrentStepIndex = this.current == null ? -1 : this.Visible().IndexOf(this.current));
    }

    void OnProgressStepSelected(object? sender, int visibleIndex)
    {
        if (!this.AllowStepSelection)
            return;

        var visible = this.Visible();
        if (visibleIndex < 0 || visibleIndex >= visible.Count)
            return;

        var target = visible[visibleIndex];
        if (this.LinearNavigation && !target.IsCompleted && !ReferenceEquals(target, this.current))
            return;

        this.GoTo(visibleIndex);
    }

    void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (WizardStep removed in e.OldItems)
            {
                removed.Changed -= this.OnStepPropertyChanged;
                removed.StepChanged -= this.OnStepPropertyChanged;
                this.stateView.States.Remove(removed);
                if (ReferenceEquals(removed, this.current))
                    this.current = null;
            }
        }

        if (e.NewItems != null)
        {
            foreach (WizardStep added in e.NewItems)
            {
                added.Changed += this.OnStepPropertyChanged;
                added.StepChanged += this.OnStepPropertyChanged;
                if (!this.stateView.States.Contains(added))
                    this.stateView.States.Add(added);
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            this.stateView.States.Clear();
            this.current = null;
            foreach (var step in this.steps)
            {
                step.Changed -= this.OnStepPropertyChanged;
                step.Changed += this.OnStepPropertyChanged;
                step.StepChanged -= this.OnStepPropertyChanged;
                step.StepChanged += this.OnStepPropertyChanged;
                this.stateView.States.Add(step);
            }
        }

        this.Refresh();
    }

    void OnStepPropertyChanged(object? sender, EventArgs e) => this.Refresh();

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        foreach (var step in this.steps)
            SetInheritedBindingContext(step, this.BindingContext);
    }


    // -------------------------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------------------------

    bool IsNextEnabled
    {
        get
        {
            if (this.current == null || !this.CanGoNext)
                return false;

            return this.current.IsOptional || this.current.IsValid;
        }
    }

    bool IsBackEnabled => this.CanGoBack && this.current != null && this.Adjacent(this.current, forward: false) != null;

    void BuildLayout()
    {
        var showProgress = this.ProgressPosition != WizardProgressPosition.None &&
                           (this.Progress != null || this.ProgressStyle != WizardProgressStyle.None);

        this.progressHost.IsVisible = showProgress;
        Grid.SetRow(this.progressHost, this.ProgressPosition == WizardProgressPosition.Bottom ? 2 : 0);

        if (showProgress)
        {
            var content = this.Progress ?? this.builtInProgress;
            if (!ReferenceEquals(this.progressHost.Content, content))
                this.progressHost.Content = content;

            this.builtInProgress.StyleKind = this.ProgressStyle;
            this.builtInProgress.ShowTitles = this.ShowStepTitles;
            this.builtInProgress.HeightRequest = this.ProgressHeight;
        }
        else if (this.progressHost.Content != null)
        {
            this.progressHost.Content = null;
        }

        var showNavigation = this.ShowNavigationBar;
        this.navigationHost.IsVisible = showNavigation;

        if (showNavigation)
        {
            var content = this.NavigationBar ?? this.builtInNavigation;
            if (!ReferenceEquals(this.navigationHost.Content, content))
                this.navigationHost.Content = content;
        }
        else if (this.navigationHost.Content != null)
        {
            this.navigationHost.Content = null;
        }

        this.Refresh();
    }

    void Refresh()
    {
        var visible = this.Visible();

        // A step can be hidden or disabled out from under the wizard (a branch that no longer
        // applies); land somewhere sensible rather than showing a step that is no longer in the run.
        if (this.current != null && (!this.current.IsVisible || !this.current.IsEnabled))
            this.current = null;

        this.current ??= this.Find(this.CurrentStep) ?? visible.FirstOrDefault(s => s.IsEnabled);

        if (this.current != null && !this.current.IsEnabled)
            this.current = visible.FirstOrDefault(s => s.IsEnabled);

        var index = this.current == null ? -1 : visible.IndexOf(this.current);

        this.stateView.CurrentState = this.current?.Name;

        this.Write(() =>
        {
            this.CurrentStep = this.current?.Name;
            this.CurrentStepIndex = index;
        });

        this.StepCount = visible.Count;
        this.StepNumber = index + 1;
        this.IsFirstStep = index <= 0;
        this.IsLastStep = index < 0 || index == visible.Count - 1;
        this.ProgressFraction = visible.Count == 0 ? 0d : (index + 1) / (double)visible.Count;

        this.RefreshProgress(visible, index);
        this.RefreshNavigation(visible, index);

        ((MauiCommand)this.GoNextCommand).ChangeCanExecute();
        ((MauiCommand)this.GoBackCommand).ChangeCanExecute();
        ((MauiCommand)this.FinishCommand).ChangeCanExecute();
        ((MauiCommand)this.CancelCommand).ChangeCanExecute();
    }

    void RefreshProgress(List<WizardStep> visible, int index)
    {
        if (!this.progressHost.IsVisible || this.Progress != null)
            return;

        var items = new List<WizardProgressItem>(visible.Count);
        for (var i = 0; i < visible.Count; i++)
        {
            var step = visible[i];
            items.Add(new WizardProgressItem(step.DisplayTitle, step.IsCompleted, i == index, step.IsEnabled));
        }

        this.builtInProgress.Items = items;
        this.builtInProgress.IsInteractive = this.AllowStepSelection;
        this.builtInProgress.Fraction = this.ProgressFraction;
        this.builtInProgress.Caption = this.current == null
            ? null
            : $"Step {index + 1} of {visible.Count} — {this.current.DisplayTitle}";
    }

    void RefreshNavigation(List<WizardStep> visible, int index)
    {
        if (!this.navigationHost.IsVisible || this.NavigationBar != null)
            return;

        var step = this.current;
        var isLast = index < 0 || index == visible.Count - 1;

        this.cancelButton.Text = this.CancelText;
        this.cancelButton.IsVisible = this.ShowCancel;
        this.cancelButton.IsEnabled = this.CanCancel;

        this.backButton.Text = step?.BackText ?? this.BackText;
        this.backButton.IsVisible = this.ShowBackOnFirstStep || index > 0;
        this.backButton.IsEnabled = this.IsBackEnabled;

        this.nextButton.Text = step?.NextText ?? (isLast ? this.FinishText : this.NextText);
        this.nextButton.IsEnabled = this.IsNextEnabled;
    }

    /// <summary>Write a bindable property without the change looping back through navigation.</summary>
    void Write(Action write)
    {
        this.suppress++;
        try
        {
            write();
        }
        finally
        {
            this.suppress--;
        }
    }
}
