using Shiny.Blazor.Controls.QuickEntry;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// Prompt tools: the docked glyphs a package like SpeechAddins adds to a <see cref="PromptView"/>.
/// Attaching is driven from <c>OnParametersSet</c>, which runs on every render of the parent — so
/// the thing worth pinning is that a tool that never left is not attached twice.
/// </summary>
public class PromptToolTests
{
    sealed class ProbeTool : PromptTool
    {
        public int Attached { get; private set; }
        public int Detached { get; private set; }
        public int Responses { get; private set; }
        public int Clicks { get; private set; }

        protected override void OnAttached()
        {
            this.Attached++;
            if (this.Prompt is not null)
                this.Prompt.ResponseChanged += this.OnResponseChanged;
        }

        protected override void OnDetached()
        {
            this.Detached++;
            if (this.Prompt is not null)
                this.Prompt.ResponseChanged -= this.OnResponseChanged;
        }

        protected override Task OnClickAsync()
        {
            this.Clicks++;
            return Task.CompletedTask;
        }

        void OnResponseChanged(object? sender, EventArgs e) => this.Responses++;
    }

    [Fact]
    public void A_tool_in_the_collection_is_attached()
    {
        var view = new PromptView { TrailingTools = new[] { new ProbeTool() } };
        view.SyncTools();

        ((ProbeTool)view.TrailingTools!.First()).Attached.ShouldBe(1);
    }

    [Fact]
    public void Re_running_the_parameter_pass_does_not_attach_again()
    {
        // The bug this guards: OnParametersSet runs on every render of the parent, so a tool that
        // subscribes in OnAttached would end up subscribed several times over.
        var tool = new ProbeTool();
        var view = new PromptView { TrailingTools = new[] { tool } };

        view.SyncTools();
        view.SyncTools();
        view.SyncTools();

        tool.Attached.ShouldBe(1);
    }

    [Fact]
    public void A_tool_removed_from_the_collection_is_detached()
    {
        // A plain List rather than an ObservableCollection: the collection-changed path marshals onto
        // the renderer, which does not exist outside a host. The reconcile itself is the same either way.
        var tool = new ProbeTool();
        var tools = new List<PromptTool> { tool };
        var view = new PromptView { TrailingTools = tools };
        view.SyncTools();

        tools.Remove(tool);
        view.SyncTools();

        tool.Detached.ShouldBe(1);
    }

    [Fact]
    public void Swapping_the_collection_detaches_what_was_in_the_old_one()
    {
        var tool = new ProbeTool();
        var view = new PromptView { LeadingTools = new[] { tool } };
        view.SyncTools();

        view.LeadingTools = Array.Empty<PromptTool>();
        view.SyncTools();

        tool.Detached.ShouldBe(1);
    }

    [Fact]
    public void A_tool_hears_the_answer_arrive_but_not_the_first_parameter_pass()
    {
        var tool = new ProbeTool();
        var view = new PromptView { TrailingTools = new[] { tool }, Response = "already here" };

        view.SyncTools();
        view.RaiseResponseChanged();
        tool.Responses.ShouldBe(0, "the initial parameter set is not the answer arriving");

        view.Response = "42";
        view.RaiseResponseChanged();
        tool.Responses.ShouldBe(1);
    }

    [Fact]
    public async Task A_disabled_tool_swallows_its_click()
    {
        var tool = new ProbeTool { IsEnabled = false };

        await tool.InternalClickAsync();

        tool.Clicks.ShouldBe(0);
    }

    [Fact]
    public void The_popup_prompt_carries_tool_collections_of_its_own()
    {
        // A service cannot hand a component parameters, so the popup's tools live on the state.
        var state = new PromptViewState();
        state.TrailingTools.ShouldNotBeNull();
        state.LeadingTools.ShouldNotBeNull();
    }


    /// <summary>
    /// An Escape in the prompt's field bubbles on to QuickEntryHost. The host has to learn the field
    /// already handled it, or one keypress peels a layer AND closes the popup.
    /// </summary>
    [Fact]
    public async Task An_escape_typed_in_the_field_is_reported_once_to_the_host()
    {
        var view = new PromptView { Text = "draft" };

        await view.OnKeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        view.Text.ShouldBe(string.Empty);
        view.TakeKeyEscapeOutcome().ShouldBe(true);
        view.TakeKeyEscapeOutcome().ShouldBeNull();

        // nothing left to back out of: the field declines, and the host is told so it can close
        await view.OnKeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        view.TakeKeyEscapeOutcome().ShouldBe(false);
    }
}
