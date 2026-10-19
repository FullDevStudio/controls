using System.Diagnostics;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.ImageEditor;

public partial class ImageEditor
{
    const float CropHandleHitRadius = 24f;
    const long DoubleTapWindowMs = 300;
    const float DoubleTapSlop = 40f;

    // Every tool — including zoom/pan — is driven from GraphicsView's interaction events rather
    // than from gesture recognizers. Recognizers and interaction events fight over the same
    // touches (the recognizer wins and the tool never sees the drag), and the interaction events
    // carry the full touch array, so a two-finger pinch can be handled directly. The upshot is
    // that pinch-zoom and two-finger pan stay live in draw / crop / text mode.
    PointF touchStartPoint;
    CropHandle activeCropHandle = CropHandle.None;
    RectF cropStartRect;
    bool isDragging;

    bool isPinching;
    float pinchStartDistance;
    float pinchStartScale;
    PointF pinchStartWorld;

    bool isPanning;
    PointF panStartScreen;
    float panStartOffsetX;
    float panStartOffsetY;

    long lastTapTicks;
    PointF lastTapPoint;

    void SetupGestures()
    {
        graphicsView.StartInteraction += OnStartInteraction;
        graphicsView.DragInteraction += OnDragInteraction;
        graphicsView.EndInteraction += OnEndInteraction;
        graphicsView.CancelInteraction += OnCancelInteraction;
    }

    #region Interaction dispatch

