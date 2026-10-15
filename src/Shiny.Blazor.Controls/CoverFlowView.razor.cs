using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A Cover Flow carousel: the current item faces the viewer, its neighbours tilt away in 3D and stack
/// up behind it on either side. Drag, flick, scroll, tap a side item or use the arrow keys to move.
/// </summary>
/// <remarks>
/// <para>
/// The position is one continuous number owned by <c>cover-flow.js</c>; each slide's transform is a
/// function of its distance from it, so dragging, settling and jumping are all the same motion. Only
/// the slides within <see cref="VisibleItems"/> (plus a small buffer) of the centre are rendered, and
/// the engine reports each new centre as it crosses it so the window follows the motion.
/// </para>
/// <para>
/// With <see cref="Loop"/> the engine works in <i>virtual</i> indices that run past both ends of the
/// list; <see cref="CurrentPosition"/> is always the real item index.
/// </para>
/// </remarks>
public partial class CoverFlowView<TItem> : IAsyncDisposable
{
    const int WindowBuffer = 2;

    ElementReference rootRef;
    ElementReference stageRef;
    IJSObjectReference? module;
    DotNetObjectReference<CoverFlowView<TItem>>? selfRef;
    bool disposed;
    bool initialized;

    /// <summary>The virtual index at the centre — what the render window is built around.</summary>
    internal int VirtualCenter { get; private set; }

    /// <summary>The last real position this component knows about, to tell a caller's change from its own echo.</summary>
    int knownPosition;
    int? pendingGoTo;
    bool pendingJump;
    bool? lastLoop;
    int lastCount = -1;

    [Inject] IJSRuntime JS { get; set; } = default!;

