namespace RadarPlacement.Core.Configuration;

/// <summary>
/// Управляет двумя независимыми приближениями: набором допустимых позиций радаров
/// и сеткой, на которой измеряется покрытие. Меньший шаг увеличивает затраты ресурсов.
/// После дискретизации оптимизатор ищет точный максимум получившейся модели.
/// </summary>
public sealed record DiscretizationSettings2D
{
    /// <summary>
    /// Максимальный промежуток между соседними кандидатами на каждой стороне КИИ, в метрах.
    /// Сторона делится на ceil(длина / шаг) равных отрезков. Все углы и центр включаются всегда.
    /// </summary>
    public double CandidateStep { get; init; } = 100;

    /// <summary>
    /// Максимальный размер стороны ячейки покрытия, в метрах. Число столбцов и строк
    /// округляется вверх, после чего сцена делится на одинаковые прямоугольные ячейки.
    /// Доступность и видимость всей ячейки определяются по её центру.
    /// </summary>
    public double CoverageStep { get; init; } = 100;

    /// <summary>Предельное количество кандидатов. Превышение вызывает исключение до создания списка.</summary>
    public int MaxCandidatePositions { get; init; } = 10_000;

    /// <summary>Предельное количество всех ячеек сетки, включая ячейки внутри зданий.</summary>
    public int MaxGridCells { get; init; } = 1_000_000;

    /// <summary>
    /// Лимит суммарного полезного размера битовых буферов, в байтах (по умолчанию 256 МиБ).
    /// Включает матрицу видимости, границы поиска и рабочие маски; не является лимитом
    /// памяти всего процесса: заголовки объектов, сцена и небольшие списки учитываются отдельно.
    /// При превышении ядро не увеличивает шаг автоматически.
    /// </summary>
    public long MaxBitsetBytes { get; init; } = 256L * 1024 * 1024;

    internal void Validate()
    {
        if (!double.IsFinite(CandidateStep) || CandidateStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(CandidateStep));
        if (!double.IsFinite(CoverageStep) || CoverageStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(CoverageStep));
        if (MaxCandidatePositions < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxCandidatePositions));
        if (MaxGridCells < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxGridCells));
        if (MaxBitsetBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxBitsetBytes));
    }
}
