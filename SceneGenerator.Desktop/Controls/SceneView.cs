using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;
using SceneGenerator.Desktop.Controls.Rendering;

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

    public static readonly StyledProperty<bool> Show3DProperty =
        AvaloniaProperty.Register<SceneView, bool>(nameof(Show3D), true);
    public static readonly StyledProperty<int> HeightScaleIndexProperty =
        AvaloniaProperty.Register<SceneView, int>(nameof(HeightScaleIndex), 0, validate: value => value >= 0 && value <= 2);
    public static readonly DirectProperty<SceneView, bool> Has3DSceneProperty =
        AvaloniaProperty.RegisterDirect<SceneView, bool>(nameof(Has3DScene), view => view.Has3DScene);
    public static readonly DirectProperty<SceneView, bool> Is3DActiveProperty =
        AvaloniaProperty.RegisterDirect<SceneView, bool>(nameof(Is3DActive), view => view.Is3DActive);
    public static readonly DirectProperty<SceneView, string> ViewTitleProperty =
        AvaloniaProperty.RegisterDirect<SceneView, string>(nameof(ViewTitle), view => view.ViewTitle);
    public static readonly DirectProperty<SceneView, string> NavigationHintProperty =
        AvaloniaProperty.RegisterDirect<SceneView, string>(nameof(NavigationHint), view => view.NavigationHint);

    public bool Show3D { get => GetValue(Show3DProperty); set => SetValue(Show3DProperty, value); }
    public int HeightScaleIndex { get => GetValue(HeightScaleIndexProperty); set => SetValue(HeightScaleIndexProperty, value); }
    public bool Has3DScene => _has3DScene;
    public bool Is3DActive => _is3DActive;
    public string ViewTitle => _viewTitle;
    public string NavigationHint => _navigationHint;
    public double CameraAzimuth => _azimuth;
    public double CameraElevation => _elevation;
    public double HeightMultiplier => HeightScaleIndex switch { 0 => 1, 1 => 10, _ => 25 };

    public Scene? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public IBrush KiiBrush { get => GetValue(KiiBrushProperty); set => SetValue(KiiBrushProperty, value); }
    public IBrush ObstacleBrush { get => GetValue(ObstacleBrushProperty); set => SetValue(ObstacleBrushProperty, value); }
    public IBrush GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush InfoBackground { get => GetValue(InfoBackgroundProperty); set => SetValue(InfoBackgroundProperty, value); }
    public string ZoomText => _zoomText;

    private readonly Scene3DRenderer _renderer3D = new();
    private bool _has3DScene, _is3DActive;
    private string _viewTitle = "Вид сверху";
    private string _navigationHint = "Колёсико — масштаб · Перетаскивание — перемещение. Мелкие объекты отмечены маркерами.";
    private double _azimuth = -55, _elevation = 35;
    private bool _isRotating;
    private Point3D? _rotationAnchor;
    private IPointer? _dragPointer;
    private MouseButton _dragButton;
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
            GridBrushProperty, TextBrushProperty, InfoBackgroundProperty, BoundsProperty, Show3DProperty, HeightScaleIndexProperty);
    }

    public void ZoomIn() => ZoomAt(PlotCenter(), 1.4);
    public void ZoomOut() => ZoomAt(PlotCenter(), 1 / 1.4);

    public void FitScene()
    {
        _zoom = 1;
        _pan = default;
        _azimuth = -55;
        _elevation = 35;
        _isPanning = _isRotating = false;
        _rotationAnchor = null;
        _dragPointer?.Capture(null);
        _dragPointer = null;
        Cursor = null;
        SetAndRaise(ZoomTextProperty, ref _zoomText, "100%");
        ClearHover();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty || change.Property == Show3DProperty)
        {
            RefreshViewInfo();
            if (change.Property == SceneProperty || !Is3DActive) _renderer3D.Dispose();
            FitScene();
        }
        else if (change.Property == HeightScaleIndexProperty)
        {
            ClampPan();
            ClearHover();
        }
        else if (change.Property == BoundsProperty)
        {
            ClampPan();
            ClearHover();
        }
    }

    private void RefreshViewInfo()
    {
        SetAndRaise(Has3DSceneProperty, ref _has3DScene, Scene?.Mode == SceneMode.ThreeD);
        SetAndRaise(Is3DActiveProperty, ref _is3DActive, _has3DScene && Show3D);
        SetAndRaise(ViewTitleProperty, ref _viewTitle, Is3DActive ? "Объёмный вид" : "Вид сверху");
        SetAndRaise(NavigationHintProperty, ref _navigationHint, Is3DActive
            ? "Левая кнопка — вращение · Правая или Shift + левая — перемещение · Колёсико — масштаб."
            : "Колёсико — масштаб · Перетаскивание — перемещение. Мелкие объекты отмечены маркерами.");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _renderer3D.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    public void RotateView(double azimuthDelta, double elevationDelta)
    {
        if (!Is3DActive || !double.IsFinite(azimuthDelta) || !double.IsFinite(elevationDelta) ||
            !TryGetProjection3D(out SceneProjection3D before)) return;

        Point3D anchor = _isRotating && _rotationAnchor is { } dragAnchor
            ? dragAnchor : Anchor3D(before, before.Plot.Center);
        _azimuth = ((_azimuth + azimuthDelta) % 360 + 540) % 360 - 180;
        _elevation = Math.Clamp(_elevation + elevationDelta, 10, 80);
        if (TryGetProjection3D(out SceneProjection3D after)) _pan += before.Plot.Center - after.ToScreen(anchor);
        ClampPan();
        ClearHover();
    }

    public void FocusKii()
    {
        if (Scene is not { } scene) return;
        var kii = scene.KiiObjects.Select(o => o.Geometry).ToArray();
        _zoom = 1;
        _pan = default;
        Rect plot;
        if (Is3DActive && TryGetProjection3D(out SceneProjection3D projection)) plot = projection.Plot;
        else if (TryGetViewport(out Viewport view)) plot = view.Plot;
        else return;

        Rect initial = ProjectedKiiBounds(kii);
        double preferred = Math.Min(scene.Bounds.Size.Length, scene.Bounds.Size.Width) /
            Math.Max(800, Math.Max(kii.Max(g => g.Footprint.MaxX) - kii.Min(g => g.Footprint.MinX), kii.Max(g => g.Footprint.MaxY) - kii.Min(g => g.Footprint.MinY)) * 3);
        double fit = .7 * Math.Min(plot.Width / Math.Max(1, initial.Width), plot.Height / Math.Max(1, initial.Height));
        _zoom = Math.Clamp(Math.Min(preferred, fit), 1, 64);
        _pan = plot.Center - ProjectedKiiBounds(kii).Center;
        ClampPan();
        SetAndRaise(ZoomTextProperty, ref _zoomText, $"{_zoom * 100:0}%");
        ClearHover();
    }

    private Rect ProjectedKiiBounds(PolygonPrism3D[] kii)
    {
        Point[] points = kii.SelectMany(g => g.GetBasePoints().Concat(g.GetRoofPoints())).Select(point =>
        {
            TryProjectPoint(point, out Point screen);
            return screen;
        }).ToArray();
        double minX = points.Min(p => p.X), minY = points.Min(p => p.Y);
        return new Rect(minX, minY, points.Max(p => p.X) - minX, points.Max(p => p.Y) - minY);
    }

    public bool TryProjectPoint(Point3D point, out Point screen)
    {
        ArgumentNullException.ThrowIfNull(point);
        screen = default;
        if (Is3DActive && TryGetProjection3D(out SceneProjection3D projection))
        {
            screen = projection.ToScreen(point);
            return true;
        }
        if (!TryGetViewport(out Viewport view)) return false;
        screen = view.ToScreen(new Point2D(point.X, point.Y));
        return true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.DrawRectangle(InfoBackground, null, new Rect(Bounds.Size));
        if (Is3DActive)
        {
            Render3D(context);
            DrawHoverInfo(context);
            return;
        }
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

    private void Render3D(DrawingContext context)
    {
        if (Scene is not { } scene || !TryGetProjection3D(out SceneProjection3D projection)) return;
        using (context.PushClip(projection.Plot))
        {
            var floor = new StreamGeometry();
            using (StreamGeometryContext drawing = floor.Open())
            {
                IReadOnlyList<Point2D> corners = scene.Bounds.Footprint.Outer;
                drawing.BeginFigure(projection.ToScreen(corners[0].X, corners[0].Y, projection.GroundZ), true);
                foreach (Point2D point in corners.Skip(1)) drawing.LineTo(projection.ToScreen(point.X, point.Y, projection.GroundZ));
                drawing.EndFigure(true);
            }
            using (context.PushOpacity(.12)) context.DrawGeometry(GridBrush, null, floor);
            context.DrawGeometry(null, new Pen(TextBrush, 1), floor);
            DrawGroundGrid(context, scene, projection);
            UpdateRenderer3D(projection);
            context.DrawImage(_renderer3D.GetImage(_hoveredObject), projection.Plot);
        }
        DrawAxes3D(context, projection);
    }

    private void DrawGroundGrid(DrawingContext context, Scene scene, SceneProjection3D projection)
    {
        Point2D[] visible = [projection.ToGround(projection.Plot.TopLeft), projection.ToGround(projection.Plot.TopRight),
            projection.ToGround(projection.Plot.BottomLeft), projection.ToGround(projection.Plot.BottomRight)];
        double minX = Math.Max(scene.Bounds.Footprint.MinX, visible.Min(p => p.X));
        double maxX = Math.Min(scene.Bounds.Footprint.MaxX, visible.Max(p => p.X));
        double minY = Math.Max(scene.Bounds.Footprint.MinY, visible.Min(p => p.Y));
        double maxY = Math.Min(scene.Bounds.Footprint.MaxY, visible.Max(p => p.Y));
        double step = GridStep(75 / projection.Scale);
        var pen = new Pen(GridBrush, 1);
        for (double x = Math.Ceiling(minX / step) * step; x <= maxX; x += step)
            context.DrawLine(pen, projection.ToScreen(x, scene.Bounds.Footprint.MinY, projection.GroundZ),
                projection.ToScreen(x, scene.Bounds.Footprint.MaxY, projection.GroundZ));
        for (double y = Math.Ceiling(minY / step) * step; y <= maxY; y += step)
            context.DrawLine(pen, projection.ToScreen(scene.Bounds.Footprint.MinX, y, projection.GroundZ),
                projection.ToScreen(scene.Bounds.Footprint.MaxX, y, projection.GroundZ));
    }

    private void DrawAxes3D(DrawingContext context, SceneProjection3D projection)
    {
        Point origin = new(52, Bounds.Height - 52);
        using (context.PushOpacity(.94))
            context.DrawRectangle(InfoBackground, null, new Rect(4, Bounds.Height - 108, 106, 104), 6, 6);
        DrawAxis(projection.RightX, -projection.UpX, "X");
        DrawAxis(projection.RightY, -projection.UpY, "Y");
        DrawAxis(0, -projection.UpZ, "Z");
        void DrawAxis(double x, double y, string label)
        {
            Point end = origin + new Vector(x * 32, y * 32);
            context.DrawLine(new Pen(TextBrush, 1.5), origin, end);
            DrawText(context, label, end + new Vector(3, -5));
        }
    }

    private bool TryGetProjection3D(out SceneProjection3D projection)
    {
        projection = default;
        if (Scene is not { } scene || Bounds.Width <= 100 || Bounds.Height <= 90) return false;
        projection = new SceneProjection3D(scene, new Rect(18, 18, Bounds.Width - 36, Bounds.Height - 36),
            _zoom, _pan, _azimuth, _elevation, HeightMultiplier);
        return true;
    }

    private void UpdateRenderer3D(SceneProjection3D projection) =>
        _renderer3D.Update(Scene!, projection, BrushColor(KiiBrush, Colors.DodgerBlue), BrushColor(ObstacleBrush, Colors.Gray));

    private static Color BrushColor(IBrush brush, Color fallback) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Clamp(Math.Round(solid.Color.A * solid.Opacity), 0, 255), solid.Color.R, solid.Color.G, solid.Color.B)
        : fallback;

    private Point3D Anchor3D(SceneProjection3D projection, Point screen)
    {
        UpdateRenderer3D(projection);
        if (_renderer3D.TryGetHitPoint(screen, out Point3D point)) return point;
        Point2D ground = projection.ToGround(screen);
        return new Point3D(ground.X, ground.Y, projection.GroundZ);
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
        if (Is3DActive && TryGetProjection3D(out SceneProjection3D projection))
        {
            UpdateRenderer3D(projection);
            return _renderer3D.FindObjectAt(position);
        }
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
        if (_isRotating)
        {
            Vector delta = position - _dragPosition;
            _dragPosition = position;
            RotateView(delta.X * .4, -delta.Y * .3);
            e.Handled = true;
        }
        else if (_isPanning)
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
        if (Scene is null) return;
        PointerPointProperties buttons = e.GetCurrentPoint(this).Properties;
        if (!buttons.IsLeftButtonPressed && !buttons.IsRightButtonPressed && !buttons.IsMiddleButtonPressed) return;
        _dragButton = buttons.IsLeftButtonPressed ? MouseButton.Left : buttons.IsRightButtonPressed ? MouseButton.Right : MouseButton.Middle;
        _isRotating = Is3DActive && _dragButton == MouseButton.Left && !e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _isPanning = !_isRotating;
        _rotationAnchor = _isRotating && TryGetProjection3D(out var rotationProjection)
            ? Anchor3D(rotationProjection, rotationProjection.Plot.Center) : null;
        _dragPosition = e.GetPosition(this);
        _dragPointer = e.Pointer;
        e.Pointer.Capture(this);
        Cursor = new Cursor(_isRotating ? StandardCursorType.Hand : StandardCursorType.SizeAll);
        ClearHover();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if ((!_isPanning && !_isRotating) || e.InitialPressMouseButton != _dragButton) return;
        _isPanning = _isRotating = false;
        _rotationAnchor = null;
        _dragPointer = null;
        e.Pointer.Capture(null);
        Cursor = null;
        UpdateHover(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isPanning = _isRotating = false;
        _rotationAnchor = null;
        _dragPointer = null;
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

    private Point PlotCenter() => Is3DActive && TryGetProjection3D(out SceneProjection3D projection)
        ? projection.Plot.Center : TryGetViewport(out Viewport view) ? view.Plot.Center : Bounds.Center;

    private void ZoomAt(Point anchor, double factor)
    {
        if (Is3DActive && TryGetProjection3D(out SceneProjection3D before3D))
        {
            Point3D world3D = Anchor3D(before3D, anchor);
            _zoom = Math.Clamp(_zoom * factor, 1, 64);
            if (TryGetProjection3D(out SceneProjection3D after3D)) _pan += anchor - after3D.ToScreen(world3D);
            ClampPan();
            SetAndRaise(ZoomTextProperty, ref _zoomText, $"{_zoom * 100:0}%");
            ClearHover();
            return;
        }
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
        Rect plot, frame;
        if (Is3DActive && TryGetProjection3D(out SceneProjection3D projection))
        {
            plot = projection.Plot; frame = projection.Frame;
        }
        else if (TryGetViewport(out Viewport view))
        {
            plot = view.Plot; frame = view.Frame;
        }
        else return;
        double dx = frame.Right < plot.Left + 24 ? plot.Left + 24 - frame.Right
            : frame.Left > plot.Right - 24 ? plot.Right - 24 - frame.Left : 0;
        double dy = frame.Bottom < plot.Top + 24 ? plot.Top + 24 - frame.Bottom
            : frame.Top > plot.Bottom - 24 ? plot.Bottom - 24 - frame.Top : 0;
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
