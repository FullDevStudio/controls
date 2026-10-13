using Microsoft.Maui.Controls;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// The wizard's whole value is in the rules around the move — validity gates, hidden steps, and a
/// cancellable <c>StepChanging</c>. Those are what these cover; the chevron drawing is not logic.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class WizardTests
{
    public WizardTests()
    {
        TestDispatcherProvider.Install();

        // A fresh Application per test, not `Application.Current ?? new` - Application.Current is
        // process-wide, so anything one test merges would leak into the rest of the collection.
        _ = new Application();
    }

    static Wizard Build(params string[] names)
    {
        var wizard = new Wizard { Transition = StateTransition.None };
        foreach (var name in names)
            wizard.Steps.Add(new WizardStep { Name = name, Content = new Label { Text = name } });

        return wizard;
    }


    [Fact]
    public void SettlesOnTheFirstStep()
    {
        var wizard = Build("One", "Two", "Three");

        wizard.CurrentStep.ShouldBe("One");
        wizard.CurrentStepIndex.ShouldBe(0);
        wizard.StepCount.ShouldBe(3);
        wizard.StepNumber.ShouldBe(1);
        wizard.IsFirstStep.ShouldBeTrue();
        wizard.IsLastStep.ShouldBeFalse();
    }

    [Fact]
    public void NextAndBackWalkTheSteps()
    {
        var wizard = Build("One", "Two", "Three");

        wizard.GoNext().ShouldBeTrue();
        wizard.CurrentStep.ShouldBe("Two");

        wizard.GoNext().ShouldBeTrue();
        wizard.CurrentStep.ShouldBe("Three");
        wizard.IsLastStep.ShouldBeTrue();

        wizard.GoBack().ShouldBeTrue();
        wizard.CurrentStep.ShouldBe("Two");
    }

    [Fact]
    public void BackIsRefusedOnTheFirstStep()
    {
        var wizard = Build("One", "Two");

        wizard.GoBack().ShouldBeFalse();
        wizard.CurrentStep.ShouldBe("One");
    }

    [Fact]
    public void NextOnTheLastStepFinishes()
    {
        var wizard = Build("One", "Two");
        var finished = 0;
        wizard.Finished += (_, _) => finished++;

        wizard.GoNext();
        wizard.GoNext();

        finished.ShouldBe(1);
        wizard.CurrentStep.ShouldBe("Two");
        wizard.Steps[1].IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void AnInvalidStepCannotBeLeftForwards()
    {
        var wizard = Build("One", "Two");
        wizard.Steps[0].IsValid = false;

        wizard.GoNext().ShouldBeFalse();
        wizard.CurrentStep.ShouldBe("One");

        wizard.Steps[0].IsValid = true;
        wizard.GoNext().ShouldBeTrue();
        wizard.CurrentStep.ShouldBe("Two");
    }

    [Fact]
    public void AnOptionalStepIsLeftEvenWhenInvalid()
    {
        var wizard = Build("One", "Two");
        wizard.Steps[0].IsValid = false;
        wizard.Steps[0].IsOptional = true;

        wizard.GoNext().ShouldBeTrue();
        wizard.CurrentStep.ShouldBe("Two");
    }

    [Fact]
    public void ValidateCommandRunsBeforeValidityIsRead()
    {
        var wizard = Build("One", "Two");
        var step = wizard.Steps[0];
        step.IsValid = false;

        // The shape this is built for: the command does the validating and sets the flag, which only
        // works because the wizard re-reads IsValid after running it.
        step.ValidateCommand = new Command(() => step.IsValid = true);

        wizard.GoNext().ShouldBeTrue();
        wizard.CurrentStep.ShouldBe("Two");
    }

    [Fact]
    public void CanGoNextGatesTheMove()
    {
        var wizard = Build("One", "Two");
        wizard.CanGoNext = false;

        wizard.GoNext().ShouldBeFalse();
        wizard.CurrentStep.ShouldBe("One");
    }

    [Fact]
    public void CanGoBackGatesTheMove()
    {
        var wizard = Build("One", "Two");
        wizard.GoNext();
        wizard.CanGoBack = false;

        wizard.GoBack().ShouldBeFalse();
        wizard.CurrentStep.ShouldBe("Two");
    }

    [Fact]
    public void StepChangingCanCancelTheMove()
    {
        var wizard = Build("One", "Two");
        wizard.StepChanging += (_, e) => e.Cancel = true;

        wizard.GoNext().ShouldBeFalse();
        wizard.CurrentStep.ShouldBe("One");
    }

    [Fact]
    public void StepChangingCarriesTheDirection()
    {
        var wizard = Build("One", "Two");
        var directions = new List<WizardDirection>();
        wizard.StepChanging += (_, e) => directions.Add(e.Direction);

        wizard.GoNext();
        wizard.GoBack();

        directions.ShouldBe([WizardDirection.Forward, WizardDirection.Backward]);
    }

    [Fact]
    public void HiddenStepsAreSkippedAndDoNotCount()
    {
        var wizard = Build("One", "Two", "Three");
        wizard.Steps[1].IsVisible = false;

        wizard.StepCount.ShouldBe(2);

        wizard.GoNext();
        wizard.CurrentStep.ShouldBe("Three");
        wizard.IsLastStep.ShouldBeTrue();
    }

    [Fact]
    public void DisabledStepsAreSkipped()
    {
        var wizard = Build("One", "Two", "Three");
        wizard.Steps[1].IsEnabled = false;

        wizard.GoNext();

        wizard.CurrentStep.ShouldBe("Three");
        // Still drawn on the indicator, so it stays in the count.
        wizard.StepCount.ShouldBe(3);
    }

    [Fact]
    public void HidingTheCurrentStepMovesOffIt()
    {
        var wizard = Build("One", "Two", "Three");
        wizard.GoNext();
        wizard.CurrentStep.ShouldBe("Two");

        wizard.Steps[1].IsVisible = false;

        wizard.CurrentStep.ShouldNotBe("Two");
        wizard.CurrentStepItem!.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void ForwardMovesMarkTheStepCompleteButBackwardsDoesNot()
    {
        var wizard = Build("One", "Two", "Three");

        wizard.GoNext();
        wizard.Steps[0].IsCompleted.ShouldBeTrue();

        wizard.GoBack();
        wizard.Steps[1].IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public void AssigningCurrentStepNavigates()
    {
        var wizard = Build("One", "Two", "Three");

        wizard.CurrentStep = "Three";

        wizard.CurrentStepItem!.Name.ShouldBe("Three");
        wizard.CurrentStepIndex.ShouldBe(2);
    }

    [Fact]
    public void AssigningAnUnknownStepIsRevertedRatherThanShowingNothing()
    {
        var wizard = Build("One", "Two");

        wizard.CurrentStep = "Nonsense";

        wizard.CurrentStep.ShouldBe("One");
        wizard.CurrentStepItem!.Name.ShouldBe("One");
    }

    /// <summary>
    /// A wizard with nothing in it is what an implicit style, or XAML setting properties before the
    /// steps are parsed, hands you. The revert has to settle rather than ping-pong: MAUI delays a
    /// SetValue made for the property currently being set, so the revert arrives as a fresh change
    /// with the re-entrancy guard already released.
    /// </summary>
    [Fact]
    public void AssigningAStepNameWithNoStepsAtAllSettles()
    {
        var wizard = new Wizard { Transition = StateTransition.None };

        wizard.CurrentStep = "probe";

        wizard.CurrentStep.ShouldBeNull();
        wizard.CurrentStepItem.ShouldBeNull();
        wizard.StepCount.ShouldBe(0);
    }

    [Fact]
    public void AssigningAnOutOfRangeIndexWithNoStepsAtAllSettles()
    {
        var wizard = new Wizard { Transition = StateTransition.None };

        wizard.CurrentStepIndex = 6;

        wizard.CurrentStepIndex.ShouldBe(-1);
    }

    [Fact]
    public void AssigningADisabledStepIsRefused()
    {
        var wizard = Build("One", "Two", "Three");
        wizard.Steps[2].IsEnabled = false;

        wizard.CurrentStep = "Three";

        wizard.CurrentStep.ShouldBe("One");
    }

    [Fact]
    public void CurrentStepIndexIsTwoWay()
    {
        var wizard = Build("One", "Two", "Three");

        wizard.CurrentStepIndex = 2;
        wizard.CurrentStep.ShouldBe("Three");

        wizard.GoBack();
        wizard.CurrentStepIndex.ShouldBe(1);
    }

    [Fact]
    public void ProgressFractionTracksThePosition()
    {
        var wizard = Build("One", "Two", "Three", "Four");

        wizard.ProgressFraction.ShouldBe(0.25);
        wizard.GoNext();
        wizard.ProgressFraction.ShouldBe(0.5);
    }

    [Fact]
    public void FinishingCanBeCancelled()
    {
        var wizard = Build("One");
        var finished = 0;
        wizard.Finishing += (_, e) => e.Cancel = true;
        wizard.Finished += (_, _) => finished++;

        wizard.GoNext().ShouldBeFalse();
        finished.ShouldBe(0);
    }

    [Fact]
    public void ResetClearsCompletionAndReturnsToTheStart()
    {
        var wizard = Build("One", "Two", "Three");
        wizard.GoNext();
        wizard.GoNext();

        wizard.Reset();

        wizard.CurrentStep.ShouldBe("One");
        wizard.Steps.ShouldAllBe(s => !s.IsCompleted);
    }

    [Fact]
    public void CommandsTrackWhatIsActuallyAvailable()
    {
        var wizard = Build("One", "Two");

        wizard.GoBackCommand.CanExecute(null).ShouldBeFalse();
        wizard.GoNextCommand.CanExecute(null).ShouldBeTrue();

        wizard.GoNext();

        wizard.GoBackCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void GoToStepCommandTakesANameOrAnIndex()
    {
        var wizard = Build("One", "Two", "Three");

        wizard.GoToStepCommand.Execute("Three");
        wizard.CurrentStep.ShouldBe("Three");

        wizard.GoToStepCommand.Execute(1);
        wizard.CurrentStep.ShouldBe("Two");
    }

    [Fact]
    public void StepTitleFallsBackToItsName()
    {
        var step = new WizardStep { Name = "Delivery" };
        step.DisplayTitle.ShouldBe("Delivery");

        step.Title = "Where to?";
        step.DisplayTitle.ShouldBe("Where to?");
    }


    class NameModel : System.ComponentModel.INotifyPropertyChanged
    {
        string? name;

        public string? Name
        {
            get => this.name;
            set
            {
                this.name = value;
                this.PropertyChanged?.Invoke(this, new(nameof(this.Name)));
                this.PropertyChanged?.Invoke(this, new(nameof(this.NameIsValid)));
            }
        }

        public bool NameIsValid => !string.IsNullOrEmpty(this.name);

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }


    /// <summary>
    /// The keystroke that flips a bound IsValid used to re-host the step's view from inside the field's
    /// own Text write: the field was detached, lost its inherited BindingContext, had the default
    /// written back into it by its TwoWay binding mid-input - and on Android the input connection then
    /// committed into text that had been replaced underneath it and crashed. (#22)
    /// </summary>
    [Fact]
    public void TypingIntoAStepWhoseValidityFollowsTheTextLeavesTheFieldInPlace()
    {
        var entry = new Entry();
        entry.SetBinding(Entry.TextProperty, new Binding(nameof(NameModel.Name), BindingMode.TwoWay));

        var step = new WizardStep { Name = "Name", Content = entry };
        step.SetBinding(WizardStep.IsValidProperty, nameof(NameModel.NameIsValid));

        var wizard = new Wizard { Transition = StateTransition.None };
        wizard.Steps.Add(step);
        wizard.Steps.Add(new WizardStep { Name = "Done", Content = new Label { Text = "Done" } });

        var model = new NameModel();
        wizard.BindingContext = model;
        step.IsValid.ShouldBeFalse();

        var host = entry.Parent;
        host.ShouldNotBeNull();
        var reparented = 0;
        entry.ParentChanged += (_, _) => reparented++;

        entry.Text = "a";

        step.IsValid.ShouldBeTrue();
        entry.Text.ShouldBe("a");
        model.Name.ShouldBe("a");
        reparented.ShouldBe(0);
        entry.Parent.ShouldBeSameAs(host);
        wizard.GoNextCommand.CanExecute(null).ShouldBeTrue();
    }


    [Fact]
    public void AStepsMetadataStillReachesTheWizard()
    {
        var wizard = Build("One", "Two");
        var first = wizard.Steps[0];
        var raised = 0;
        wizard.GoNextCommand.CanExecuteChanged += (_, _) => raised++;

        first.IsValid = false;

        raised.ShouldBeGreaterThan(0);
        wizard.GoNextCommand.CanExecute(null).ShouldBeFalse();
    }
}
