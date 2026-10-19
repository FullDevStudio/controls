using Shiny.Controls.Office.Presentation;
using Shiny.Controls.Office.Shell;
using Shiny.Controls.Office.Skia;
using Shiny.Maui.Controls.Office;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>The MAUI SlideEditorView dressed as PowerPoint: the shell's parts, switches and file requests.</summary>
[Collection(ApplicationResourcesCollection.Name)]
public class SlideEditorShellTests
{
    public SlideEditorShellTests() => TestDispatcherProvider.Install();

    [Fact]
    public void TheShellIsOnByDefault_AndEveryPartCanBeSwitchedOff()
    {
        using var view = new SlideEditorView();

        view.Content.ShouldBe(view.Shell);
        view.Shell.App.ShouldBe(OfficeApp.PowerPoint);
        view.TitleBar.IsVisible.ShouldBeTrue();
        view.StatusBar.IsVisible.ShouldBeTrue();
        view.StatusBar.ShowFitToWindow.ShouldBeTrue();
        view.RibbonActions.ShowComments.ShouldBeFalse();
        view.TitleBar.DocumentName.ShouldBe("Presentation1");

        view.ShowShell = false;

        view.TitleBar.IsVisible.ShouldBeFalse();
        view.StatusBar.IsVisible.ShouldBeFalse();
        view.Ribbon.ApplicationButtonText.ShouldBeNull();
    }

    [Fact]
    public void ShowFileCommandsOff_HidesTheFileSurface_AndLeavesTheRestOfTheShell()
    {
        using var view = new SlideEditorView();

        view.ShowFileCommands = false;

        view.TitleBar.IsVisible.ShouldBeTrue();
        view.TitleBar.ShowSave.ShouldBeFalse();
        view.TitleBar.ShowAutoSave.ShouldBeFalse();
        view.Ribbon.ApplicationButtonText.ShouldBeNull();
        view.CommandIndex.Search("save").ShouldNotContain(x => x.Command.Category == "File");
        view.CommandIndex.Search("print").ShouldBeEmpty();

        view.Shell.IsBackstageOpen = true;
        view.Shell.IsBackstageOpen.ShouldBeFalse();

        view.ShowFileCommands = true;
        view.TitleBar.ShowSave.ShouldBeTrue();
        view.Ribbon.ApplicationButtonText.ShouldBe("File");
        view.CommandIndex.Search("print").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExportGoesToTheDocumentStore_AndSaveThenOverwritesTheSavedFile()
    {
        using var deck = await SlideTemplates.OpenAsync(SlideTemplates.All.Single(x => x.Id == SlideTemplates.PitchId));
        var store = new RecordingStore();
        using var view = new SlideEditorView { Deck = deck, DocumentName = "Pitch", DocumentStore = store };

        await view.ExportAsync(OfficeFileFormats.Pdf);

        var export = store.Saves.ShouldHaveSingleItem();
        export.Request.FileName.ShouldBe("Pitch.pdf");
        export.Request.Action.ShouldBe(OfficeFileAction.Export);
        export.Bytes.Length.ShouldBeGreaterThan(0);
        view.CurrentFile.ShouldBeNull();

        await view.SaveAsync();
        await view.SaveAsync();

        store.Saves[1].Target.ShouldBeNull();
        store.Saves[2].Target.ShouldBe(view.CurrentFile);
        view.CurrentFile!.Name.ShouldBe("Pitch.pptx");
    }

    sealed class RecordingStore : IOfficeDocumentStore
    {
        public List<(OfficeDocumentRef? Target, OfficeSaveRequest Request, byte[] Bytes)> Saves { get; } = [];

        public Task<OfficeDocumentRef?> PickOpenAsync(OfficeApp app, CancellationToken cancellationToken = default) => Task.FromResult<OfficeDocumentRef?>(null);

        public Task<Stream> OpenReadAsync(OfficeDocumentRef document, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<OfficeDocumentRef?> SaveAsync(OfficeDocumentRef? target, OfficeSaveRequest request, CancellationToken cancellationToken = default)
        {
            this.Saves.Add((target, request, await request.ToBytesAsync(cancellationToken)));
            return target ?? new OfficeDocumentRef(Guid.NewGuid().ToString(), request.FileName);
        }

        public Task<IReadOnlyList<OfficeRecentFile>> GetRecentAsync(OfficeApp app, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OfficeRecentFile>>([]);
    }

    [Fact]
    public async Task ViewingModeReachesTheRibbon_AndTheSorterIsAStatusBarButton()
    {
        using var deck = await SlideTemplates.OpenAsync(SlideTemplates.All.Single(x => x.Id == SlideTemplates.ProjectId));
        using var view = new SlideEditorView { Deck = deck };

        view.EditMode = OfficeEditMode.Viewing;
        view.RibbonActions.EditMode.ShouldBe(OfficeEditMode.Viewing);
        view.TitleBar.CanUndo.ShouldBeFalse();

        view.SelectViewMode(OfficeViewModes.Sorter.Id);
        view.ViewMode.ShouldBe(SlideEditorViewMode.SlideSorter);
        view.StatusBar.SelectedViewMode.ShouldBe(OfficeViewModes.Sorter.Id);
    }
}
