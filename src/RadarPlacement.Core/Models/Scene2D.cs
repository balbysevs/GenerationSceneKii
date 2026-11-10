using RadarPlacement.Core.Geometry;

namespace RadarPlacement.Core.Models;

/// <summary>
/// Независимое от JSON и интерфейса представление одной двумерной сцены.
/// Сцена содержит ровно один КИИ и любое количество прямоугольных помех.
/// Все объекты неизменяемы; входной список помех копируется.
/// </summary>
public sealed class Scene2D
{
    /// <summary>Горизонтальные границы наблюдаемой территории.</summary>
    public Rectangle2D Bounds { get; }
    /// <summary>Основание КИИ: исключается из наблюдаемой площади, но не перекрывает лучи.</summary>
    public Rectangle2D CriticalInfrastructure { get; }
    /// <summary>Непрозрачные помехи. Изменить коллекцию через этот интерфейс нельзя.</summary>
    public IReadOnlyList<Rectangle2D> Obstacles { get; }

    /// <summary>
    /// Создаёт и проверяет сцену. Объекты должны находиться внутри Bounds,
    /// их внутренние области не должны пересекаться. Общие границы допускаются.
    /// Диапазоны размеров из настроек генератора здесь не навязываются:
    /// ядро работает и с небольшими учебными примерами в тех же единицах — метрах.
    /// </summary>
    public Scene2D(Rectangle2D bounds, Rectangle2D criticalInfrastructure,
        IEnumerable<Rectangle2D>? obstacles = null)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(criticalInfrastructure);
        if (!bounds.Contains(criticalInfrastructure))
            throw new ArgumentException("КИИ должен полностью находиться внутри сцены.", nameof(criticalInfrastructure));

        var copy = (obstacles ?? []).ToArray();
        for (int i = 0; i < copy.Length; i++)
        {
            var obstacle = copy[i];
            if (obstacle is null || !bounds.Contains(obstacle))
                throw new ArgumentException($"Помеха {i} отсутствует или выходит за границы сцены.", nameof(obstacles));
            if (obstacle.OverlapsInterior(criticalInfrastructure))
                throw new ArgumentException($"Помеха {i} пересекает КИИ.", nameof(obstacles));
            for (int j = 0; j < i; j++)
                if (obstacle.OverlapsInterior(copy[j]))
                    throw new ArgumentException($"Помехи {j} и {i} пересекаются.", nameof(obstacles));
        }

        Bounds = bounds;
        CriticalInfrastructure = criticalInfrastructure;
        Obstacles = Array.AsReadOnly(copy);
    }

    /// <summary>
    /// Проверяет, является ли точка частью наблюдаемой свободной территории.
    /// Основания КИИ и помех вместе с их границами исключаются.
    /// </summary>
    public bool IsObservable(Point2D point) => Bounds.Contains(point) &&
        !CriticalInfrastructure.Contains(point) && !Obstacles.Any(obstacle => obstacle.Contains(point));
}
