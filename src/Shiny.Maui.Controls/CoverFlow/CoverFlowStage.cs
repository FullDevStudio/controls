using Microsoft.Maui.Layouts;

namespace Shiny.Maui.Controls;

/// <summary>
/// The clipped surface the covers stand on. Every cover is arranged in the same centred box and placed
/// purely by its transform, so moving the position never needs a layout pass - only realizing or
/// releasing a cover does.
/// </summary>
sealed class CoverFlowStage : Layout
{
    readonly CoverFlowView owner;

    public CoverFlowStage(CoverFlowView owner)
    {
        this.owner = owner;
        this.IsClippedToBounds = true;
    }

    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    sealed class Manager(CoverFlowStage stage) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint) => stage.owner.MeasureStage(widthConstraint, heightConstraint);
        public Size ArrangeChildren(Rect bounds) => stage.owner.ArrangeStage(bounds);
    }
}


/// <summary>
/// One realized cover: the item, and - when reflections are on - a second realization of the same
/// template flipped upside down beneath it and faded into the surface colour.
/// </summary>
/// <remarks>
/// The reflection is a separate realization rather than a copy because a MAUI view cannot be in two
/// places at once. Both live inside the slot, so the slot's rotation and scale carry them together.
/// </remarks>
sealed class CoverFlowSlot : Layout
{
    readonly ReflectionHost? reflection;

    public CoverFlowSlot(View item, View? reflectionView, Color surface)
    {
        this.Item = item;
        this.InputTransparent = true;
        this.CascadeInputTransparent = true;
        this.Children.Add(item);

        if (reflectionView is not null)
        {
            this.reflection = new ReflectionHost(reflectionView, surface);
            this.Children.Add(this.reflection);
        }
    }

    /// <summary>The template the views came from, so a recycled slot is only reused for the same template.</summary>
    public DataTemplate? Template { get; init; }

    /// <summary>The virtual index (unwrapped when looping) this slot currently shows.</summary>
    public int VirtualIndex { get; set; }

    public View Item { get; }

    public View? ReflectionView => this.reflection?.Flipped;

    public bool HasReflection => this.reflection is not null;

    public double ItemWidth { get; set; }

    public double ItemHeight { get; set; }

    public double ReflectionHeight { get; set; }

    public double ReflectionOpacity
    {
        set
        {
            if (this.reflection is not null)
                this.reflection.Opacity = value;
        }
    }

    public void SetBindingContextFor(object? item)
    {
        this.Item.BindingContext = item;
        if (this.reflection is not null)
            this.reflection.Flipped.BindingContext = item;
    }

    public void SetSurface(Color color) => this.reflection?.SetSurface(color);

    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    sealed class Manager(CoverFlowSlot slot) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint)
        {
            slot.Item.Measure(slot.ItemWidth, slot.ItemHeight);
            slot.reflection?.Measure(slot.ItemWidth, slot.ReflectionHeight);
            return new Size(slot.ItemWidth, slot.ItemHeight + slot.ReflectionHeight);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            slot.Item.Arrange(new Rect(0, 0, slot.ItemWidth, slot.ItemHeight));
            slot.reflection?.Arrange(new Rect(0, slot.ItemHeight, slot.ItemWidth, slot.ReflectionHeight));
            return bounds.Size;
        }
    }


    /// <summary>Clips the flipped copy to the reflection band and lays the fade over it.</summary>
    sealed class ReflectionHost : Layout
    {
        readonly BoxView fade;
        readonly GradientStop clear;
        readonly GradientStop solid;

        public ReflectionHost(View flipped, Color surface)
        {
            this.Flipped = flipped;
            this.IsClippedToBounds = true;
            this.InputTransparent = true;

            // Flipped about its own centre: arranged at the top of the band with the item's full
            // height, the item's bottom edge lands against the item above - which is what a mirror does.
            flipped.ScaleY = -1;
            flipped.AnchorY = 0.5;

            this.clear = new GradientStop(surface.WithAlpha(0), 0);
            this.solid = new GradientStop(surface, 1);
            this.fade = new BoxView
            {
                InputTransparent = true,
                Color = Colors.Transparent,
                Background = new LinearGradientBrush([this.clear, this.solid], new Point(0, 0), new Point(0, 1))
            };

            this.Children.Add(flipped);
            this.Children.Add(this.fade);
        }

        public View Flipped { get; }

        public void SetSurface(Color color)
        {
            this.clear.Color = color.WithAlpha(0);
            this.solid.Color = color;
        }

        protected override ILayoutManager CreateLayoutManager() => new Manager(this);

        sealed class Manager(ReflectionHost host) : ILayoutManager
        {
            Size full;

            public Size Measure(double widthConstraint, double heightConstraint)
            {
                // The flipped copy is the item's own size; only the band it shows through is short.
                var owner = (CoverFlowSlot?)host.Parent;
                this.full = new Size(widthConstraint, owner?.ItemHeight ?? heightConstraint);
                host.Flipped.Measure(this.full.Width, this.full.Height);
                host.fade.Measure(widthConstraint, heightConstraint);
                return new Size(widthConstraint, heightConstraint);
            }

            public Size ArrangeChildren(Rect bounds)
            {
                host.Flipped.Arrange(new Rect(0, 0, bounds.Width, this.full.Height > 0 ? this.full.Height : bounds.Height));
                host.fade.Arrange(new Rect(0, 0, bounds.Width, bounds.Height));
                return bounds.Size;
            }
        }
    }
}
