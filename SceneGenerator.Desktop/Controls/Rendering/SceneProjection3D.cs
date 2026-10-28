using System;
using Avalonia;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Desktop.Controls.Rendering;

/// <summary>Orthographic camera. Vertical exaggeration belongs to the view, not the scene.</summary>
public readonly record struct SceneProjection3D
{
    public Rect Plot { get; }
    public Rect Frame { get; }
    public double Scale { get; }
    public double VerticalScale { get; }
    public double GroundZ { get; }
    public double CenterX { get; }
    public double CenterY { get; }
    public double RightX { get; }
    public double RightY { get; }
    public double UpX { get; }
    public double UpY { get; }
    public double UpZ { get; }
    public double DepthX { get; }
    public double DepthY { get; }
    public double DepthZ { get; }
    private readonly double _midU;
    private readonly double _midV;
    private readonly Point _screenCenter;

    public SceneProjection3D(Scene scene, Rect plot, double zoom, Vector pan,
        double azimuth, double elevation, double verticalScale)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (plot.Width <= 0 || plot.Height <= 0 || !double.IsFinite(zoom) || zoom <= 0 ||
            !double.IsFinite(azimuth) || !double.IsFinite(elevation) || elevation < 10 || elevation > 80 ||
            !double.IsFinite(verticalScale) || verticalScale < 1 || verticalScale > 25)
            throw new ArgumentOutOfRangeException(nameof(zoom), "Недопустимые параметры камеры.");

        Plot = plot;
        VerticalScale = verticalScale;
        GroundZ = scene.Bounds.BaseElevation;
        CenterX = scene.Bounds.Footprint.MinX + scene.Bounds.Size.Length / 2;
        CenterY = scene.Bounds.Footprint.MinY + scene.Bounds.Size.Width / 2;
        double yaw = azimuth * Math.PI / 180;
        double pitch = elevation * Math.PI / 180;
        RightX = -Math.Sin(yaw); RightY = Math.Cos(yaw);
        UpX = -Math.Cos(yaw) * Math.Sin(pitch);
        UpY = -Math.Sin(yaw) * Math.Sin(pitch);
        UpZ = Math.Cos(pitch);
        DepthX = Math.Cos(yaw) * Math.Cos(pitch);
        DepthY = Math.Sin(yaw) * Math.Cos(pitch);
        DepthZ = Math.Sin(pitch);

        double minU = double.PositiveInfinity, minV = double.PositiveInfinity;
        double maxU = double.NegativeInfinity, maxV = double.NegativeInfinity;
        foreach (Point2D point in scene.Bounds.Footprint.Outer)
        foreach (double height in new[] { 0.0, scene.Bounds.Height * verticalScale })
        {
            double x = point.X - CenterX, y = point.Y - CenterY;
            double u = RightX * x + RightY * y;
            double v = UpX * x + UpY * y + UpZ * height;
            minU = Math.Min(minU, u); maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
        }
        _midU = (minU + maxU) / 2; _midV = (minV + maxV) / 2;
        Scale = .92 * Math.Min(plot.Width / (maxU - minU), plot.Height / (maxV - minV)) * zoom;
        _screenCenter = plot.Center + pan;
        Frame = new Rect(_screenCenter.X - (maxU - minU) * Scale / 2,
            _screenCenter.Y - (maxV - minV) * Scale / 2,
            (maxU - minU) * Scale, (maxV - minV) * Scale);
    }

    public Point ToScreen(Point3D point) => ToScreen(point.X, point.Y, point.Z);
    public Point ToScreen(double x, double y, double z)
    {
        x -= CenterX; y -= CenterY;
        double height = (z - GroundZ) * VerticalScale;
        return new Point(_screenCenter.X + (RightX * x + RightY * y - _midU) * Scale,
            _screenCenter.Y - (UpX * x + UpY * y + UpZ * height - _midV) * Scale);
    }

    public double Depth(double x, double y, double z) =>
        DepthX * (x - CenterX) + DepthY * (y - CenterY) + DepthZ * (z - GroundZ) * VerticalScale;

    public double DepthOnHorizontalPlane(double screenY, double z)
    {
        double v = _midV - (screenY - _screenCenter.Y) / Scale;
        return ((z - GroundZ) * VerticalScale - UpZ * v) / DepthZ;
    }

    public Point2D ToGround(Point screen)
    {
        double u = _midU + (screen.X - _screenCenter.X) / Scale;
        double v = _midV - (screen.Y - _screenCenter.Y) / Scale;
        double along = -v / DepthZ;
        double horizontalDepth = Math.Sqrt(DepthX * DepthX + DepthY * DepthY);
        return new Point2D(CenterX + RightX * u + DepthX / horizontalDepth * along,
            CenterY + RightY * u + DepthY / horizontalDepth * along);
    }

    public Point3D Unproject(Point screen, double depth)
    {
        double u = _midU + (screen.X - _screenCenter.X) / Scale;
        double v = _midV - (screen.Y - _screenCenter.Y) / Scale;
        return new Point3D(CenterX + RightX * u + UpX * v + DepthX * depth,
            CenterY + RightY * u + UpY * v + DepthY * depth,
            GroundZ + (UpZ * v + DepthZ * depth) / VerticalScale);
    }
}
