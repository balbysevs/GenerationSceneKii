using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Desktop.Controls;

public class SceneView : Control
{
    public static readonly StyledProperty<Scene?> SceneProperty =
        AvaloniaProperty.Register<SceneView, Scene?>(nameof(Scene));
    public static readonly StyledProperty<IBrush> KiiBrushProperty =
        AvaloniaProperty.Register<SceneView, IBrush>(nameof(KiiBrush), Brushes.DodgerBlue);
    public static readonly StyledProperty<IBrush> ObstacleBrushProperty =
        AvaloniaProperty.Register<SceneView, IBrush>(nameof(ObstacleBrush), Brushes.Gray);
    public static readonly StyledProperty<IBrush> GridBrushProperty =
        AvaloniaProperty.Register<SceneView, IBrush>(nameof(GridBrush), Brushes.LightGray);
    public static readonly StyledProperty<IBrush> TextBrushProperty =
        AvaloniaProperty.Register<SceneView, IBrush>(nameof(TextBrush), Brushes.Gray);
    public static readonly StyledProperty<IBrush> InfoBackgroundProperty =
        AvaloniaProperty.Register<SceneView, IBrush>(nameof(InfoBackground), Brushes.White);
    public static readonly DirectProperty<SceneView, string> ZoomTextProperty =
        AvaloniaProperty.RegisterDirect<SceneView, string>(nameof(ZoomText), view => view.ZoomText);

    public Scene? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public IBrush KiiBrush { get => GetValue(KiiBrushProperty); set => SetValue(KiiBrushProperty, value); }
    public IBrush ObstacleBrush { get => GetValue(ObstacleBrushProperty); set => SetValue(ObstacleBrushProperty, value); }
    public IBrush GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush InfoBackground { get => GetValue(InfoBackgroundProperty); set => SetValue(InfoBackgroundProperty, value); }
    public string ZoomText => _zoomText;

    private double _zoom = 1;
    private Vector _pan;
    private string _zoomText = "100%";
    private bool _isPanning;
    private Point _dragPosition;
    private Point? _hoverPosition;
    private SceneObject? _hoveredObject;

    static SceneView()
    {
        AffectsRender<SceneView>(SceneProperty, KiiBrushProperty, ObstacleBrushProperty,
            GridBrushProperty, TextBrushProperty, InfoBackgroundProperty, BoundsProperty);
    }

    public void ZoomIn() => ZoomAt(PlotCenter(), 1.4);
    public void ZoomOut() => ZoomAt(PlotCenter(), 1 / 1.4);

    public void FitScene()
    {
        _zoom = 1;
        _pan = default;
        SetAndRaise(ZoomTextProperty, ref _zoomText, "100%");
        ClearHover();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            FitScene();
        }
        else if (change.Property == BoundsProperty)
        {
            ClampPan();
            ClearHover();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // A transparent drawing makes empty parts of the viewer receive pointer input.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (Scene is not { } scene || !TryGetViewport(out Viewport view)) return;

        using (context.PushClip(view.Plot))
        {
            DrawGrid(context, view, labels: false);
            context.DrawRectangle(null, new Pen(TextBrush, 1), view.Frame);
            foreach (SceneObject item in scene.Objects)
            {
                StreamGeometry geometry = CreateGeometry(item.Geometry.Footprint, view);
                IBrush fill = item.Type == SceneObjectType.Kii ? KiiBrush : ObstacleBrush;
                bool hovered = ReferenceEquals(item, _hoveredObject);
                context.DrawGeometry(fill, hovered ? new Pen(KiiBrush, 2) : null, geometry);

                if (IsTiny(item, view))
                {
                    Point marker = view.ToScreen(item.Geometry.Footprint.GetCenterPoint());
                    context.DrawEllipse(fill, hovered ? new Pen(KiiBrush, 2) : null, marker, 2.5, 2.5);
                }
            }
        }

        DrawGrid(context, view, labels: true);
        DrawText(context, "Y, м", new Point(4, 2));
        FormattedText xTitle = Text("X, м");
        context.DrawText(xTitle, new Point(view.Plot.Right - xTitle.Width, Bounds.Height - xTitle.Height - 2));
        DrawHoverInfo(context);
    }

