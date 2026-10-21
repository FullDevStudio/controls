using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Shiny.Controls.Keyboard;
using Shiny.Maui.Controls.Dialogs;
using Shiny.Maui.Controls.Infrastructure;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// The MAUI half of keyboard shortcuts: XAML-facing properties, binding context hand-off, and the
/// scope rules the host applies. Strokes are fed straight into the manager, standing in for a native
/// key source. The engine is static and shared, so every test uses a key no other test uses.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class KeyboardShortcutTests
{
    public KeyboardShortcutTests()
    {
        // Unconditional: a style added by an earlier test must not leak in through Application.Current.
        _ = new Application();
        KeyboardShortcutManager.Engine.Platform = KeyboardPlatform.Windows;
    }

    sealed class ViewModel
    {
        public int Saves;
        public Command Save => new(() => this.Saves++);
    }

    static KeyStroke Ctrl(string code) => new() { Code = code, Modifiers = KeyModifiers.Control };

    [Fact]
    public void Shortcuts_inherit_the_element_binding_context()
    {
        var vm = new ViewModel();
        var page = new ContentPage { BindingContext = vm };
        var shortcut = new KeyboardShortcut { Gesture = "Ctrl+F1" };
        shortcut.SetBinding(KeyboardShortcut.CommandProperty, nameof(ViewModel.Save));

        KeyboardShortcuts.GetShortcuts(page).Add(shortcut);

        shortcut.BindingContext.ShouldBe(vm);
        shortcut.Command.ShouldNotBeNull();

        var other = new ViewModel();
        page.BindingContext = other;
        shortcut.BindingContext.ShouldBe(other);
    }

    [Fact]
    public void The_xaml_source_generator_path_reaches_a_live_collection()
    {
        // SourceGen emits GetValue(ShortcutsProperty).Add(...) and never calls GetShortcuts. A lazily
        // created collection was null there, and the page threw in InitializeComponent.
        var hits = 0;
        var page = new ContentPage();
        var shortcut = new KeyboardShortcut { Gesture = "Ctrl+F11" };
        shortcut.Pressed += (_, _) => hits++;

        var collection = (KeyboardShortcutCollection)page.GetValue(KeyboardShortcuts.ShortcutsProperty);
        collection.ShouldNotBeNull();
        collection.Add(shortcut);

        var window = new Window(page);
        KeyboardShortcutManager.Process(Ctrl("F11"), window).ShouldBeTrue();
        hits.ShouldBe(1);
    }

    [Fact]
    public void Gesture_and_key_modifiers_both_describe_the_keys()
    {
        var byGesture = new KeyboardShortcut { Gesture = "Primary+Shift+S" };
        byGesture.DisplayText.ShouldBe("Ctrl+Shift+S");

        var byKey = new KeyboardShortcut { Key = "S", Modifiers = KeyModifiers.Primary | KeyModifiers.Shift };
        byKey.ParsedGesture.ShouldBe(byGesture.ParsedGesture);

        // The gesture wins when both are set.
        var both = new KeyboardShortcut { Key = "Q", Gesture = "F7" };
        both.ParsedGesture!.ToString().ShouldBe("F7");
    }

    [Fact]
    public void An_invalid_gesture_disables_the_shortcut()
    {
        var shortcut = new KeyboardShortcut { Gesture = "Hyper+Nope" };
        shortcut.ParsedGesture.ShouldBeNull();
        shortcut.DisplayText.ShouldBe(String.Empty);
        shortcut.Binding.IsEnabled.ShouldBeFalse();

        shortcut.Gesture = "Ctrl+F2";
        shortcut.Binding.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void A_page_in_a_window_fires_its_command()
    {
        var vm = new ViewModel();
        var page = new ContentPage { BindingContext = vm };
        var shortcut = new KeyboardShortcut { Gesture = "Ctrl+F3" };
        shortcut.SetBinding(KeyboardShortcut.CommandProperty, nameof(ViewModel.Save));
        KeyboardShortcuts.GetShortcuts(page).Add(shortcut);

        var window = new Window(page);

        KeyboardShortcutManager.Process(Ctrl("F3"), window).ShouldBeTrue();
        vm.Saves.ShouldBe(1);
    }

    [Fact]
    public void Nothing_fires_outside_a_window()
    {
        var hits = 0;
        var page = new ContentPage();
        var shortcut = new KeyboardShortcut { Gesture = "Ctrl+F4" };
        shortcut.Pressed += (_, _) => hits++;
        KeyboardShortcuts.GetShortcuts(page).Add(shortcut);

        KeyboardShortcutManager.Process(Ctrl("F4"), null).ShouldBeFalse();
        hits.ShouldBe(0);
    }

    [Fact]
    public void A_view_outranks_its_page_and_hides_with_IsVisible()
    {
        var log = new List<string>();
        var view = new ContentView();
        var page = new ContentPage { Content = view };

        var pageShortcut = new KeyboardShortcut { Gesture = "Ctrl+F5" };
        pageShortcut.Pressed += (_, _) => log.Add("page");
        KeyboardShortcuts.GetShortcuts(page).Add(pageShortcut);

        var viewShortcut = new KeyboardShortcut { Gesture = "Ctrl+F5" };
        viewShortcut.Pressed += (_, _) => log.Add("view");
        KeyboardShortcuts.GetShortcuts(view).Add(viewShortcut);

        var window = new Window(page);

        KeyboardShortcutManager.Process(Ctrl("F5"), window);
        view.IsVisible = false;
        KeyboardShortcutManager.Process(Ctrl("F5"), window);

        log.ShouldBe(["view", "page"]);
    }

    [Fact]
    public void A_modal_element_blocks_the_page_behind_it()
    {
        var hits = 0;
        var dialog = new ContentView { IsVisible = false };
        var page = new ContentPage { Content = new Grid { Children = { dialog } } };

        var save = new KeyboardShortcut { Gesture = "Ctrl+F6" };
        save.Pressed += (_, _) => hits++;
        KeyboardShortcuts.GetShortcuts(page).Add(save);
        KeyboardShortcuts.SetIsModal(dialog, true);

        var window = new Window(page);

        KeyboardShortcutManager.Process(Ctrl("F6"), window).ShouldBeTrue();
        dialog.IsVisible = true;
        KeyboardShortcutManager.Process(Ctrl("F6"), window).ShouldBeFalse();
        dialog.IsVisible = false;
        KeyboardShortcutManager.Process(Ctrl("F6"), window).ShouldBeTrue();

        hits.ShouldBe(2);
    }

    [Fact]
    public void A_command_that_cannot_execute_lets_the_key_through()
    {
        var page = new ContentPage();
        var enabled = false;
        var shortcut = new KeyboardShortcut
        {
            Gesture = "Ctrl+F7",
            Command = new Command(() => { }, () => enabled)
        };
        KeyboardShortcuts.GetShortcuts(page).Add(shortcut);
        var window = new Window(page);

        KeyboardShortcutManager.Process(Ctrl("F7"), window).ShouldBeFalse();
        enabled = true;
        KeyboardShortcutManager.Process(Ctrl("F7"), window).ShouldBeTrue();
    }

    [Fact]
    public void Removing_a_shortcut_from_the_collection_unregisters_it()
    {
        var page = new ContentPage();
        var shortcut = new KeyboardShortcut { Gesture = "Ctrl+F8" };
        KeyboardShortcuts.GetShortcuts(page).Add(shortcut);
        var window = new Window(page);

        KeyboardShortcutManager.Process(Ctrl("F8"), window).ShouldBeTrue();
        KeyboardShortcuts.GetShortcuts(page).Remove(shortcut);
        KeyboardShortcutManager.Process(Ctrl("F8"), window).ShouldBeFalse();
    }

    [Fact]
    public void Released_command_runs_on_key_up()
    {
        var log = new List<string>();
        var page = new ContentPage();
        var shortcut = new KeyboardShortcut
        {
            Gesture = "F9",
            Command = new Command(() => log.Add("down")),
            ReleasedCommand = new Command(() => log.Add("up"))
        };
        KeyboardShortcuts.GetShortcuts(page).Add(shortcut);
        var window = new Window(page);

        KeyboardShortcutManager.Process(new KeyStroke { Code = "F9" }, window);
        KeyboardShortcutManager.Process(new KeyStroke { Code = "F9", IsKeyUp = true }, window);

        log.ShouldBe(["down", "up"]);
    }

    static KeyStroke CtrlShift(string code) => new() { Code = code, Modifiers = KeyModifiers.Control | KeyModifiers.Shift };

    static void Collect()
    {
        TestDispatcherProvider.Instance.Timers.Clear();
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        GC.Collect();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference ShowThenSwapOut(Func<ContentPage, VisualElement> build, KeyStroke stroke, bool fires)
    {
        var page = new ContentPage();
        var element = build(page);
        var window = new Window(page);

        KeyboardShortcutManager.Process(stroke, window).ShouldBe(fires);

        // What a Shell switch or a pop does to the page that leaves.
        window.Page = new ContentPage();
        return new WeakReference(element);
    }

    [Fact]
    public void A_page_that_leaves_its_window_is_not_kept_alive()
    {
        // The engine is static. Its scope held the binding, the binding the shortcut, the shortcut
        // the command, and the command the page — so every page that ever had a shortcut survived.
        var weak = ShowThenSwapOut(page =>
        {
            KeyboardShortcuts.GetShortcuts(page).Add(new KeyboardShortcut
            {
                Gesture = "Ctrl+Shift+F1",
                Command = new Command(() => page.Title = "saved")
            });
            return page;
        }, CtrlShift("F1"), fires: true);

        Collect();

        weak.IsAlive.ShouldBeFalse("the shortcut engine still holds the page");
    }

    [Fact]
    public void A_dialog_on_a_page_that_leaves_its_window_is_not_kept_alive()
    {
        // DialogView is modal and binds Escape to its own cancel — the same chain, for every dialog.
        TestDispatcherProvider.Install();
        var weak = ShowThenSwapOut(page =>
        {
            var dialog = new DialogView(
                new DialogConfig { Kind = DialogKind.Confirm, Title = "t", OkText = "OK", CancelText = "Cancel" },
                new DialogOptions()
            );
            page.Content = new Grid { Children = { dialog } };
            return dialog;
        }, CtrlShift("F2"), fires: false);

        Collect();

        weak.IsAlive.ShouldBeFalse("the shortcut engine still holds the dialog");
    }

    [Fact]
    public void A_page_back_in_its_window_has_its_shortcuts_again()
    {
        var log = new List<string>();
        var view = new ContentView();
        var page = new ContentPage { Content = view };

        var pageShortcut = new KeyboardShortcut { Gesture = "Ctrl+Shift+F3" };
        pageShortcut.Pressed += (_, _) => log.Add("page");
        KeyboardShortcuts.GetShortcuts(page).Add(pageShortcut);

        var viewShortcut = new KeyboardShortcut { Gesture = "Ctrl+Shift+F4" };
        viewShortcut.Pressed += (_, _) => log.Add("view");
        KeyboardShortcuts.GetShortcuts(view).Add(viewShortcut);

        var window = new Window(page);
        KeyboardShortcutManager.Process(CtrlShift("F3"), window).ShouldBeTrue();

        window.Page = new ContentPage();
        KeyboardShortcutManager.Process(CtrlShift("F3"), window).ShouldBeFalse();
        KeyboardShortcutManager.Process(CtrlShift("F4"), window).ShouldBeFalse();

        // A Shell switched back to, a page pushed again: the same page, the same collections, a fresh scope.
        window.Page = page;
        KeyboardShortcutManager.Process(CtrlShift("F3"), window).ShouldBeTrue();
        KeyboardShortcutManager.Process(CtrlShift("F4"), window).ShouldBeTrue();

        log.ShouldBe(["page", "page", "view"]);
    }

    static List<Window?> RecordWindows(VisualElement element)
    {
        var seen = new List<Window?>();
        element.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VisualElement.Window))
                seen.Add(element.Window);
        };
        return seen;
    }

    [Fact]
    public void A_view_keeps_its_shortcuts_and_precedence_when_the_overlay_root_moves_it()
    {
        // Installing the overlay root detaches the page's content and adds it back inside the root,
        // so the view loses its window for a moment and its scope is released and rebuilt.
        var log = new List<string>();
        var view = new ContentView();
        var page = new ContentPage { Content = view };

        var pageShortcut = new KeyboardShortcut { Gesture = "Ctrl+Shift+F5" };
        pageShortcut.Pressed += (_, _) => log.Add("page");
        KeyboardShortcuts.GetShortcuts(page).Add(pageShortcut);

        var viewShortcut = new KeyboardShortcut { Gesture = "Ctrl+Shift+F5" };
        viewShortcut.Pressed += (_, _) => log.Add("view");
        KeyboardShortcuts.GetShortcuts(view).Add(viewShortcut);

        var viewOnly = new KeyboardShortcut { Gesture = "Ctrl+Shift+F6" };
        viewOnly.Pressed += (_, _) => log.Add("view only");
        KeyboardShortcuts.GetShortcuts(view).Add(viewOnly);

        var window = new Window(page);
        KeyboardShortcutManager.Process(CtrlShift("F5"), window).ShouldBeTrue();

        var seen = RecordWindows(view);
        PageOverlay.GetOrCreateRoot(page);

        view.Parent.ShouldBeOfType<PageOverlay.ShinyOverlayRoot>();
        seen.ShouldBe([null, window], "the re-parent should take the view out of the window and back");

        KeyboardShortcutManager.Process(CtrlShift("F5"), window).ShouldBeTrue();
        KeyboardShortcutManager.Process(CtrlShift("F6"), window).ShouldBeTrue();

        // And the page still sits underneath: hiding the view hands the keys back to it.
        view.IsVisible = false;
        KeyboardShortcutManager.Process(CtrlShift("F5"), window).ShouldBeTrue();
        KeyboardShortcutManager.Process(CtrlShift("F6"), window).ShouldBeFalse();

        log.ShouldBe(["view", "view", "view only", "page"]);
    }

    [Fact]
    public void A_modal_view_stays_modal_when_the_overlay_root_moves_it()
    {
        var log = new List<string>();
        var dialog = new ContentView();
        var page = new ContentPage { Content = new Grid { Children = { dialog } } };

        var pageShortcut = new KeyboardShortcut { Gesture = "Ctrl+Shift+F7" };
        pageShortcut.Pressed += (_, _) => log.Add("page");
        KeyboardShortcuts.GetShortcuts(page).Add(pageShortcut);

        var dialogShortcut = new KeyboardShortcut { Gesture = "Ctrl+Shift+F8" };
        dialogShortcut.Pressed += (_, _) => log.Add("dialog");
        KeyboardShortcuts.GetShortcuts(dialog).Add(dialogShortcut);
        KeyboardShortcuts.SetIsModal(dialog, true);

        var window = new Window(page);
        KeyboardShortcutManager.Process(CtrlShift("F7"), window).ShouldBeFalse();

        var seen = RecordWindows(dialog);
        PageOverlay.GetOrCreateRoot(page);
        seen.ShouldBe([null, window], "the re-parent should take the dialog out of the window and back");

        KeyboardShortcutManager.Process(CtrlShift("F7"), window).ShouldBeFalse();
        KeyboardShortcutManager.Process(CtrlShift("F8"), window).ShouldBeTrue();

        log.ShouldBe(["dialog"]);

        // The engine is shared: a modal left live would block the other tests' keys.
        window.Page = new ContentPage();
    }

    [Fact]
    public void IsModal_changed_while_out_of_the_window_applies_when_it_comes_back()
    {
        // With no scope to set it on, the change is only in the attached property — the rebuilt
        // scope has to read it from there.
        var hits = 0;
        var dialog = new ContentView();
        var page = new ContentPage { Content = new Grid { Children = { dialog } } };

        var pageShortcut = new KeyboardShortcut { Gesture = "Ctrl+Shift+F9" };
        pageShortcut.Pressed += (_, _) => hits++;
        KeyboardShortcuts.GetShortcuts(page).Add(pageShortcut);
        KeyboardShortcuts.SetIsModal(dialog, true);

        var window = new Window(page);
        KeyboardShortcutManager.Process(CtrlShift("F9"), window).ShouldBeFalse();

        window.Page = new ContentPage();
        KeyboardShortcuts.SetIsModal(dialog, false);
        window.Page = page;

        KeyboardShortcutManager.Process(CtrlShift("F9"), window).ShouldBeTrue();
        hits.ShouldBe(1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static (WeakReference Page, Window Window) ShowShellTabThenSwapOutShell()
    {
        var first = new ContentPage();
        KeyboardShortcuts.GetShortcuts(first).Add(new KeyboardShortcut
        {
            Gesture = "Ctrl+Shift+F10",
            Command = new Command(() => first.Title = "saved")
        });

        var tabBar = new TabBar();
        tabBar.Items.Add(new Tab { Items = { new ShellContent { Content = first } } });
        tabBar.Items.Add(new Tab { Items = { new ShellContent { Content = new ContentPage() } } });
        var shell = new Shell { Items = { tabBar } };
        var window = new Window(shell);
        KeyboardShortcutManager.Process(CtrlShift("F10"), window).ShouldBeTrue();

        // Switching tabs inside a Shell does not take the page out of the window: Shell keeps it
        // under its ShellContent, so its scope stays registered (switched off by Disappearing on a
        // device) for as long as the Shell itself holds the page.
        tabBar.CurrentItem = tabBar.Items[1];
        first.Window.ShouldBe(window);

        // Replacing the Shell, which is what Shiny's SwitchShell does to Window.Page, takes every
        // page it kept out of the window with it.
        window.Page = new ContentPage();
        return (new WeakReference(first), window);
    }

    [Fact]
    public void A_Shell_tab_switched_away_is_let_go_with_its_Shell()
    {
        var (weak, window) = ShowShellTabThenSwapOutShell();

        KeyboardShortcutManager.Process(CtrlShift("F10"), window).ShouldBeFalse();
        Collect();

        weak.IsAlive.ShouldBeFalse("the shortcut engine still holds the page of a Shell that left the window");
    }

    [Fact]
    public void Service_registrations_are_app_wide_and_disposable()
    {
        var service = new KeyboardShortcutService();
        var hits = 0;
        var registration = service.Register("Ctrl+F10", _ => hits++, b => b.Description = "test");

        service.GetActiveShortcuts().ShouldContain(x => x.Binding.Description == "test");
        KeyboardShortcutManager.Process(Ctrl("F10"), null).ShouldBeTrue();

        registration.Dispose();
        KeyboardShortcutManager.Process(Ctrl("F10"), null).ShouldBeFalse();
        hits.ShouldBe(1);
    }

    [Fact]
    public void Service_formats_for_the_platform()
    {
        var service = new KeyboardShortcutService();
        KeyboardShortcutManager.Engine.Platform = KeyboardPlatform.Apple;
        try
        {
            service.Format("Primary+Shift+P").ShouldBe("⇧⌘P");
        }
        finally
        {
            KeyboardShortcutManager.Engine.Platform = KeyboardPlatform.Windows;
        }
    }
}
