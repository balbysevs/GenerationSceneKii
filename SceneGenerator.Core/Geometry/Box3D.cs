namespace SceneGenerator.Core.Geometry;

/// <summary>Axis-aligned rectangular special case of a polygon prism.</summary>
public sealed class Box3D : PolygonPrism3D
{
    public Box3D(Point3D origin, Size3D size)
        : base(CreateFootprint(origin, size), origin.Z, size.Height) { }

    public bool Contains(PolygonPrism3D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Footprint.MinX >= Footprint.MinX && other.Footprint.MaxX <= Footprint.MaxX &&
               other.Footprint.MinY >= Footprint.MinY && other.Footprint.MaxY <= Footprint.MaxY &&
               other.BaseElevation >= BaseElevation && other.BaseElevation + other.Height <= BaseElevation + Height;
    }

    public override double GetHorizontalDistanceTo(PolygonPrism3D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is not Box3D)
        {
            return base.GetHorizontalDistanceTo(other);
        }

        // Axis-aligned rectangles need interval gaps, not enumeration of every pair of edges.
        double dx = Math.Max(0, Math.Max(Footprint.MinX - other.Footprint.MaxX,
            other.Footprint.MinX - Footprint.MaxX));
        double dy = Math.Max(0, Math.Max(Footprint.MinY - other.Footprint.MaxY,
            other.Footprint.MinY - Footprint.MaxY));
        double distance = double.Hypot(dx, dy);
        return distance <= Polygon2D.Tolerance ? 0 : distance;
    }

    private static Polygon2D CreateFootprint(Point3D origin, Size3D size)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(size);
        return new Polygon2D([
            new Point2D(origin.X, origin.Y),
            new Point2D(origin.X + size.Length, origin.Y),
            new Point2D(origin.X + size.Length, origin.Y + size.Width),
            new Point2D(origin.X, origin.Y + size.Width)]);
    }
}