    void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0)
            return;

        if (e.Touches.Length >= 2)
        {
            BeginPinch(e.Touches);
            return;
        }

        var screen = e.Touches[0];
        var point = drawable.ScreenToWorld(screen);

        // Double-tap zoom only in the neutral tool — in Draw or Text mode a quick second tap is
        // the user dotting the image, not asking to zoom
        if (CurrentToolMode is ImageEditorToolMode.Move or ImageEditorToolMode.None && IsDoubleTap(screen))
        {
            ToggleZoomAt(screen);
            return;
        }

        switch (CurrentToolMode)
        {
            case ImageEditorToolMode.Text:
                HandleTextPlacement(point);
                return;

            case ImageEditorToolMode.Move:
            case ImageEditorToolMode.None:
                BeginPan(screen);
                return;

            case ImageEditorToolMode.Crop when drawable.ActiveCropRect.HasValue:
                touchStartPoint = point;
                isDragging = true;
                cropStartRect = drawable.ActiveCropRect.Value;
                activeCropHandle = HitTestCropHandle(point);

                // Tapping outside the crop box pans the image instead of fighting the user
                if (activeCropHandle == CropHandle.None)
                {
                    isDragging = false;
                    BeginPan(screen);
                }
                return;

            case ImageEditorToolMode.Draw:
            {
                var imageRect = drawable.GetImageRect();
                if (imageRect is not { Width: > 0, Height: > 0 } || !imageRect.Contains(point))
                    return;

                touchStartPoint = point;
                isDragging = true;
                drawable.ActiveStrokePoints = [point];
                Invalidate();
                return;
            }

            case ImageEditorToolMode.Line:
            case ImageEditorToolMode.Arrow:
            {
                var imageRect = drawable.GetImageRect();
                if (imageRect is not { Width: > 0, Height: > 0 } || !imageRect.Contains(point))
                    return;

                touchStartPoint = point;
                isDragging = true;
                drawable.ActiveLineStart = point;
                drawable.ActiveLineEnd = point;
                drawable.ActiveLineIsArrow = CurrentToolMode == ImageEditorToolMode.Arrow;
                Invalidate();
                return;
            }

            case ImageEditorToolMode.Rectangle:
            case ImageEditorToolMode.Ellipse:
            case ImageEditorToolMode.Circle:
            {
                var imageRect = drawable.GetImageRect();
                if (imageRect is not { Width: > 0, Height: > 0 } || !imageRect.Contains(point))
                    return;

                touchStartPoint = point;
                isDragging = true;
                drawable.ActiveShapeStart = point;
                drawable.ActiveShapeEnd = point;
                drawable.ActiveShapeKind = ShapeFor(CurrentToolMode);
                drawable.ActiveFillColor = ShapeFillColor;
                Invalidate();
                return;
            }
        }
    }

    static ImageEditorShape ShapeFor(ImageEditorToolMode mode) => mode switch
    {
        ImageEditorToolMode.Ellipse => ImageEditorShape.Ellipse,
        ImageEditorToolMode.Circle => ImageEditorShape.Circle,
        _ => ImageEditorShape.Rectangle
    };

    void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0)
            return;

        if (isPinching)
        {
            if (e.Touches.Length >= 2)
                UpdatePinch(e.Touches);
            return;
        }

        // A second finger arriving mid-drag turns the gesture into a zoom
        if (e.Touches.Length >= 2 && AllowZoom)
        {
            AbandonActiveToolGesture();
            BeginPinch(e.Touches);
            return;
        }

        var screen = e.Touches[0];

        if (isPanning)
        {
            UpdatePan(screen);
            return;
        }

        if (!isDragging)
            return;

        var point = drawable.ScreenToWorld(screen);

        switch (CurrentToolMode)
        {
            case ImageEditorToolMode.Crop when activeCropHandle != CropHandle.None:
                HandleCropDrag(point);
                break;

            case ImageEditorToolMode.Draw when drawable.ActiveStrokePoints != null:
            {
                var imageRect = drawable.GetImageRect();
                if (imageRect is { Width: > 0, Height: > 0 })
                    drawable.ActiveStrokePoints.Add(ClampToRect(point, imageRect));
                Invalidate();
                break;
            }

            case ImageEditorToolMode.Line:
            case ImageEditorToolMode.Arrow:
            {
                if (drawable.ActiveLineStart.HasValue)
                {
                    var imageRect = drawable.GetImageRect();
                    if (imageRect is { Width: > 0, Height: > 0 })
                        drawable.ActiveLineEnd = ClampToRect(point, imageRect);
                    Invalidate();
                }
                break;
            }

            case ImageEditorToolMode.Rectangle:
            case ImageEditorToolMode.Ellipse:
            case ImageEditorToolMode.Circle:
            {
                if (drawable.ActiveShapeStart.HasValue)
                {
                    var imageRect = drawable.GetImageRect();
                    if (imageRect is { Width: > 0, Height: > 0 })
                        drawable.ActiveShapeEnd = ClampToRect(point, imageRect);
                    Invalidate();
                }
                break;
            }
        }
    }

    void OnEndInteraction(object? sender, TouchEventArgs e) => EndInteraction();

    void OnCancelInteraction(object? sender, EventArgs e)
    {
        AbandonActiveToolGesture();
        EndInteraction();
    }

    void EndInteraction()
    {
        if (isPinching)
        {
            isPinching = false;
            isDragging = false;
            return;
        }

        isPanning = false;

        if (!isDragging)
            return;

        isDragging = false;

        switch (CurrentToolMode)
        {
            case ImageEditorToolMode.Crop:
                activeCropHandle = CropHandle.None;
                break;

            case ImageEditorToolMode.Draw:
                CommitCurrentStroke();
                break;

            case ImageEditorToolMode.Line:
            case ImageEditorToolMode.Arrow:
                CommitCurrentLine();
                break;

            case ImageEditorToolMode.Rectangle:
            case ImageEditorToolMode.Ellipse:
            case ImageEditorToolMode.Circle:
                CommitCurrentShape();
                break;
        }
    }

    /// <summary>Drops an in-progress stroke/line/shape without committing it (a pinch took over).</summary>
    void AbandonActiveToolGesture()
    {
        drawable.ActiveStrokePoints = null;
        drawable.ActiveLineStart = null;
        drawable.ActiveLineEnd = null;
        drawable.ActiveShapeStart = null;
        drawable.ActiveShapeEnd = null;
        activeCropHandle = CropHandle.None;
        isDragging = false;
        isPanning = false;
        Invalidate();
    }

    static PointF ClampToRect(PointF point, RectF rect) => new(
        Math.Clamp(point.X, rect.X, rect.Right),
        Math.Clamp(point.Y, rect.Y, rect.Bottom));

    #endregion

    #region Pinch / pan

    void BeginPinch(PointF[] touches)
    {
        if (!AllowZoom)
            return;

        AbandonActiveToolGesture();

        var mid = Midpoint(touches);
        pinchStartDistance = Distance(touches[0], touches[1]);
        pinchStartScale = zoomScale;
        pinchStartWorld = drawable.ScreenToWorld(mid);
        isPinching = pinchStartDistance > 1f;
    }

    void UpdatePinch(PointF[] touches)
    {
        if (pinchStartDistance <= 1f)
            return;

        var mid = Midpoint(touches);
        var distance = Distance(touches[0], touches[1]);
        var scale = Math.Clamp(
            pinchStartScale * (distance / pinchStartDistance),
            (float)Math.Max(0.1, MinZoom),
            (float)Math.Max(MinZoom, MaxZoom));

        // Anchoring the world point captured at pinch-start to the *current* midpoint gives
        // pinch and two-finger pan in a single expression
        ApplyTransform(scale, mid, pinchStartWorld);
    }

    void BeginPan(PointF screen)
    {
        if (zoomScale <= 1.001f)
            return;

        isPanning = true;
        panStartScreen = screen;
        panStartOffsetX = zoomOffsetX;
        panStartOffsetY = zoomOffsetY;
    }

    void UpdatePan(PointF screen)
    {
        zoomOffsetX = panStartOffsetX + (screen.X - panStartScreen.X);
        zoomOffsetY = panStartOffsetY + (screen.Y - panStartScreen.Y);
        ClampOffsets();
        PushTransformToDrawable();
    }

    bool IsDoubleTap(PointF screen)
    {
        if (!AllowZoom)
            return false;

        var now = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
        var elapsed = now - lastTapTicks;
        var isDouble = elapsed is > 0 and < DoubleTapWindowMs && Distance(screen, lastTapPoint) < DoubleTapSlop;

        lastTapTicks = isDouble ? 0 : now;
        lastTapPoint = screen;
        return isDouble;
    }

    void ToggleZoomAt(PointF screen)
        => SetZoom(zoomScale > 1.05f ? 1f : Math.Min(2.5f, (float)MaxZoom), screen);

    static PointF Midpoint(PointF[] touches)
        => new((touches[0].X + touches[1].X) / 2f, (touches[0].Y + touches[1].Y) / 2f);

    static float Distance(PointF a, PointF b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    #endregion

    #region Line commit

    void CommitCurrentLine()
    {
        if (drawable.ActiveLineStart is not { } start || drawable.ActiveLineEnd is not { } end)
        {
            drawable.ActiveLineStart = null;
            drawable.ActiveLineEnd = null;
            return;
        }

        var imageRect = drawable.GetImageRect();
        if (imageRect is { Width: > 0, Height: > 0 })
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            // Ignore taps without drag
            if (dx * dx + dy * dy >= 4f)
            {
                state.Push(new EditActions.LineAction
                {
                    Start = new PointF((start.X - imageRect.X) / imageRect.Width, (start.Y - imageRect.Y) / imageRect.Height),
                    End = new PointF((end.X - imageRect.X) / imageRect.Width, (end.Y - imageRect.Y) / imageRect.Height),
                    StrokeColor = DrawStrokeColor,
                    StrokeWidth = (float)DrawStrokeWidth,
                    ReferenceWidth = imageRect.Width,
                    IsArrow = drawable.ActiveLineIsArrow
                });
            }
        }

        drawable.ActiveLineStart = null;
        drawable.ActiveLineEnd = null;
        Invalidate();
    }

    #endregion

    #region Shape commit

    void CommitCurrentShape()
    {
        if (drawable.ActiveShapeStart is not { } start || drawable.ActiveShapeEnd is not { } end)
        {
            drawable.ActiveShapeStart = null;
            drawable.ActiveShapeEnd = null;
            return;
        }

        var kind = drawable.ActiveShapeKind;
        drawable.ActiveShapeStart = null;
        drawable.ActiveShapeEnd = null;

        var imageRect = drawable.GetImageRect();
        if (imageRect is { Width: > 0, Height: > 0 })
        {
            var rect = ImageEditorDrawable.BuildShapeRect(start, end, kind);

            // Ignore taps without a real drag — the same threshold the line tool uses
            if (rect.Width >= 2f && rect.Height >= 2f)
            {
                state.Push(new EditActions.ShapeAction
                {
                    Shape = kind,
                    Bounds = new RectF(
                        (rect.X - imageRect.X) / imageRect.Width,
                        (rect.Y - imageRect.Y) / imageRect.Height,
                        rect.Width / imageRect.Width,
                        rect.Height / imageRect.Height),
                    FillColor = ShapeFillColor,
                    StrokeColor = DrawStrokeColor,
                    StrokeWidth = (float)DrawStrokeWidth,
                    ReferenceWidth = imageRect.Width
                });
            }
        }

        Invalidate();
    }

    #endregion

    #region Crop Interaction

    void HandleCropDrag(PointF point)
    {
        var imageRect = drawable.GetImageRect();
        if (imageRect is not { Width: > 0, Height: > 0 })
            return;

        var dx = (point.X - touchStartPoint.X) / imageRect.Width;
        var dy = (point.Y - touchStartPoint.Y) / imageRect.Height;

        var crop = cropStartRect;
        RectF newCrop;

        switch (activeCropHandle)
        {
            case CropHandle.Move:
                newCrop = new RectF(
                    Math.Clamp(crop.X + dx, 0, 1 - crop.Width),
                    Math.Clamp(crop.Y + dy, 0, 1 - crop.Height),
                    crop.Width,
                    crop.Height);
                break;
            case CropHandle.TopLeft:
                newCrop = ResizeCrop(crop, dx, dy, 0, 0);
                break;
            case CropHandle.TopCenter:
                newCrop = ResizeCrop(crop, 0, dy, 0, 0);
                break;
            case CropHandle.TopRight:
                newCrop = ResizeCrop(crop, 0, dy, dx, 0);
                break;
            case CropHandle.MiddleLeft:
                newCrop = ResizeCrop(crop, dx, 0, 0, 0);
                break;
            case CropHandle.MiddleRight:
                newCrop = ResizeCrop(crop, 0, 0, dx, 0);
                break;
            case CropHandle.BottomLeft:
                newCrop = ResizeCrop(crop, dx, 0, 0, dy);
                break;
            case CropHandle.BottomCenter:
                newCrop = ResizeCrop(crop, 0, 0, 0, dy);
                break;
            case CropHandle.BottomRight:
                newCrop = ResizeCrop(crop, 0, 0, dx, dy);
                break;
            default:
                return;
        }

        drawable.ActiveCropRect = newCrop;
        Invalidate();
    }

    CropHandle HitTestCropHandle(PointF touchPoint)
    {
        if (!drawable.ActiveCropRect.HasValue)
            return CropHandle.None;

        var imageRect = drawable.GetImageRect();
        if (imageRect is not { Width: > 0, Height: > 0 })
            return CropHandle.None;

        var crop = drawable.ActiveCropRect.Value;
        var cropPixel = new RectF(
            imageRect.X + crop.X * imageRect.Width,
            imageRect.Y + crop.Y * imageRect.Height,
            crop.Width * imageRect.Width,
            crop.Height * imageRect.Height
        );

        // Hit testing runs in un-zoomed space, so the on-screen grab radius has to shrink
        // by the same factor the view was zoomed by
        var radius = CropHandleHitRadius / zoomScale;

        if (IsNear(touchPoint, cropPixel.X, cropPixel.Y, radius)) return CropHandle.TopLeft;
        if (IsNear(touchPoint, cropPixel.Right, cropPixel.Y, radius)) return CropHandle.TopRight;
        if (IsNear(touchPoint, cropPixel.X, cropPixel.Bottom, radius)) return CropHandle.BottomLeft;
        if (IsNear(touchPoint, cropPixel.Right, cropPixel.Bottom, radius)) return CropHandle.BottomRight;

        if (IsNear(touchPoint, cropPixel.Center.X, cropPixel.Y, radius)) return CropHandle.TopCenter;
        if (IsNear(touchPoint, cropPixel.Center.X, cropPixel.Bottom, radius)) return CropHandle.BottomCenter;
        if (IsNear(touchPoint, cropPixel.X, cropPixel.Center.Y, radius)) return CropHandle.MiddleLeft;
        if (IsNear(touchPoint, cropPixel.Right, cropPixel.Center.Y, radius)) return CropHandle.MiddleRight;

        if (IsNearHorizontalEdge(touchPoint, cropPixel.X, cropPixel.Right, cropPixel.Y, radius)) return CropHandle.TopCenter;
        if (IsNearHorizontalEdge(touchPoint, cropPixel.X, cropPixel.Right, cropPixel.Bottom, radius)) return CropHandle.BottomCenter;
        if (IsNearVerticalEdge(touchPoint, cropPixel.Y, cropPixel.Bottom, cropPixel.X, radius)) return CropHandle.MiddleLeft;
        if (IsNearVerticalEdge(touchPoint, cropPixel.Y, cropPixel.Bottom, cropPixel.Right, radius)) return CropHandle.MiddleRight;

        if (cropPixel.Contains(touchPoint))
            return CropHandle.Move;

        return CropHandle.None;
    }

    static RectF ResizeCrop(RectF crop, float dLeft, float dTop, float dRight, float dBottom)
    {
        const float minSize = 0.05f;

        var x = crop.X + dLeft;
        var y = crop.Y + dTop;
        var w = crop.Width - dLeft + dRight;
        var h = crop.Height - dTop + dBottom;

        if (w < minSize) { w = minSize; x = crop.X + crop.Width - minSize; }
        if (h < minSize) { h = minSize; y = crop.Y + crop.Height - minSize; }

        x = Math.Clamp(x, 0, 1 - minSize);
        y = Math.Clamp(y, 0, 1 - minSize);
        w = Math.Min(w, 1 - x);
        h = Math.Min(h, 1 - y);

        return new RectF(x, y, w, h);
    }

    static bool IsNear(PointF touch, float x, float y, float radius)
    {
        var dx = touch.X - x;
        var dy = touch.Y - y;
        return dx * dx + dy * dy <= radius * radius;
    }

    static bool IsNearHorizontalEdge(PointF touch, float x1, float x2, float y, float radius)
    {
        return touch.X >= x1 - radius && touch.X <= x2 + radius
            && MathF.Abs(touch.Y - y) <= radius;
    }

    static bool IsNearVerticalEdge(PointF touch, float y1, float y2, float x, float radius)
    {
        return touch.Y >= y1 - radius && touch.Y <= y2 + radius
            && MathF.Abs(touch.X - x) <= radius;
    }

    #endregion

    #region Text Placement

    Entry? activeTextEntry;
    PointF activeTextPosition;

    void HandleTextPlacement(PointF point)
    {
        var imageRect = drawable.GetImageRect();
        if (imageRect is not { Width: > 0, Height: > 0 })
            return;

        if (!imageRect.Contains(point))
            return;

        CommitActiveTextEntry();

        activeTextPosition = point;

        var screen = drawable.WorldToScreen(point);

        activeTextEntry = new Entry
        {
            FontSize = TextFontSize * zoomScale,
            FontFamily = TextFontFamily,
            TextColor = DrawStrokeColor,
            BackgroundColor = Colors.Transparent,
            Placeholder = "Type here...",
            PlaceholderColor = DrawStrokeColor.WithAlpha(0.5f),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = 200,
            Margin = new Thickness(screen.X, screen.Y, 0, 0)
        };

        AutomationIds.Set(activeTextEntry, AutomationIds.For(this, TextEntryAutomationIdSuffix));
        activeTextEntry.Completed += OnTextEntryCompleted;
        activeTextEntry.Unfocused += OnTextEntryUnfocused;

        Grid.SetRow(activeTextEntry, Grid.GetRow(graphicsView));
        rootGrid.Children.Add(activeTextEntry);

        activeTextEntry.Focus();
    }

    /// <summary>Keeps the in-flight text entry glued to the image while the view is zoomed or panned.</summary>
    void RepositionActiveTextEntry()
    {
        if (activeTextEntry == null)
            return;

        var screen = drawable.WorldToScreen(activeTextPosition);
        activeTextEntry.Margin = new Thickness(screen.X, screen.Y, 0, 0);
        activeTextEntry.FontSize = TextFontSize * zoomScale;
    }

    void OnTextEntryCompleted(object? sender, EventArgs e) => CommitActiveTextEntry();
    void OnTextEntryUnfocused(object? sender, FocusEventArgs e) => CommitActiveTextEntry();

    void CommitActiveTextEntry()
    {
        if (activeTextEntry == null)
            return;

        var text = activeTextEntry.Text;
        var entry = activeTextEntry;
        activeTextEntry = null;

        entry.Completed -= OnTextEntryCompleted;
        entry.Unfocused -= OnTextEntryUnfocused;
        rootGrid.Children.Remove(entry);

        if (string.IsNullOrWhiteSpace(text))
            return;

        var imageRect = drawable.GetImageRect();
        if (imageRect is not { Width: > 0, Height: > 0 })
            return;

        var normalized = new PointF(
            (activeTextPosition.X - imageRect.X) / imageRect.Width,
            (activeTextPosition.Y - imageRect.Y) / imageRect.Height
        );

        state.Push(new EditActions.TextAnnotationAction
        {
            Text = text,
            Position = normalized,
            FontSize = (float)TextFontSize,
            TextColor = DrawStrokeColor,
            FontFamily = TextFontFamily,
            ReferenceWidth = imageRect.Width
        });
    }

    #endregion

    #region Stroke Commit

    void CommitCurrentStroke()
    {
        if (drawable.ActiveStrokePoints is not { Count: >= 2 })
        {
            drawable.ActiveStrokePoints = null;
            return;
        }

        var imageRect = drawable.GetImageRect();
        if (imageRect is not { Width: > 0, Height: > 0 })
        {
            drawable.ActiveStrokePoints = null;
            return;
        }

        var normalized = drawable.ActiveStrokePoints
            .Select(p => new PointF(
                (p.X - imageRect.X) / imageRect.Width,
                (p.Y - imageRect.Y) / imageRect.Height))
            .ToArray();

        state.Push(new EditActions.DrawStrokeAction
        {
            Points = normalized,
            StrokeColor = DrawStrokeColor,
            StrokeWidth = (float)DrawStrokeWidth,
            ReferenceWidth = imageRect.Width
        });

        drawable.ActiveStrokePoints = null;
        Invalidate();
    }

    #endregion

    enum CropHandle
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
        Move
    }
}
