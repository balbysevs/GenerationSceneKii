using RadarPlacement.Core.Models;

namespace RadarPlacement.Core.Geometry;

/// <summary>
/// Геометрическая модель видимости: круг заданного радиуса и прямая видимость до цели.
/// Основания помех непрозрачны, КИИ не участвует в перекрытии лучей.
/// Все сравнения выполняются над double; касание границы помехи считается перекрытием.
/// </summary>
public static class Visibility2D
{
    /// <summary>
    /// Проверяет видимость одной точки свободной территории. Позволяет отдельно изучать
    /// геометрию без запуска оптимизации. Допустимость позиции радара на крыше здесь
    /// не проверяется: при оптимизации за неё отвечает генератор кандидатов.
    /// Точка на границе круга входит в радиус действия.
    /// </summary>
    public static bool CanObserve(Scene2D scene, Point2D radarPosition, Point2D target, double radius)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!double.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        return scene.IsObservable(target) && IsVisibleUnchecked(scene, radarPosition, target, radius);
    }

    // Внутренняя версия не проверяет принадлежность цели свободной территории:
    // оптимизатор уже один раз проверил это при построении сетки.
    internal static bool IsVisibleUnchecked(Scene2D scene, Point2D radar, Point2D target, double radius)
    {
        if (radar.DistanceTo(target) > radius)
            return false;
        foreach (var obstacle in scene.Obstacles)
            if (BlocksSegment(obstacle, radar, target))
                return false;
        return true;
    }

    // Отрезок задаём параметрически: P(t) = start + t * (end - start), 0 <= t <= 1.
    // Для каждой оси находим интервал t, в котором P лежит между сторонами прямоугольника.
    // Пересечение интервалов даёт часть луча внутри непрозрачного объекта (метод отсечений).
    private static bool BlocksSegment(Rectangle2D rectangle, Point2D start, Point2D end)
    {
        double enter = 0;
        double exit = 1;
        if (!ClipAxis(start.X, end.X - start.X, rectangle.MinX, rectangle.MaxX, ref enter, ref exit) ||
            !ClipAxis(start.Y, end.Y - start.Y, rectangle.MinY, rectangle.MaxY, ref enter, ref exit))
            return false;

        // Если радар касается помехи и смотрит от неё, единственная общая точка — t = 0.
        // Она не закрывает обзор. Проход по самой границе на ненулевую длину закрывает.
        return exit > 0 && enter <= exit;
    }

    private static bool ClipAxis(double start, double direction, double min, double max,
        ref double enter, ref double exit)
    {
        // Параллельный оси луч либо совсем не входит в полосу, либо остаётся в ней.
        if (direction == 0)
            return start >= min && start <= max;
        double first = (min - start) / direction;
        double last = (max - start) / direction;
        if (first > last)
            (first, last) = (last, first);
        enter = Math.Max(enter, first);
        exit = Math.Min(exit, last);
        return enter <= exit;
    }
}
