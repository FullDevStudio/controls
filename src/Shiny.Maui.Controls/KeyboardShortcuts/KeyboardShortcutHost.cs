using System.Collections.Specialized;
using System.ComponentModel;
using Shiny.Controls.Keyboard;

namespace Shiny.Maui.Controls;

/// <summary>
/// The engine scope behind one element's <see cref="KeyboardShortcuts"/>: keeps its shortcuts in
/// sync with the collection, its bindings fed with the element's <c>BindingContext</c>, and the scope
/// switched on exactly while the element is showing.
/// </summary>
/// <remarks>
/// <para>
/// "Showing" deliberately avoids <c>Loaded</c>. A page underneath a pushed one stays loaded on iOS,
/// and the AppKit and GTK heads do not raise every lifecycle event the mobile ones do. A page is
/// showing between <c>Appearing</c> and <c>Disappearing</c>; any other element while it is in a
/// window, visible, and its page is showing.
/// </para>
/// <para>
/// That last clause is enforced by the engine rather than here: a view's scope takes its page's
/// scope as its parent — creating an empty one for the page if the page declares no shortcuts — and
/// a scope whose parent is inactive is inactive. It also makes the view's shortcuts outrank the
/// page's, which is the precedence anyone would expect.
/// </para>
/// <para>
/// The engine is static and its scopes hold the bindings, whose handlers reach the element and its
/// whole page. So the scope is only registered while the element is in a window: it is created when
/// the element gets one and removed from the engine when it loses it, otherwise every page that ever
/// carried a shortcut — a Shell swapped out, a popped page, a closed dialog — would stay alive.
/// </para>
/// </remarks>
sealed class KeyboardShortcutHost
{
    static readonly BindableProperty HostProperty = BindableProperty.CreateAttached(
        "ShinyKeyboardShortcutHost",
        typeof(KeyboardShortcutHost),
        typeof(KeyboardShortcutHost),
        null
    );

    readonly VisualElement element;
    KeyboardShortcutCollection? collection;
    KeyboardShortcutScope? scope;
    bool appeared;
    bool sawLifecycle;

    KeyboardShortcutHost(VisualElement element)
    {
        this.element = element;

        element.PropertyChanged += this.OnElementPropertyChanged;
        element.BindingContextChanged += this.OnBindingContextChanged;

        if (element is Page page)
        {
            page.Appearing += this.OnAppearing;
            page.Disappearing += this.OnDisappearing;

            // A host created after the page appeared — a view added at runtime asks for its page's
            // scope — has missed Appearing and has to work out where things stand.
            this.appeared = IsShowing(page);
        }

        this.Evaluate();
    }

    /// <summary>
    /// The element's scope in the engine, created on first use. Only asked for while the element is
    /// in a window — by its own activation, or by a view on it looking for its parent scope.
    /// </summary>
    public KeyboardShortcutScope Scope
    {
        get
        {
            if (this.scope == null)
            {
                this.scope = KeyboardShortcutManager.Engine.CreateScope(this.element.GetType().Name);
                this.scope.IsModal = KeyboardShortcuts.GetIsModal(this.element);
                this.Sync();
            }

            return this.scope;
        }
    }

    public static KeyboardShortcutHost Ensure(VisualElement element)
    {
        if (element.GetValue(HostProperty) is KeyboardShortcutHost existing)
            return existing;

        var host = new KeyboardShortcutHost(element);
        element.SetValue(HostProperty, host);
        return host;
    }

    internal static KeyboardShortcutHost? Get(BindableObject element) => element.GetValue(HostProperty) as KeyboardShortcutHost;

    public void SetModal(bool modal)
    {
        if (this.scope != null)
            this.scope.IsModal = modal;
    }

    public void SetCollection(KeyboardShortcutCollection? value)
    {
        if (this.collection != null)
            this.collection.CollectionChanged -= this.OnCollectionChanged;

        this.collection = value;

        if (this.collection != null)
            this.collection.CollectionChanged += this.OnCollectionChanged;

        this.Sync();
    }

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => this.Sync();

