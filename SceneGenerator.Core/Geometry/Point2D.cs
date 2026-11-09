namespace SceneGenerator.Core.Geometry;

public sealed record Point2D
{
    public double X { get; }
    public double Y { get; }

    /// <summary>Создаёт точку на плоскости с конечными координатами X и Y.</summary>
    public Point2D(double x, double y)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y));
        X = x;
        Y = y;
    }
}