    private void DrawGrid(DrawingContext context, Viewport view, bool labels)
    {
        Rect visible = view.Plot.Intersect(view.Frame);
        if (visible.Width <= 0 || visible.Height <= 0) return;
        Point2D min = view.ToWorld(new Point(visible.Left, visible.Bottom));
        Point2D max = view.ToWorld(new Point(visible.Right, visible.Top));
        double step = GridStep(70 / view.Scale);
        var pen = new Pen(GridBrush, 1);

        for (double x = Math.Ceiling(min.X / step) * step; x <= max.X + step * 1e-8; x += step)
        {
            double px = view.ToScreen(new Point2D(x, min.Y)).X;
            if (labels)
            {
                FormattedText text = Text(x.ToString("0.##", CultureInfo.CurrentCulture));
                context.DrawText(text, new Point(px - text.Width / 2, view.Plot.Bottom + 7));
            }
            else context.DrawLine(pen, new Point(px, visible.Top), new Point(px, visible.Bottom));
        }
        for (double y = Math.Ceiling(min.Y / step) * step; y <= max.Y + step * 1e-8; y += step)
        {
            double py = view.ToScreen(new Point2D(min.X, y)).Y;
            if (labels)
            {
                FormattedText text = Text(y.ToString("0.##", CultureInfo.CurrentCulture));
                context.DrawText(text, new Point(view.Plot.Left - text.Width - 8, py - text.Height / 2));
            }
            else context.DrawLine(pen, new Point(visible.Left, py), new Point(visible.Right, py));
        }
    }

