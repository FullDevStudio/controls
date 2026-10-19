namespace Shiny.Maui.Controls.QuickEntry;

/// <summary>
/// Configuration for the quick entry popup — the borderless, always-on-top window that opens
/// over whatever the user is doing, in the style of Claude Desktop's quick entry or the
/// Windows Copilot key.
/// </summary>
/// <remarks>
/// The same instance is registered as a singleton and handed to <see cref="IQuickEntryService"/>,
/// so most values can be changed at runtime. Ones that are baked into the native window when it is
/// first created (<see cref="ShowInTaskbar"/>, <see cref="JoinAllSpaces"/>) are noted below.
/// </remarks>
public sealed class QuickEntryOptions
{
    /// <summary>
    /// How the popup is presented. Default <see cref="QuickEntryPresentation.Auto"/> — a native
    /// desktop window where one is available, the in-app overlay everywhere else.
    /// </summary>
    public QuickEntryPresentation Presentation { get; set; } = QuickEntryPresentation.Auto;

    /// <summary>
    /// Appearance of the screen-edge glow. What lights it is <see cref="ScreenGlow"/>; it can also
    /// be driven by hand through <see cref="IQuickEntryService"/>.
    /// </summary>
    public ScreenGlowOptions Glow { get; } = new();

    /// <summary>
    /// Global hotkey that toggles the popup, e.g. <c>"Ctrl+Alt+Space"</c> or <c>"Cmd+Opt+Space"</c>.
    /// <b>Desktop only</b> — a system-wide key grab does not exist on mobile or in a browser, so this
    /// is ignored unless the <c>Shiny.Maui.Controls.Desktop</c> add-on is registered.
    /// Parsed with the same grammar as tray menu accelerators. Leave null to register no hotkey and
    /// drive the popup from a tray icon click or <see cref="IQuickEntryService.Show"/>.
    /// </summary>
    /// <remarks>
    /// Assigning a new value after startup re-registers the hotkey. A combination already owned by
    /// another application cannot be claimed; <see cref="IGlobalHotKeyService.Register"/> reports
    /// that by returning null rather than throwing.
    /// </remarks>
    public string? HotKey { get; set; }

    /// <summary>Popup width in device-independent pixels. Default 720.</summary>
    public double Width { get; set; } = 720d;

    /// <summary>
    /// Height of the popup when its content is collapsed to just the entry row. Default 76.
    /// The window grows towards <see cref="MaxHeight"/> as content (suggestions, a response)
    /// appears — see <see cref="IQuickEntryService.Resize"/>.
    /// </summary>
    public double CollapsedHeight { get; set; } = 76d;

    /// <summary>Ceiling the popup will not grow past, in device-independent pixels. Default 560.</summary>
    public double MaxHeight { get; set; } = 560d;

    /// <summary>Where the popup appears. Default <see cref="QuickEntryPlacement.TopCenter"/>.</summary>
    public QuickEntryPlacement Placement { get; set; } = QuickEntryPlacement.TopCenter;

    /// <summary>
    /// For <see cref="QuickEntryPlacement.TopCenter"/>: the popup's top edge as a fraction of the
    /// screen's working height. Default 0.18 — high enough to feel like a HUD, low enough to clear
    /// the menu bar / taskbar.
    /// </summary>
    public double TopMarginRatio { get; set; } = 0.18d;

    /// <summary>
    /// For <see cref="QuickEntryPlacement.BottomCenter"/>: the gap between the popup's bottom edge
    /// and the bottom of the screen's working area, as a fraction of that area's height. Default
    /// 0.12 — clear of the dock or taskbar without floating in the middle of the screen.
    /// </summary>
    public double BottomMarginRatio { get; set; } = 0.12d;

    /// <summary>Screen X for <see cref="QuickEntryPlacement.Manual"/>, in device-independent pixels from the left of the primary screen.</summary>
    public double X { get; set; }

