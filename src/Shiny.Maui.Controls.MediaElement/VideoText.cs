namespace Shiny.Maui.Controls.Media;

/// <summary>
/// Display text whose letters are windows onto a looping video — the video plays only inside the glyphs,
/// and everything around them is transparent, so whatever is behind the control shows through.
/// </summary>
/// <remarks>
/// <para>
/// Decorative by design: there is no transport bar, no seek, no fullscreen. The video starts as soon as it
/// opens and loops forever. Sound is off unless you set <see cref="IsMuted"/> to <c>false</c>.
/// </para>
/// <para>
/// The mask is drawn natively over the same <see cref="IMediaPlayerBackend"/> a <see cref="MediaElement"/>
/// uses: a layer mask on the <c>AVPlayerLayer</c> on iOS, Mac Catalyst and macOS, and a <c>DST_IN</c>
/// composite over a <c>TextureView</c> on Android. Windows and Linux have no masked output yet and show
/// the text in <see cref="FallbackTextColor"/> instead (see <see cref="IsMaskSupported"/>).
/// </para>
/// <para>
/// Leave <see cref="FontSize"/> at <c>0</c> to auto-fit: the text grows until its longest line (or the
/// whole block, if that is the tighter fit) fills 90% of the control. Only explicit line breaks split it.
/// </para>
/// </remarks>
/// <example>
/// <code language="xaml">
/// &lt;media:VideoText Source="https://example.com/ocean.mp4"
///                  Text="OCEAN"
///                  HeightRequest="200" /&gt;
/// </code>
/// </example>
public class VideoText : ContentView
{
    // ── properties ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The video playing inside the letters. Accepts a bare string in XAML, classified by
    /// <see cref="MediaSource.Parse"/> as a URI, a filesystem path, or a packaged resource.
    /// </summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source), typeof(MediaSource), typeof(VideoText), null,
        propertyChanged: (b, _, _) => ((VideoText)b).LoadSource());

    /// <summary>The text cut out of the video. Use a line break (<c>&amp;#10;</c> in XAML) for several lines.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(VideoText), null,
        propertyChanged: (b, _, _) => ((VideoText)b).ApplyText());

    /// <summary>The font family — a registered MAUI font alias or a platform font name. <c>null</c> uses the system font.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily), typeof(string), typeof(VideoText), null,
        propertyChanged: (b, _, _) => ((VideoText)b).ApplyText());

    /// <summary>The font size in device-independent units. <c>0</c> (the default) auto-fits the text to the control.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(VideoText), 0d,
        coerceValue: (_, v) => Math.Max(0d, (double)v),
        propertyChanged: (b, _, _) => ((VideoText)b).ApplyText());

    /// <summary>Bold and/or italic. Default <see cref="FontAttributes.Bold"/> — thin strokes show very little video.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        nameof(FontAttributes), typeof(FontAttributes), typeof(VideoText), FontAttributes.Bold,
        propertyChanged: (b, _, _) => ((VideoText)b).ApplyText());

    /// <summary>Silence the video's audio. Default <c>true</c>: VideoText is decoration, and decoration shouldn't make noise.</summary>
    public static readonly BindableProperty IsMutedProperty = BindableProperty.Create(
        nameof(IsMuted), typeof(bool), typeof(VideoText), true,
        propertyChanged: (b, _, v) => ((VideoText)b).backend?.SetMuted((bool)v));

    /// <summary>The colour of the plain text shown where no masked video output exists (Windows, Linux). <c>null</c> uses the label default.</summary>
    public static readonly BindableProperty FallbackTextColorProperty = BindableProperty.Create(
        nameof(FallbackTextColor), typeof(Color), typeof(VideoText), null,
        propertyChanged: (b, _, _) => ((VideoText)b).ApplyText());

    /// <inheritdoc cref="SourceProperty"/>
    public MediaSource? Source
    {
        get => (MediaSource?)this.GetValue(SourceProperty);
        set => this.SetValue(SourceProperty, value);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => (string?)this.GetValue(TextProperty);
        set => this.SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="FontFamilyProperty"/>
    public string? FontFamily
    {
        get => (string?)this.GetValue(FontFamilyProperty);
        set => this.SetValue(FontFamilyProperty, value);
    }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize
    {
        get => (double)this.GetValue(FontSizeProperty);
        set => this.SetValue(FontSizeProperty, value);
    }

    /// <inheritdoc cref="FontAttributesProperty"/>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)this.GetValue(FontAttributesProperty);
        set => this.SetValue(FontAttributesProperty, value);
    }

    /// <inheritdoc cref="IsMutedProperty"/>
    public bool IsMuted
    {
        get => (bool)this.GetValue(IsMutedProperty);
        set => this.SetValue(IsMutedProperty, value);
    }

    /// <inheritdoc cref="FallbackTextColorProperty"/>
    public Color? FallbackTextColor
    {
        get => (Color?)this.GetValue(FallbackTextColorProperty);
        set => this.SetValue(FallbackTextColorProperty, value);
    }

    /// <summary>
    /// Whether this build draws the text as a video mask. <c>false</c> on Windows and on the plain
    /// <c>net10.0</c> target the Linux head runs on, where the text is shown in <see cref="FallbackTextColor"/>.
    /// </summary>
    public static bool IsMaskSupported =>
