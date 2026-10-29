using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Desktop.ViewModels;

namespace SceneGenerator.Desktop.Controls;

public sealed class OsmBuildingsPreview : Control
{
    public static readonly StyledProperty<IReadOnlyList<OsmBuildingItemViewModel>?> BuildingsProperty =
        AvaloniaProperty.Register<OsmBuildingsPreview, IReadOnlyList<OsmBuildingItemViewModel>?>(nameof(Buildings));
    public static readonly StyledProperty<OsmBuildingItemViewModel?> SelectedBuildingProperty =
        AvaloniaProperty.Register<OsmBuildingsPreview, OsmBuildingItemViewModel?>(nameof(SelectedBuilding));
    public static readonly StyledProperty<double> AreaLengthProperty = AvaloniaProperty.Register<OsmBuildingsPreview, double>(nameof(AreaLength), 1000);
    public static readonly StyledProperty<double> AreaWidthProperty = AvaloniaProperty.Register<OsmBuildingsPreview, double>(nameof(AreaWidth), 1000);
    public static readonly StyledProperty<IBrush> AccentBrushProperty = AvaloniaProperty.Register<OsmBuildingsPreview, IBrush>(nameof(AccentBrush), Brushes.DodgerBlue);
    public static readonly StyledProperty<IBrush> GridBrushProperty = AvaloniaProperty.Register<OsmBuildingsPreview, IBrush>(nameof(GridBrush), Brushes.Gray);
    public static readonly StyledProperty<IBrush> TextBrushProperty = AvaloniaProperty.Register<OsmBuildingsPreview, IBrush>(nameof(TextBrush), Brushes.Gray);
    public IReadOnlyList<OsmBuildingItemViewModel>? Buildings { get => GetValue(BuildingsProperty); set => SetValue(BuildingsProperty, value); }
    public OsmBuildingItemViewModel? SelectedBuilding { get => GetValue(SelectedBuildingProperty); set => SetValue(SelectedBuildingProperty, value); }
    public double AreaLength { get => GetValue(AreaLengthProperty); set => SetValue(AreaLengthProperty, value); }
    public double AreaWidth { get => GetValue(AreaWidthProperty); set => SetValue(AreaWidthProperty, value); }
    public IBrush AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public IBrush GridBrush { get => GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    static OsmBuildingsPreview() => AffectsRender<OsmBuildingsPreview>(BuildingsProperty, SelectedBuildingProperty,
        AreaLengthProperty, AreaWidthProperty, AccentBrushProperty, GridBrushProperty, TextBrushProperty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BuildingsProperty)
        {
            if (change.OldValue is IReadOnlyList<OsmBuildingItemViewModel> oldItems)
                foreach (var item in oldItems) item.PropertyChanged -= ItemChanged;
            if (change.NewValue is IReadOnlyList<OsmBuildingItemViewModel> newItems)
                foreach (var item in newItems) item.PropertyChanged += ItemChanged;
        }
    }

    private void ItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvalidateVisual();

    private (Rect Plot, double Scale) Layout()
    {
        double scale = Math.Max(0, Math.Min((Bounds.Width - 40) / AreaLength, (Bounds.Height - 55) / AreaWidth));
        return (new Rect((Bounds.Width - AreaLength * scale) / 2, 30, AreaLength * scale, AreaWidth * scale), scale);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 50 || Bounds.Height < 60) return;
        var (plot, scale) = Layout();
        var gridPen = new Pen(GridBrush, 0.5);
        for (int i = 0; i <= 10; i++)
        {
            double x = plot.X + plot.Width * i / 10, y = plot.Y + plot.Height * i / 10;
            context.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
        }
        foreach (var item in Buildings ?? [])
        {
            var polygon = item.Building.Footprint;
            var geometry = new StreamGeometry();
            using (var stream = geometry.Open())
            {
                stream.SetFillRule(FillRule.EvenOdd);
                foreach (var ring in new[] { polygon.Outer }.Concat(polygon.Holes))
                {
                    Point Map(Point2D p) => new(plot.X + p.X * scale, plot.Bottom - p.Y * scale);
                    stream.BeginFigure(Map(ring[0]), true);
                    foreach (var point in ring.Skip(1)) stream.LineTo(Map(point));
                    stream.EndFigure(true);
                }
            }
            IBrush fill = !item.Included ? Brushes.DimGray : ReferenceEquals(item, SelectedBuilding) ? AccentBrush : item.IsEstimated ? Brushes.DarkGoldenrod : Brushes.SlateGray;
            context.DrawGeometry(fill, new Pen(TextBrush, ReferenceEquals(item, SelectedBuilding) ? 2 : 0.6), geometry);
        }
        var label = new FormattedText("Север ↑ · оранжевый — оценка высоты · щелчок — выбрать КИИ",
            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 11, TextBrush);
        context.DrawText(label, new Point(10, 8));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var (plot, scale) = Layout();
        if (scale <= 0) return;
        var position = e.GetPosition(this);
        var point = new Point2D((position.X - plot.X) / scale, (plot.Bottom - position.Y) / scale);
        SetCurrentValue(SelectedBuildingProperty, Buildings?.LastOrDefault(b => b.Building.Footprint.Contains(point)));
        e.Handled = true;
    }
}