    /// <summary>Screen Y for <see cref="QuickEntryPlacement.Manual"/>, in device-independent pixels from the top of the primary screen.</summary>
    public double Y { get; set; }

    /// <summary>Close the popup when it loses focus to another application. Default true.</summary>
    public bool DismissOnFocusLost { get; set; } = true;

    /// <summary>
    /// Close the popup on Escape. Default true. Content implementing
    /// <see cref="IQuickEntryKeyHandler"/> gets first refusal, so a prompt view can clear its
    /// text on the first Escape and let the second one close the window.
    /// </summary>
    public bool DismissOnEscape { get; set; } = true;

    /// <summary>
    /// Bring the popup to the foreground and give it keyboard focus when shown. Default true —
    /// turn it off for a purely informational HUD that should not steal the user's typing.
    /// </summary>
    public bool ActivateOnShow { get; set; } = true;

    /// <summary>
    /// Show an entry for the popup in the taskbar / dock window list. Default false. Desktop
    /// presentation only; applied when the native window is created.
    /// </summary>
    public bool ShowInTaskbar { get; set; }

    /// <summary>
    /// macOS only, and only in <see cref="QuickEntryPresentation.Desktop"/>: let the popup appear on
    /// every Space and over full-screen apps. Default true. Applied when the native window is created.
    /// </summary>
    public bool JoinAllSpaces { get; set; } = true;

    /// <summary>
    /// Builds the popup's content. Defaults to a new
    /// <see cref="Shiny.Maui.Controls.Desktop.QuickEntry.PromptView"/>. Replace it to host any
    /// view you like — the popup is just a window.
    /// </summary>
    public Func<View>? ContentFactory { get; set; }

    /// <summary>
    /// Rebuild the content view on every open rather than reusing one instance. Default false, so
    /// a half-typed prompt survives an accidental dismiss.
    /// </summary>
    public bool RecreateContentOnShow { get; set; }


    /// <summary>
    /// Resize the window to fit its content as the content grows and shrinks, clamped between
    /// <see cref="CollapsedHeight"/> and <see cref="MaxHeight"/>. Default true. Turn it off and
    /// call <see cref="IQuickEntryService.Resize"/> yourself if your content measures expensively.
    /// </summary>
    public bool AutoSize { get; set; } = true;

    /// <summary>
    /// Whether opening the popup also lights the screen-edge glow — the Siri-style colour wash around
    /// the display border. Default <see cref="ScreenGlowTrigger.None"/>. Its appearance comes from
    /// <see cref="Glow"/>, and it can be driven by hand through <see cref="IQuickEntryService"/>.
    /// </summary>
    public ScreenGlowTrigger ScreenGlow { get; set; } = ScreenGlowTrigger.None;
    /// <summary>
    /// Backdrop painted behind an in-app popup, dimming the page under it. Transparent disables the
    /// scrim. Ignored by desktop presentation, which has no page to dim.
    /// </summary>
    public Color ScrimColor { get; set; } = Color.FromRgba(0, 0, 0, 0.35);

    /// <summary>
    /// Close an in-app popup when the scrim behind it is tapped. Default true — the touch equivalent
    /// of <see cref="DismissOnFocusLost"/>, which has no meaning without a window manager.
    /// </summary>
    public bool DismissOnScrimTap { get; set; } = true;

    /// <summary>
    /// Title assigned to the underlying MAUI window in desktop presentation. Never visible (the popup
    /// is borderless) but it shows up in accessibility tooling and window lists. Default "Quick Entry".
    /// </summary>
    public string WindowTitle { get; set; } = "Quick Entry";

    /// <summary>
    /// AutomationId given to the popup's content when it is built, so a UI test can reach it — and,
    /// for the default <see cref="PromptView"/>, its parts (<c>&lt;id&gt;_Entry</c>, <c>&lt;id&gt;_Submit</c>, …).
    /// Default null. Content from <see cref="ContentFactory"/> that already carries an id keeps its own.
    /// </summary>
    public string? AutomationId { get; set; }
}