#if ANDROID || IOS || MACCATALYST || MACOS
        true;
#else
        false;
#endif


    // ── state ────────────────────────────────────────────────────────────────────────────────────

    readonly VideoTextSurface? surface;
    readonly Label? fallback;
    IMediaPlayerBackend? backend;
    CancellationTokenSource? openCts;

    public VideoText()
    {
        if (IsMaskSupported)
        {
            this.surface = new VideoTextSurface();
            this.Content = this.surface;
        }
        else
        {
            this.fallback = new Label
            {
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                LineBreakMode = LineBreakMode.NoWrap
            };
            this.fallback.SizeChanged += (_, _) => this.ApplyFallbackText();
            this.Content = this.fallback;
        }

        this.ApplyText();
    }

    /// <summary>The playback state of the video, for diagnostics. VideoText drives playback itself.</summary>
    public MediaElementState State => this.backend?.State ?? MediaElementState.None;


    // ── backend lifecycle ────────────────────────────────────────────────────────────────────────
    //
    // Same rule as MediaElement: the player is created when a handler arrives, never in the constructor,
    // because a backend roots itself in process-wide sources the moment it exists and only a handler
    // disconnect ever tears it down.

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        if (this.surface is null)
            return;

        if (args.NewHandler is not null)
        {
            if (this.backend is not null)
                return;

            this.backend = MediaPlayerBackends.Create();
            if (this.backend is null)
                return;

            this.backend.VideoSizeChanged += this.OnVideoSizeChanged;
            this.surface.Backend = this.backend;
            return;
        }

        this.openCts?.Cancel();
        this.openCts?.Dispose();
        this.openCts = null;

        if (this.backend is null)
            return;

        this.backend.VideoSizeChanged -= this.OnVideoSizeChanged;
        this.surface.Backend = null;
        this.backend.Dispose();
        this.backend = null;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (this.Handler is not null && this.backend is not null)
            this.LoadSource();
    }

    void ApplyBackendSettings()
    {
        if (this.backend is null)
            return;

        this.backend.SetLooping(true);
        this.backend.SetMuted(this.IsMuted);
        this.backend.SetAspect(MediaAspect.AspectFill);
        this.backend.SetKeepScreenOn(false);
    }

    async void LoadSource()
    {
        if (this.backend is null)
            return;

        this.openCts?.Cancel();
        this.openCts?.Dispose();
        this.openCts = new CancellationTokenSource();
        var ct = this.openCts.Token;
        var source = this.Source;

        try
        {
            this.ApplyBackendSettings();
            await this.backend.OpenAsync(source, ct).ConfigureAwait(true);

            if (ct.IsCancellationRequested || this.backend is null)
                return;

            // Re-applied after open: AVPlayer settings live on the item, which OpenAsync just replaced.
            this.ApplyBackendSettings();

            if (source is not null)
                this.backend.Play();
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer Source
        }
        catch (Exception ex)
        {
            // Decorative control: a dead link leaves the letters empty rather than taking the page down.
            System.Diagnostics.Debug.WriteLine($"[VideoText] Could not open '{source}': {ex.Message}");
        }
    }

    void OnVideoSizeChanged(object? sender, EventArgs e)
    {
        if (this.surface is null || this.backend is null)
            return;

        this.surface.VideoSize = this.backend.VideoSize;
        this.surface.Handler?.UpdateValue(nameof(VideoTextSurface.VideoSize));
    }


    // ── text ─────────────────────────────────────────────────────────────────────────────────────

    void ApplyText()
    {
        if (this.surface is not null)
        {
            this.surface.Text = this.Text;
            this.surface.FontFamily = this.FontFamily;
            this.surface.FontSize = this.FontSize;
            this.surface.FontAttributes = this.FontAttributes;
            this.surface.Handler?.UpdateValue(nameof(VideoTextSurface.Text));
        }

        if (this.fallback is not null)
        {
            this.fallback.Text = this.Text;
            this.fallback.FontFamily = this.FontFamily;
            this.fallback.FontAttributes = this.FontAttributes;

            if (this.FallbackTextColor is not null)
                this.fallback.TextColor = this.FallbackTextColor;
            else
                this.fallback.ClearValue(Label.TextColorProperty);

            this.ApplyFallbackText();
        }
    }

    // The fallback has no native measuring to auto-fit with, so it approximates: average glyph advance of
    // a bold display face is ~0.62em, and a line is ~1.2em tall. Close enough for a placeholder.
    void ApplyFallbackText()
    {
        if (this.fallback is null)
            return;

        if (this.FontSize > 0)
        {
            this.fallback.FontSize = this.FontSize;
            return;
        }

        var lines = VideoTextLayout.SplitLines(this.Text);
        var longest = lines.Count == 0 ? 0 : lines.Max(l => l.Length);
        var fitted = VideoTextLayout.FitFontSize(
            longest * 0.62 * 100,
            lines.Count * 1.2 * 100,
            100,
            this.fallback.Width,
            this.fallback.Height);

        if (fitted > 0)
            this.fallback.FontSize = fitted;
    }
}
