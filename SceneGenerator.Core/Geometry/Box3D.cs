namespace SceneGenerator.Core.Geometry;

public class Box3D
{
    public Point3D Origin { get; }
    public Size3D Size { get; }

    public Box3D(Point3D origin, Size3D size)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(size);
        
        Origin = origin;
        Size = size;
    }

    public Point3D[] GetBasePoints()
    {
        double x = Origin.X;
        double y = Origin.Y;
        double z = Origin.Z;
        return new Point3D[]
        {
            new Point3D(x, y, z),
            new Point3D(x + Size.Length, y, z),
            new Point3D(x + Size.Length, y + Size.Width, z),
            new Point3D(x, y + Size.Width, z)
        };
        
    }

    public Point3D[] GetRoofPoints()
    {
        Point3D[] basePoints = GetBasePoints();
        Point3D[] roofPoints = new Point3D[basePoints.Length];

        for (int i = 0; i < basePoints.Length; i++)
        {
            roofPoints[i] = new Point3D(
                basePoints[i].X,
                basePoints[i].Y,
                basePoints[i].Z + Size.Height);
        }
        return roofPoints;
    }

    public bool Contains(Box3D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return 
            other.Origin.X >= Origin.X &&
            other.Origin.Y >= Origin.Y &&
            other.Origin.Z >= Origin.Z &&
            
            other.Origin.X + other.Size.Length <= Origin.X + Size.Length &&
            other.Origin.Y + other.Size.Width <= Origin.X + Size.Width &&
            other.Origin.Z + other.Size.Height <= Origin.Z + Size.Height;
    }
    public double GetHorizontalDistanceTo(Box3D other)
    {
        ArgumentNullException.ThrowIfNull(other);

        double gapX = Math.Max(
            0,
            Math.Max(
                other.Origin.X - (Origin.X + Size.Length),
                Origin.X - (other.Origin.X + other.Size.Length)));

        double gapY = Math.Max(
            0,
            Math.Max(
                other.Origin.Y - (Origin.Y + Size.Width),
                Origin.Y - (other.Origin.Y + other.Size.Width)));

        return Math.Sqrt(gapX * gapX + gapY * gapY);
    }
    public bool IntersectsHorizontally(Box3D other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return
            Origin.X < other.Origin.X + other.Size.Length &&
            Origin.X + Size.Length > other.Origin.X &&

            Origin.Y < other.Origin.Y + other.Size.Width &&
            Origin.Y + Size.Width > other.Origin.Y;
    }
}
