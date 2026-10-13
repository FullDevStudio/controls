# StateView & Wizard

[← All Shiny Controls](../../README.md)

Two related controls on both hosts. **`StateView`** shows exactly one of several named branches, chosen by
a string — the declarative form of the `IsVisible` (MAUI) / `@if/else` (Blazor) ladder every app grows.
**`Wizard`** builds on it: the same named branches, plus an order, a progress indicator, a Back/Next bar
that knows where it is, and a gate on leaving a step.

**StateView** — bind `CurrentState` and the matching `StateViewState` is what is on screen. An unmatched
name falls back to `DefaultState`, then to the first declared state, so a typo shows something rather than
a blank rectangle. Content declared inline is built with the rest of the markup; content declared as a
`ContentTemplate` (MAUI) is built the first time the branch is reached and then cached — turn `CacheContent`
off to rebuild, and reset, on every visit. On Blazor the branches are lazy by construction: a
`StateViewState` renders nothing itself and hands its `ChildContent` to the state view.

`Transition` animates the swap — `None`, `Fade`, `Slide` (direction taken from the move, so a later state
enters from the right and an earlier one from the left), `SlideLeft`/`SlideRight`/`SlideUp`/`SlideDown`, or
`Scale`.

```xml
<shiny:StateView CurrentState="{Binding CurrentState}" Transition="Slide">
    <shiny:StateViewState Name="Loading">
        <ActivityIndicator IsRunning="True" />
    </shiny:StateViewState>
    <shiny:StateViewState Name="Loaded">
        <shiny:StateViewState.ContentTemplate>
            <DataTemplate><local:ReportView /></DataTemplate>
        </shiny:StateViewState.ContentTemplate>
    </shiny:StateViewState>
    <shiny:StateViewState Name="Error">
        <Label Text="Something went wrong" />
    </shiny:StateViewState>
</shiny:StateView>
```

```razor
<StateView @bind-CurrentState="state" Transition="StateTransition.Slide">
    <States>
        <StateViewState Name="Loading"><Spinner /></StateViewState>
        <StateViewState Name="Loaded"><Report /></StateViewState>
        <StateViewState Name="Error"><p>Something went wrong</p></StateViewState>
    </States>
</StateView>
```

**Wizard** — steps are `WizardStep`s (a `StateViewState` with a title and a few rules). The default
progress indicator is the pointed breadcrumb: one chevron per step, completed / current / upcoming taken
from the theme, with `ProgressStyle="Dots"` and `"Bar"` as alternatives and `Progress` to replace it with
your own view entirely. On MAUI it is drawn on a `GraphicsView`, so it renders identically on every head
including AppKit and GTK4; on Blazor the same shape is a `clip-path`.

What the wizard adds beyond switching views:

- **Validity gates.** `WizardStep.IsValid` blocks Next; `IsOptional` bypasses it. `ValidateCommand` (MAUI)
  runs *before* `IsValid` is read, so a view-model that validates inside the command and sets the flag is
  enough — no event wiring. Blazor's `Validate` is an `async Func<Task<bool>>`, so a server round-trip is a
  first-class validator. `StepChanging` is cancellable for anything neither can express. Binding `IsValid`
  to the text being typed into the step is safe: a change to a step's metadata (`IsValid`, `Title`,
  `IsCompleted`, …) refreshes the wizard chrome without re-hosting the step's view, so the field keeps
  focus and its input on every platform.
- **Conditional branches.** `IsVisible="False"` takes a step out of the run entirely: skipped by Next/Back,
  dropped from the progress bar, and excluded from `StepCount`. Bind it and the wizard reshapes itself.
  `IsEnabled="False"` keeps the step on the indicator but unreachable.
- **Built-in commands.** `GoNextCommand`, `GoBackCommand`, `FinishCommand`, `CancelCommand` and
  `GoToStepCommand` are on the wizard, so a button inside a step reaches them with `x:Reference` rather
  than the view-model re-implementing navigation. `CanGoBack`/`CanGoNext` remain yours — they are ANDed
  with the wizard's own boundary and validity checks.
- **Position, read-only and bindable.** `StepNumber`, `StepCount`, `IsFirstStep`, `IsLastStep` and
  `ProgressFraction`, plus two-way `CurrentStep` and `CurrentStepIndex`. Assigning an unknown or disabled
  step is reverted rather than blanking the wizard.
- **Review without skipping ahead.** `AllowStepSelection` makes the indicator clickable;
  `LinearNavigation` (on by default) limits that to steps already completed.
- **Finish that can fail.** `Finishing` is cancellable, so a submit rejected server-side leaves the user on
  the last step with their input intact.

```xml
<shiny:Wizard x:Name="Checkout"
              CurrentStep="{Binding CurrentStep}"
              ShowCancel="True"
              AllowStepSelection="True"
              FinishedCommand="{Binding SubmitCommand}">

    <shiny:WizardStep Name="Account" Title="Account" IsValid="{Binding EmailIsValid}">
        <shiny:TextEntry Text="{Binding Email}" Placeholder="you@example.com" />
    </shiny:WizardStep>

    <!-- Turn delivery off and this step leaves the run entirely -->
    <shiny:WizardStep Name="Delivery" Title="Delivery" IsVisible="{Binding WantsDelivery}">
        <shiny:TextEntry Text="{Binding Address}" />
    </shiny:WizardStep>

    <shiny:WizardStep Name="Review" Title="Review" NextText="Place order">
        <Button Text="Start over"
                Command="{Binding Source={x:Reference Checkout}, Path=GoToStepCommand}"
                CommandParameter="Account" />
    </shiny:WizardStep>
</shiny:Wizard>
```

```razor
<Wizard @bind-CurrentStep="step" ShowCancel="true" Finished="SubmitAsync">
    <Steps>
        <WizardStep Name="Account" Title="Account" IsValid="@emailIsValid">…</WizardStep>
        <WizardStep Name="Delivery" Title="Delivery" IsVisible="@wantsDelivery">…</WizardStep>
        <WizardStep Name="Review" Title="Review" NextText="Place order" Validate="ConfirmAsync">…</WizardStep>
    </Steps>
    <Progress>
        <!-- optional: replaces the built-in pointed progress bar -->
    </Progress>
</Wizard>
```

Turn `ShowNavigationBar` off when each step carries its own buttons; `NavigationBar` (MAUI) /
`<NavigationBar>` (Blazor) replaces the built-in bar while keeping the wizard's own navigation logic.
