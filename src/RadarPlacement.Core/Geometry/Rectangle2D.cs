namespace RadarPlacement.Core.Geometry;

/// <summary>
/// Прямоугольник со сторонами, параллельными осям координат.
/// В первой версии ядра такая геометрия описывает сцену, КИИ и каждую помеху.
/// Поворот прямоугольников и произвольные многоугольники пока не поддерживаются.
/// </summary>
public sealed record Rectangle2D
{
    /// <summary>Минимальная координата X.</summary>
    public double MinX { get; }
    /// <summary>Минимальная координата Y.</summary>
    public double MinY { get; }
    /// <summary>Максимальная координата X.</summary>
    public double MaxX { get; }
    /// <summary>Максимальная координата Y.</summary>
    public double MaxY { get; }
    /// <summary>Размер вдоль X.</summary>
    public double Width => MaxX - MinX;
    /// <summary>Размер вдоль Y.</summary>
    public double Height => MaxY - MinY;
    /// <summary>Площадь в квадратных метрах.</summary>
    public double Area => Width * Height;
    /// <summary>Центр прямоугольника; для КИИ это одна из допустимых позиций радара.</summary>
    public Point2D Center => new(MinX + Width / 2, MinY + Height / 2);

    /// <summary>
    /// Создаёт прямоугольник по минимальным и максимальным координатам, а не по размерам.
    /// Например, (10, 20, 110, 70) задаёт прямоугольник размером 100 × 50 м.
    /// </summary>
    public Rectangle2D(double minX, double minY, double maxX, double maxY)
    {
        if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
            !double.IsFinite(maxX) || !double.IsFinite(maxY))
            throw new ArgumentException("Все координаты прямоугольника должны быть конечными.");
        double width = maxX - minX;
        double height = maxY - minY;
        if (width <= 0 || height <= 0 || !double.IsFinite(width * height) || width * height <= 0)
            throw new ArgumentException("Прямоугольник должен иметь положительные конечные размеры и площадь.");
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    /// <summary>Проверяет принадлежность точки прямоугольнику, включая его границу.</summary>
    public bool Contains(Point2D point) =>
        point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

    /// <summary>Проверяет полное включение другого прямоугольника, включая границы.</summary>
    public bool Contains(Rectangle2D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.MinX >= MinX && other.MaxX <= MaxX && other.MinY >= MinY && other.MaxY <= MaxY;
    }

    /// <summary>
    /// Проверяет пересечение с положительной площадью. Касание сторонами или углами
    /// допускается и не считается наложением внутренних областей.
    /// </summary>
    public bool OverlapsInterior(Rectangle2D other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return MinX < other.MaxX && MaxX > other.MinX && MinY < other.MaxY && MaxY > other.MinY;
    }
}
