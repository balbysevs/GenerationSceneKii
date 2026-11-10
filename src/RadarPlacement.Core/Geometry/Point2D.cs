namespace RadarPlacement.Core.Geometry;

/// <summary>
/// Неизменяемая точка на горизонтальной плоскости. Обе координаты измеряются в метрах.
/// X направлен вправо (на восток), Y — вверх на плане (на север).
/// </summary>
public readonly record struct Point2D
{
    /// <summary>Координата по оси X, в метрах.</summary>
    public double X { get; }

    /// <summary>Координата по оси Y, в метрах.</summary>
    public double Y { get; }

    /// <summary>Создаёт точку. NaN и бесконечные координаты недопустимы.</summary>
    public Point2D(double x, double y)
    {
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x), "Координата должна быть конечной.");
        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y), "Координата должна быть конечной.");
        X = x;
        Y = y;
    }

    /// <summary>Вычисляет евклидово расстояние до другой точки.</summary>
    public double DistanceTo(Point2D other) => double.Hypot(X - other.X, Y - other.Y);
}