    // ----- Data / templates -----

    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Rendered below the stage for the current item (an album's title and artist, say).</summary>
    [Parameter] public RenderFragment<TItem>? CaptionTemplate { get; set; }

    /// <summary>Rendered when <see cref="Items"/> is null or empty.</summary>
    [Parameter] public RenderFragment? EmptyTemplate { get; set; }

    // ----- Geometry -----

    [Parameter] public double ItemWidth { get; set; } = 200;
    [Parameter] public double ItemHeight { get; set; } = 200;

    /// <summary>Degrees each side item turns away from the viewer.</summary>
    [Parameter] public double SideAngle { get; set; } = 60;

    /// <summary>Scale of the side items relative to the centre one.</summary>
    [Parameter] public double SideScale { get; set; } = 0.85;

    /// <summary>Opacity the side items ease to by the last visible one.</summary>
    [Parameter] public double SideOpacity { get; set; } = 1.0;

    /// <summary>Distance in pixels from the centre item to its immediate neighbours.</summary>
    [Parameter] public double CenterSpacing { get; set; } = 130;

    /// <summary>Distance in pixels between successive side items in each stack.</summary>
    [Parameter] public double ItemSpacing { get; set; } = 50;

    /// <summary>How many items are shown on each side of the centre.</summary>
    [Parameter] public int VisibleItems { get; set; } = 4;

    /// <summary>Distance of the 3D camera in pixels — smaller is more dramatic (Blazor only).</summary>
    [Parameter] public double Perspective { get; set; } = 800;

    // ----- Behaviour -----

    /// <summary>Wrap around past the first and last item.</summary>
    [Parameter] public bool Loop { get; set; }

    /// <summary>Draw a faded mirror image of each item beneath it.</summary>
    [Parameter] public bool ShowReflection { get; set; }

    /// <summary>Opacity of the reflection at the item's edge.</summary>
    [Parameter] public double ReflectionOpacity { get; set; } = 0.3;

    /// <summary>Length of the settle animation in milliseconds.</summary>
    [Parameter] public int AnimationDuration { get; set; } = 300;

    /// <summary>Allow pointer drag and horizontal wheel/trackpad scrolling.</summary>
    [Parameter] public bool IsSwipeEnabled { get; set; } = true;

    [Parameter] public string? AriaLabel { get; set; }

    // ----- Binding / events -----

    /// <summary>Index of the item in the centre (two-way bindable).</summary>
    [Parameter] public int CurrentPosition { get; set; }
    [Parameter] public EventCallback<int> CurrentPositionChanged { get; set; }

    /// <summary>The centre item was tapped, clicked or had Enter pressed on it.</summary>
    [Parameter] public EventCallback<TItem> ItemSelected { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }

    // ----- Derived -----

    int Count => Items?.Count ?? 0;

    internal int RealIndex(int virtualIndex) => Count == 0 ? 0 : ((virtualIndex % Count) + Count) % Count;

    TItem? CurrentItem => Count == 0 ? default : Items![RealIndex(VirtualCenter)];

    string RootClasses => string.Join(' ',
        "shiny-coverflow",
        ShowReflection ? "has-reflection" : "",
        IsSwipeEnabled ? "is-swipeable" : "").Trim();

    internal string RootStyle =>
        $"--coverflow-item-w:{Css(ItemWidth)}px;--coverflow-item-h:{Css(ItemHeight)}px;" +
        $"--coverflow-perspective:{Css(Perspective)}px;" +
        $"--coverflow-reflection-opacity:{Css(Math.Clamp(ReflectionOpacity, 0, 1))};";

    static string Css(double d) => d.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The virtual indices to render: the centre and <see cref="VisibleItems"/> + a buffer on each side,
    /// clipped to the list when not looping and never repeating an item when looping.
    /// </summary>
    internal IEnumerable<int> Window()
    {
        if (Count == 0)
            yield break;

        var half = Math.Max(0, VisibleItems) + WindowBuffer;
        if (Loop)
        {
            var span = Math.Min(half * 2 + 1, Count);
            var first = VirtualCenter - (span - 1) / 2;
            for (var v = first; v < first + span; v++)
                yield return v;
        }
        else
        {
            var from = Math.Max(0, VirtualCenter - half);
            var to = Math.Min(Count - 1, VirtualCenter + half);
            for (var v = from; v <= to; v++)
                yield return v;
        }
    }

    /// <summary>The virtual index of a real item nearest the current centre — the short way round when looping.</summary>
    internal int NearestVirtual(int index)
    {
        index = Math.Clamp(index, 0, Math.Max(0, Count - 1));
        if (!Loop || Count == 0)
            return index;

        var delta = index - RealIndex(VirtualCenter);
        if (delta > Count / 2) delta -= Count;
        else if (delta < -(Count - 1) / 2) delta += Count;
        return VirtualCenter + delta;
    }

    // ----- Lifecycle -----

    protected override void OnParametersSet()
    {
        var count = Count;
        var clamped = count == 0 ? 0 : Math.Clamp(CurrentPosition, 0, count - 1);

        if (lastLoop is null)
        {
            // First pass: start where the caller asked.
            VirtualCenter = clamped;
            knownPosition = clamped;
        }
        else if (lastLoop != Loop || (lastCount != count && RealIndex(VirtualCenter) != VirtualCenter))
        {
            // Virtual indices mean something different now; collapse back to the real index.
            VirtualCenter = clamped;
            knownPosition = clamped;
            pendingJump = true;
        }
        else if (lastCount != count && VirtualCenter > Math.Max(0, count - 1))
        {
            VirtualCenter = clamped;
            knownPosition = clamped;
            pendingJump = true;
        }
        else if (clamped != knownPosition)
        {
            // The caller moved us.
            knownPosition = clamped;
            if (initialized)
                pendingGoTo = NearestVirtual(clamped);
            else
                VirtualCenter = clamped;
        }

        if (CurrentPosition != clamped)
            CurrentPosition = clamped;

        lastLoop = Loop;
        lastCount = count;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Count == 0)
        {
            // The stage left the DOM with the last item; the next one gets a fresh engine.
            if (initialized && module is not null)
            {
                initialized = false;
                await module.InvokeVoidAsync("dispose", stageRef);
            }
            return;
        }

        if (module is null)
        {
            var imported = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Shiny.Blazor.Controls/cover-flow.js");
            if (disposed) { await imported.ReleaseLateAsync(); return; }
            module = imported;
            selfRef ??= DotNetObjectReference.Create(this);
        }

        if (!initialized)
        {
            // Set before the await: nothing in init calls back, but a render queued meanwhile must
            // take the update path rather than initialising a second engine.
            initialized = true;
            pendingJump = false;
            pendingGoTo = null;
            await module.InvokeVoidAsync("init", rootRef, stageRef, selfRef, BuildOptions());
            return;
        }

        await module.InvokeVoidAsync("update", stageRef, BuildOptions());

        if (pendingJump)
        {
            pendingJump = false;
            await module.InvokeVoidAsync("goTo", stageRef, VirtualCenter, false);
        }
        if (pendingGoTo is { } target)
        {
            pendingGoTo = null;
            await module.InvokeVoidAsync("goTo", stageRef, target, true);
        }
    }