    void Sync()
    {
        var wanted = this.collection?.ToList() ?? [];
        foreach (var shortcut in wanted)
            BindableObject.SetInheritedBindingContext(shortcut, this.element.BindingContext);

        // Without a scope there is nothing to keep in step; the collection is read again when one is created.
        if (this.scope is not { } scope)
            return;

        foreach (var binding in scope.Bindings)
        {
            if (binding.Tag is not KeyboardShortcut shortcut || !wanted.Contains(shortcut))
                scope.Remove(binding);
        }

        // Re-added in collection order, which is the order the engine tries them in.
        foreach (var shortcut in wanted)
        {
            scope.Remove(shortcut.Binding);
            scope.Add(shortcut.Binding);
        }
    }

    void OnBindingContextChanged(object? sender, EventArgs e)
    {
        // Items of an attached collection are not children of anything, so nothing hands them the
        // element's BindingContext — {Binding SaveCommand} would quietly resolve against null.
        if (this.collection == null)
            return;

        foreach (var shortcut in this.collection)
            BindableObject.SetInheritedBindingContext(shortcut, this.element.BindingContext);
    }

    void OnAppearing(object? sender, EventArgs e)
    {
        this.sawLifecycle = true;
        this.appeared = true;
        this.Evaluate();
    }

    void OnDisappearing(object? sender, EventArgs e)
    {
        this.sawLifecycle = true;
        this.appeared = false;
        this.Evaluate();
    }

    void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(VisualElement.IsVisible) or nameof(VisualElement.Window) or nameof(Element.Parent)))
            return;

        // Shortcuts are usually declared in XAML, before the page has a window — so the host exists
        // before anything could have appeared. Until the first Appearing/Disappearing arrives, the
        // page's place in its window is the best evidence there is; the lifecycle events take over
        // the moment they start.
        if (this.element is Page page && !this.sawLifecycle && e.PropertyName == nameof(VisualElement.Window))
            this.appeared = IsShowing(page);

        this.Evaluate();
    }

    void Evaluate()
    {
        var window = this.element.Window;
        if (window == null)
        {
            // Out of the window: the engine lets go of the scope, and with it of the element. Back
            // in a window — a Shell switched back to, a page pushed again, content the overlay root
            // has just re-parented — Scope builds a fresh one from the collection. A Shell tab that
            // is switched away never gets here: Shell keeps it in the window, so it is only switched
            // off by Disappearing, and goes when its Shell does.
            if (this.scope is { } released)
            {
                this.scope = null;
                KeyboardShortcutManager.Engine.RemoveScope(released);
            }
            return;
        }

        var active = this.element is Page
            ? this.appeared
            : this.element.IsVisible;

        if (!active)
        {
            this.scope?.Deactivate();
            return;
        }

        this.Scope.Window = window;
        this.Scope.Parent = this.FindParentScope();
        this.Scope.Activate();
        KeyboardShortcutManager.EnsureWindow(window);
    }

    KeyboardShortcutScope? FindParentScope()
    {
        for (var parent = this.element.Parent; parent != null; parent = parent.Parent)
        {
            if (Get(parent) is { } host)
                return host.Scope;

            // A view always hangs off its page's scope, so the page's Appearing/Disappearing switch
            // the view too. A page only links to an enclosing page that has shortcuts of its own.
            if (parent is Page page && this.element is not Page)
                return Ensure(page).Scope;
        }

        return null;
    }

    /// <summary>
    /// Best guess at whether <paramref name="page"/> is on screen right now, for a host created after
    /// the page's <c>Appearing</c>. Follows the window's current-page chain from the top modal down
    /// through Shell, navigation, tabbed and flyout pages.
    /// </summary>
    internal static bool IsShowing(Page page)
    {
        if (page.Window is not { } window || window.Page is not { } root)
            return false;

        var top = root.Navigation?.ModalStack is { Count: > 0 } modals ? modals[^1] : root;
        if (OnChain(top, page))
            return true;

        // A modal can sit over a page that is still the root's current page — that one is not showing.
        return false;
    }

    static bool OnChain(Page? current, Page target)
    {
        for (var i = 0; current != null && i < 32; i++)
        {
            if (current == target)
                return true;

            if (current is FlyoutPage { IsPresented: true } presented && OnChain(presented.Flyout, target))
                return true;

            current = current switch
            {
                Shell shell => shell.CurrentPage,
                NavigationPage navigation => navigation.CurrentPage,
                FlyoutPage flyout => flyout.Detail,
                TabbedPage tabbed => tabbed.CurrentPage,
                _ => null
            };
        }

        return false;
    }
}
