namespace Shiny.Maui.Controls;

/// <summary>
/// One step of a <see cref="Wizard"/>. A <see cref="StateViewState"/> that also carries the pieces a
/// wizard needs: a title for the progress indicator, whether it can be left, and whether it counts
/// towards the run at all.
/// </summary>
[ContentProperty(nameof(Content))]
public class WizardStep : StateViewState
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(WizardStep), null,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(WizardStep), null,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty IsVisibleProperty = BindableProperty.Create(
        nameof(IsVisible), typeof(bool), typeof(WizardStep), true,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create(
        nameof(IsEnabled), typeof(bool), typeof(WizardStep), true,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty IsValidProperty = BindableProperty.Create(
        nameof(IsValid), typeof(bool), typeof(WizardStep), true,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty IsOptionalProperty = BindableProperty.Create(
        nameof(IsOptional), typeof(bool), typeof(WizardStep), false,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty IsCompletedProperty = BindableProperty.Create(
        nameof(IsCompleted), typeof(bool), typeof(WizardStep), false, BindingMode.TwoWay,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty NextTextProperty = BindableProperty.Create(
        nameof(NextText), typeof(string), typeof(WizardStep), null,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty BackTextProperty = BindableProperty.Create(
        nameof(BackText), typeof(string), typeof(WizardStep), null,
        propertyChanged: (b, o, n) => ((WizardStep)b).RaiseStepChanged());

    public static readonly BindableProperty ValidateCommandProperty = BindableProperty.Create(
        nameof(ValidateCommand), typeof(System.Windows.Input.ICommand), typeof(WizardStep), null);


    /// <summary>Shown on the progress indicator. Falls back to <see cref="StateViewState.Name"/>.</summary>
    public string? Title
    {
        get => (string?)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    /// <summary>Sub-caption for the step, shown by the <see cref="WizardProgressStyle.Bar"/> indicator.</summary>
    public string? Description
    {
        get => (string?)this.GetValue(DescriptionProperty);
        set => this.SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Whether this step is part of the run at all. A hidden step is skipped by Next/Back and is not
    /// drawn on the progress indicator, which is how a branch that only applies to some inputs is
    /// modelled — bind it and the wizard reshapes itself.
    /// </summary>
    public bool IsVisible
    {
        get => (bool)this.GetValue(IsVisibleProperty);
        set => this.SetValue(IsVisibleProperty, value);
    }

    /// <summary>A disabled step is drawn but cannot be navigated to.</summary>
    public bool IsEnabled
    {
        get => (bool)this.GetValue(IsEnabledProperty);
        set => this.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Whether the step is happy to be left going forwards. Bind it to a view-model validity flag; the
    /// wizard re-reads it after running <see cref="ValidateCommand"/>, so a command that sets it is
    /// enough to gate Next.
    /// </summary>
    public bool IsValid
    {
        get => (bool)this.GetValue(IsValidProperty);
        set => this.SetValue(IsValidProperty, value);
    }

    /// <summary>An optional step may be left even when <see cref="IsValid"/> is false.</summary>
    public bool IsOptional
    {
        get => (bool)this.GetValue(IsOptionalProperty);
        set => this.SetValue(IsOptionalProperty, value);
    }

    /// <summary>Set by the wizard when the step is left going forwards; cleared by <c>Reset</c>.</summary>
    public bool IsCompleted
    {
        get => (bool)this.GetValue(IsCompletedProperty);
        set => this.SetValue(IsCompletedProperty, value);
    }

    /// <summary>Overrides the wizard's Next button text while this step is showing.</summary>
    public string? NextText
    {
        get => (string?)this.GetValue(NextTextProperty);
        set => this.SetValue(NextTextProperty, value);
    }

    /// <summary>Overrides the wizard's Back button text while this step is showing.</summary>
    public string? BackText
    {
        get => (string?)this.GetValue(BackTextProperty);
        set => this.SetValue(BackTextProperty, value);
    }

    /// <summary>
    /// Run before the step is left going forwards, so a view-model can validate and set
    /// <see cref="IsValid"/>. The step's <c>Name</c> is passed as the parameter.
    /// </summary>
    public System.Windows.Input.ICommand? ValidateCommand
    {
        get => (System.Windows.Input.ICommand?)this.GetValue(ValidateCommandProperty);
        set => this.SetValue(ValidateCommandProperty, value);
    }

    /// <summary>What the progress indicator labels this step.</summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(this.Title) ? (this.Name ?? string.Empty) : this.Title!;

    /// <summary>
    /// Raised when step metadata the wizard renders from changes — title, validity, visibility.
    /// </summary>
    /// <remarks>
    /// ⚠️ Deliberately not the base <c>Changed</c>. That one makes the hosting <see cref="StateView"/>
    /// re-host the step's view, and <see cref="IsValid"/> is routinely bound to the text being typed
    /// into that very view: the keystroke that flipped it detached the field mid-input, the detach
    /// dropped its inherited BindingContext, the TwoWay binding wrote the default back into the field,
    /// and Android's input connection then committed into text that had been replaced under it and
    /// crashed in EmojiCompat. Nothing a step's metadata carries changes what the step shows.
    /// </remarks>
    internal event EventHandler? StepChanged;

    void RaiseStepChanged() => this.StepChanged?.Invoke(this, EventArgs.Empty);
}
