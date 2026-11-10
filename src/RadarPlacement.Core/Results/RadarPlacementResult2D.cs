using RadarPlacement.Core.Configuration;
using RadarPlacement.Core.Geometry;
using RadarPlacement.Core.Models;

namespace RadarPlacement.Core.Results;

/// <summary>Один установленный радар. Высота и стоимость вышки в двумерной модели отсутствуют.</summary>
/// <param name="Position">Горизонтальные координаты установки.</param>
/// <param name="Radius">Радиус действия в метрах.</param>
public sealed record PlacedRadar2D(Point2D Position, double Radius);

/// <summary>
/// Успешный результат точной оптимизации дискретной модели.
/// При отмене, ошибке или превышении ресурсов результат этого типа не возвращается.
/// Неизменяемые входные данные сохраняются, чтобы результат можно было воспроизвести.
/// </summary>
public sealed class RadarPlacementResult2D
{
    /// <summary>Исходная сцена.</summary>
    public Scene2D Scene { get; }
    /// <summary>Параметры радаров, использованные при расчёте.</summary>
    public RadarSettings2D RadarSettings { get; }
    /// <summary>Параметры дискретизации и лимиты данного расчёта.</summary>
    public DiscretizationSettings2D Discretization { get; }
    /// <summary>
    /// Ровно RadarCount установок. Если кандидатов меньше, чем радаров, некоторые позиции
    /// повторяются: минимального расстояния нет, совместное размещение допускается моделью.
    /// </summary>
    public IReadOnlyList<PlacedRadar2D> Radars { get; }
    /// <summary>Полный набор разрешённых дискретных позиций, полезный для показа сетки размещения.</summary>
    public IReadOnlyList<Point2D> CandidatePositions { get; }
    /// <summary>Покрытие каждого радара, объединённое покрытие и показатели площади.</summary>
    public CoverageMap2D Coverage { get; }
    /// <summary>Количество посещённых узлов точного поиска. Может быть нулём, если начальное решение покрывает всё.</summary>
    public long VisitedSearchNodes { get; }
    /// <summary>Количество узлов, исключённых по доказанной верхней оценке покрытия.</summary>
    public long PrunedSearchNodes { get; }
    /// <summary>
    /// Всегда true для успешно возвращённого результата. Гарантия относится исключительно
    /// к CandidatePositions и центрам ячеек Coverage, а не к непрерывной геометрической задаче.
    /// </summary>
    public bool IsGloballyOptimalForDiscreteModel => true;

    internal RadarPlacementResult2D(Scene2D scene, RadarSettings2D radarSettings,
        DiscretizationSettings2D discretization, PlacedRadar2D[] radars,
        IReadOnlyList<Point2D> candidates, CoverageMap2D coverage, long visited, long pruned)
    {
        Scene = scene;
        RadarSettings = radarSettings;
        Discretization = discretization;
        Radars = Array.AsReadOnly(radars);
        CandidatePositions = candidates;
        Coverage = coverage;
        VisitedSearchNodes = visited;
        PrunedSearchNodes = pruned;
    }
}