    CoverFlowJsOptions BuildOptions() => new()
    {
        Count = Count,
        Center = VirtualCenter,
        Loop = Loop,
        SideAngle = SideAngle,
        SideScale = SideScale,
        SideOpacity = Math.Clamp(SideOpacity, 0, 1),
        CenterSpacing = CenterSpacing,
        ItemSpacing = ItemSpacing,
        VisibleItems = Math.Max(0, VisibleItems),
        AnimationDuration = Math.Max(0, AnimationDuration),
        IsSwipeEnabled = IsSwipeEnabled
    };

    // ----- Navigation -----

    public async Task NextAsync()
    {
        if (module is not null && initialized)
            await module.InvokeVoidAsync("next", stageRef);
    }

    public async Task PreviousAsync()
    {
        if (module is not null && initialized)
            await module.InvokeVoidAsync("prev", stageRef);
    }

    /// <summary>Bring the item at <paramref name="index"/> to the centre.</summary>
    public async Task GoToAsync(int index, bool animate = true)
    {
        if (Count == 0)
            return;

        var target = NearestVirtual(index);
        if (module is not null && initialized)
        {
            await module.InvokeVoidAsync("goTo", stageRef, target, animate);
            if (!animate)
                await OnSettled(target);
        }
        else
        {
            await OnSettled(target);
        }
    }

    Task OnKeyDown(KeyboardEventArgs e) => e.Key switch
    {
        "ArrowRight" => NextAsync(),
        "ArrowLeft" => PreviousAsync(),
        "Home" => GoToAsync(0),
        "End" => GoToAsync(Count - 1),
        "Enter" or " " => SelectCurrentAsync(),
        _ => Task.CompletedTask
    };

    async Task SelectCurrentAsync()
    {
        if (Count > 0 && ItemSelected.HasDelegate)
            await ItemSelected.InvokeAsync(Items![RealIndex(VirtualCenter)]);
    }

    // ----- JS callbacks -----

    /// <summary>The engine crossed into a new centre mid-motion; move the render window with it.</summary>
    [JSInvokable]
    public Task OnCenterChanged(int virtualIndex)
    {
        if (ApplyCenter(virtualIndex))
            StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Motion stopped on <paramref name="virtualIndex"/>.</summary>
    [JSInvokable]
    public async Task OnSettled(int virtualIndex)
    {
        var moved = ApplyCenter(virtualIndex);
        var real = RealIndex(VirtualCenter);
        if (real != knownPosition || real != CurrentPosition)
        {
            knownPosition = real;
            CurrentPosition = real;
            if (CurrentPositionChanged.HasDelegate)
                await CurrentPositionChanged.InvokeAsync(real);
            moved = true;
        }
        if (moved && !disposed)
            StateHasChanged();
    }

    /// <summary>The centre item was tapped.</summary>
    [JSInvokable]
    public async Task OnItemClicked(int virtualIndex)
    {
        if (Count > 0 && ItemSelected.HasDelegate)
            await ItemSelected.InvokeAsync(Items![RealIndex(virtualIndex)]);
    }

    /// <summary>Moves the window's centre; false when nothing changed.</summary>
    internal bool ApplyCenter(int virtualIndex)
    {
        if (!Loop)
            virtualIndex = Math.Clamp(virtualIndex, 0, Math.Max(0, Count - 1));
        if (virtualIndex == VirtualCenter)
            return false;
        VirtualCenter = virtualIndex;
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", stageRef);
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            catch (JSException) { }
            catch (ObjectDisposedException) { }
        }
        selfRef?.Dispose();
    }

    sealed class CoverFlowJsOptions
    {
        public int Count { get; set; }
        public int Center { get; set; }
        public bool Loop { get; set; }
        public double SideAngle { get; set; }
        public double SideScale { get; set; }
        public double SideOpacity { get; set; }
        public double CenterSpacing { get; set; }
        public double ItemSpacing { get; set; }
        public int VisibleItems { get; set; }
        public int AnimationDuration { get; set; }
        public bool IsSwipeEnabled { get; set; }
    }
}
