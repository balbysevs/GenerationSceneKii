namespace SceneGenerator.Core.Geometry;

public class PolygonPrism3D
{
    public Polygon2D Footprint { get; }
    public double BaseElevation { get; }
    public double Height { get; }

    public Point3D Origin { get; }
    public Size3D Size { get; }

    /// <summary>Создаёт вертикальную призму по контуру, отметке основания и высоте над основанием.</summary>
    public PolygonPrism3D(Polygon2D footprint, double baseElevation, double height)
    {
        ArgumentNullException.ThrowIfNull(footprint);
        if (!double.IsFinite(baseElevation)) throw new ArgumentOutOfRangeException(nameof(baseElevation));
        if (!double.IsFinite(height) || height < 0 || !double.IsFinite(baseElevation + height))
            throw new ArgumentOutOfRangeException(nameof(height));
        Footprint = footprint;
        BaseElevation = baseElevation;
        Height = height;
        Origin = new Point3D(footprint.MinX, footprint.MinY, baseElevation);
        Size = new Size3D(footprint.MaxX - footprint.MinX, footprint.MaxY - footprint.MinY, height);
    }

    /// <summary>Возвращает вершины внешнего контура на отметке основания; внутренние контуры не включаются.</summary>
    public Point3D[] GetBasePoints() => Footprint.Outer.Select(p => new Point3D(p.X, p.Y, BaseElevation)).ToArray();
    /// <summary>Возвращает вершины внешнего контура на отметке основания плюс высота призмы.</summary>
    public Point3D[] GetRoofPoints() => Footprint.Outer.Select(p => new Point3D(p.X, p.Y, BaseElevation + Height)).ToArray();

    /// <summary>Измеряет кратчайшее расстояние между основаниями призм на плоскости XY.</summary>
    public virtual double GetHorizontalDistanceTo(PolygonPrism3D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Footprint.DistanceTo(other.Footprint);
    }

    /// <summary>Проверяет, перекрываются ли основания на плоскости XY по площади, без учёта высоты.</summary>
    public bool IntersectsHorizontally(PolygonPrism3D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Footprint.Intersects(other.Footprint);
    }
}
