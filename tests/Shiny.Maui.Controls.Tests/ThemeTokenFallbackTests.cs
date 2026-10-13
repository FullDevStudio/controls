using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Dialogs;
using Shiny.Maui.Controls.Kanban;
using Shiny.Maui.Controls.Kanban.Internal;
using Shiny.Maui.Controls.Themes;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// Dialogs and the Kanban board follow the installed theme, and paint a literal fallback only when
/// there is none (#19).
/// </summary>
/// <remarks>
/// Both seeded the fallback as a local value and then bound the token. A local value outranks a
/// dynamic resource, so the fallback - the purple OK button, the white card, the Basic board - won in
/// every theme and through every live theme switch.
/// </remarks>
[Collection(ApplicationResourcesCollection.Name)]
public class ThemeTokenFallbackTests
{
    static readonly Color Orange = Color.FromArgb("#FF8C00");

    public ThemeTokenFallbackTests() => TestDispatcherProvider.Install();

    /// <summary>The Basic palette with a few tokens swapped for colours no fallback uses.</summary>
    static ResourceDictionary Palette(Color accent)
    {
        var palette = new ResourceDictionary();
        palette.MergedDictionaries.Add(new BasicLightTheme());
        palette[ShinyThemeKeys.Color.Primary] = accent;
        palette[ShinyThemeKeys.Color.SurfaceVariant] = accent;
        palette[ShinyThemeKeys.Color.Error] = accent;
        palette[ShinyThemeKeys.Color.SurfaceContainerLow] = accent;
        return palette;
    }

    static Application App(ResourceDictionary? palette)
    {
        var app = new Application { UserAppTheme = AppTheme.Light };
        if (palette is not null)
            app.Resources.MergedDictionaries.Add(palette);
        return app;
    }

    static T Host<T>(Application app, T view) where T : View
    {
        var page = new ContentPage { Content = view };
        page.Parent = app;
        return view;
    }

    static Button ButtonFor(Element root, string text)
        => ((IVisualTreeElement)root).GetVisualTreeDescendants().OfType<Button>().Single(b => b.Text == text);

    static DialogView Dialog(Application app, DialogConfig config)
        => Host(app, new DialogView(config, new DialogOptions()));


    [Fact]
    public void DialogButtonsTakeTheThemeColour()
    {
        var app = App(Palette(Orange));
        var dialog = Dialog(app, new DialogConfig { Kind = DialogKind.Confirm, Title = "t", OkText = "OK", CancelText = "Cancel" });

        ButtonFor(dialog, "OK").BackgroundColor.ShouldBe(Orange, "unfixed, the hard-coded #7C3AED won");
    }


    [Fact]
    public void DialogFollowsALiveThemeSwitch()
    {
        var first = Palette(Orange);
        var app = App(first);
        var dialog = Dialog(app, new DialogConfig { Kind = DialogKind.Confirm, OkText = "OK", CancelText = "Cancel" });

        app.Resources.MergedDictionaries.Remove(first);
        app.Resources.MergedDictionaries.Add(Palette(Colors.Teal));

        ButtonFor(dialog, "OK").BackgroundColor.ShouldBe(Colors.Teal);
    }


    [Fact]
    public void AnExplicitDialogColourStillWins()
    {
        var app = App(Palette(Orange));
        var dialog = Dialog(app, new DialogConfig { Kind = DialogKind.Confirm, OkText = "OK", CancelText = "Cancel", OkButtonColor = Colors.Green });

        ButtonFor(dialog, "OK").BackgroundColor.ShouldBe(Colors.Green);
    }


    [Fact]
    public void ActionSheetOptionsFollowTheThemeAndCanBeColoured()
    {
        var app = App(Palette(Orange));

        var themed = Dialog(app, new DialogConfig { Kind = DialogKind.ActionSheet, Actions = ["Share", "Delete"], DestructiveAction = "Delete" });
        ButtonFor(themed, "Share").BackgroundColor.ShouldBe(Orange);
        ButtonFor(themed, "Delete").TextColor.ShouldBe(Orange);

        var coloured = Dialog(app, new DialogConfig
        {
            Kind = DialogKind.ActionSheet,
            Actions = ["Share", "Delete"],
            DestructiveAction = "Delete",
            ActionSheetButtonColor = Colors.Green,
            ActionSheetButtonTextColor = Colors.Blue,
            DestructiveButtonTextColor = Colors.Purple
        });
        ButtonFor(coloured, "Share").BackgroundColor.ShouldBe(Colors.Green);
        ButtonFor(coloured, "Share").TextColor.ShouldBe(Colors.Blue);
        ButtonFor(coloured, "Delete").TextColor.ShouldBe(Colors.Purple);
    }


    [Fact]
    public void WithNoThemeTheDialogStillPaints()
    {
        var app = App(null);
        var dialog = Dialog(app, new DialogConfig { Kind = DialogKind.Confirm, OkText = "OK", CancelText = "Cancel" });

        ButtonFor(dialog, "OK").BackgroundColor.ShouldBe(Color.FromArgb("#7C3AED"));
    }


    [Fact]
    public void KanbanTokensFollowTheThemeAndALiveSwitch()
    {
        var first = Palette(Orange);
        var app = App(first);
        var box = Host(app, new BoxView());

        KanbanChrome.Token(box, VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.SurfaceContainerLow);
        box.BackgroundColor.ShouldBe(Orange, "unfixed, the Basic fallback won");

        app.Resources.MergedDictionaries.Remove(first);
        app.Resources.MergedDictionaries.Add(Palette(Colors.Teal));
        box.BackgroundColor.ShouldBe(Colors.Teal);
    }


    [Fact]
    public void WithNoThemeKanbanFallsBackToBasic()
    {
        var app = App(null);
        var box = Host(app, new BoxView());

        KanbanChrome.Token(box, VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.SurfaceContainerLow);

        box.BackgroundColor.ShouldBe(KanbanChrome.Fallback(ShinyThemeKeys.Color.SurfaceContainerLow));
    }


    [Fact]
    public void AKanbanBoardPaintsInTheInstalledTheme()
    {
        var app = App(Palette(Orange));
        var view = Host(app, new KanbanView
        {
            Columns = new List<KanbanColumn> { new() { Id = "todo", Title = "To do" } },
            Cards = new List<KanbanCard> { new() { Id = "a", Title = "A", ColumnId = "todo" } }
        });

        var painted = ((IVisualTreeElement)view).GetVisualTreeDescendants().OfType<VisualElement>().Select(e => e.BackgroundColor).ToList();
        painted.ShouldContain(Orange);
        painted.ShouldNotContain(KanbanChrome.Fallback(ShinyThemeKeys.Color.SurfaceContainerLow));
    }
}