    private static double GridStep(double target)
    {
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(target)));
        double fraction = target / magnitude;
        return magnitude * (fraction < 1.5 ? 1 : fraction < 3.5 ? 2 : fraction < 7.5 ? 5 : 10);
    }

    public SceneObject? FindObjectAt(Point position)
    {
        if (Scene is not { } scene || !TryGetViewport(out Viewport view) || !view.Plot.Contains(position)) return null;
        Point2D world = view.ToWorld(position);
        foreach (SceneObject item in scene.Objects)
            if (item.Geometry.Footprint.Contains(world)) return item;

        SceneObject? nearest = null;
        double bestDistance = 7;
        foreach (SceneObject item in scene.Objects)
        {
            Point center = view.ToScreen(item.Geometry.Footprint.GetCenterPoint());
            double distance = double.Hypot(center.X - position.X, center.Y - position.Y);
            if (IsTiny(item, view) && distance < bestDistance)
            {
                nearest = item;
                bestDistance = distance;
            }
        }
        return nearest;
    }

    private static bool IsTiny(SceneObject item, Viewport view) =>
        Math.Max(item.Geometry.Size.Length, item.Geometry.Size.Width) * view.Scale < 5;

    private void UpdateHover(Point position)
    {
        _hoveredObject = FindObjectAt(position);
        _hoverPosition = _hoveredObject is null ? null : position;
        InvalidateVisual();
    }

    private void ClearHover()
    {
        _hoveredObject = null;
        _hoverPosition = null;
        InvalidateVisual();
    }

    private void DrawHoverInfo(DrawingContext context)
    {
        if (_hoveredObject is not { } item || _hoverPosition is not { } pointer) return;
        string kind = item.Type == SceneObjectType.Kii ? "КИИ" : "Помеха";
        Point3D origin = item.Geometry.Origin;
        Size3D size = item.Geometry.Size;
        string description = $"{kind} · {item.Id}\n" +
            $"Xmin: {origin.X:0.##} м · Ymin: {origin.Y:0.##} м\n" +
            $"Основание Z: {origin.Z:0.##} м\n" +
            $"Габариты: {size.Length:0.##} × {size.Width:0.##} × {size.Height:0.##} м";
        FormattedText text = Text(description);
        text.MaxTextWidth = Math.Max(40, Bounds.Width - 36);
        double width = text.Width + 20;
        double height = text.Height + 20;
        double left = Math.Clamp(pointer.X + 14, 8, Math.Max(8, Bounds.Width - width - 8));
        double top = Math.Clamp(pointer.Y + 14, 8, Math.Max(8, Bounds.Height - height - 8));
        var box = new Rect(left, top, width, height);
        context.DrawRectangle(InfoBackground, new Pen(KiiBrush, 1), box, 6, 6);
        context.DrawText(text, new Point(left + 10, top + 10));
    }

    private FormattedText Text(string value) => new(value, CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight, Typeface.Default, 11, TextBrush);
    private void DrawText(DrawingContext context, string value, Point position) => context.DrawText(Text(value), position);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        if (_isPanning)
        {
            _pan += position - _dragPosition;
            _dragPosition = position;
            ClampPan();
            ClearHover();
            e.Handled = true;
        }
        else UpdateHover(position);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Scene is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _isPanning = true;
        _dragPosition = e.GetPosition(this);
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);
        ClearHover();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isPanning || e.InitialPressMouseButton != MouseButton.Left) return;
        _isPanning = false;
        e.Pointer.Capture(null);
        Cursor = null;
        UpdateHover(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isPanning = false;
        Cursor = null;
        ClearHover();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Scene is null || e.Delta.Y == 0) return;
        Point position = e.GetPosition(this);
        ZoomAt(position, Math.Pow(1.15, Math.Clamp(e.Delta.Y, -4, 4)));
        UpdateHover(position);
        e.Handled = true;
    }

    private Point PlotCenter() => TryGetViewport(out Viewport view) ? view.Plot.Center : Bounds.Center;

    private void ZoomAt(Point anchor, double factor)
    {
        if (!TryGetViewport(out Viewport before)) return;
        Point2D world = before.ToWorld(anchor);
        _zoom = Math.Clamp(_zoom * factor, 1, 64);
        if (TryGetViewport(out Viewport after)) _pan += anchor - after.ToScreen(world);
        ClampPan();
        SetAndRaise(ZoomTextProperty, ref _zoomText, $"{_zoom * 100:0}%");
        ClearHover();
    }

    private void ClampPan()
    {
        if (!TryGetViewport(out Viewport view)) return;
        double dx = view.Frame.Right < view.Plot.Left + 24 ? view.Plot.Left + 24 - view.Frame.Right
            : view.Frame.Left > view.Plot.Right - 24 ? view.Plot.Right - 24 - view.Frame.Left : 0;
        double dy = view.Frame.Bottom < view.Plot.Top + 24 ? view.Plot.Top + 24 - view.Frame.Bottom
            : view.Frame.Top > view.Plot.Bottom - 24 ? view.Plot.Bottom - 24 - view.Frame.Top : 0;
        _pan += new Vector(dx, dy);
    }

    private bool TryGetViewport(out Viewport view)
    {
        view = default;
        if (Scene is not { } scene || Bounds.Width <= 100 || Bounds.Height <= 90) return false;
        var plot = new Rect(60, 24, Bounds.Width - 80, Bounds.Height - 72);
        double scale = Math.Min(plot.Width / scene.Bounds.Size.Length, plot.Height / scene.Bounds.Size.Width) * _zoom;
        var frame = new Rect(plot.Center.X - scene.Bounds.Size.Length * scale / 2 + _pan.X,
            plot.Center.Y - scene.Bounds.Size.Width * scale / 2 + _pan.Y,
            scene.Bounds.Size.Length * scale, scene.Bounds.Size.Width * scale);
        view = new Viewport(plot, frame, scale, scene.Bounds.Footprint.MinX, scene.Bounds.Footprint.MaxY);
        return true;
    }

    private readonly record struct Viewport(Rect Plot, Rect Frame, double Scale, double MinX, double MaxY)
    {
        public Point ToScreen(Point2D point) => new(Frame.Left + (point.X - MinX) * Scale,
            Frame.Top + (MaxY - point.Y) * Scale);
        public Point2D ToWorld(Point point) => new(MinX + (point.X - Frame.Left) / Scale,
            MaxY - (point.Y - Frame.Top) / Scale);
    }

    private static StreamGeometry CreateGeometry(Polygon2D footprint, Viewport view)
    {
        var geometry = new StreamGeometry();
        using StreamGeometryContext drawing = geometry.Open();
        drawing.SetFillRule(FillRule.EvenOdd);
        DrawRing(drawing, footprint.Outer, view);
        foreach (IReadOnlyList<Point2D> hole in footprint.Holes) DrawRing(drawing, hole, view);
        return geometry;
    }

    private static void DrawRing(StreamGeometryContext drawing, IReadOnlyList<Point2D> points, Viewport view)
    {
        drawing.BeginFigure(view.ToScreen(points[0]), isFilled: true);
        for (int index = 1; index < points.Count; index++) drawing.LineTo(view.ToScreen(points[index]));
        drawing.EndFigure(isClosed: true);
    }
}
