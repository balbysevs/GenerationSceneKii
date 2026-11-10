namespace RadarPlacement.Core;

/// <summary>Стадия вычисления; позволяет приложению показывать состояние без зависимости ядра от UI.</summary>
public enum SolverPhase2D
{
    /// <summary>Построение сетки и исключение оснований зданий.</summary>
    PreparingGrid,
    /// <summary>Расчёт видимости для каждого допустимого положения радара.</summary>
    ComputingVisibility,
    /// <summary>Точный комбинаторный поиск по рассчитанным маскам покрытия.</summary>
    Searching,
    /// <summary>Расчёт успешно завершён, оптимальность в дискретной модели установлена.</summary>
    Completed
}

/// <summary>
/// Снимок прогресса. Процент перебора не сообщается: число ветвей, которые удастся отсечь,
/// заранее неизвестно. BestCoveredCellCount — найденная нижняя оценка, до Completed она не гарантирует максимум.
/// </summary>
/// <param name="Phase">Текущая стадия.</param>
/// <param name="ProcessedCandidates">Количество обработанных кандидатов на стадии видимости.</param>
/// <param name="TotalCandidates">Общее количество допустимых позиций.</param>
/// <param name="VisitedSearchNodes">Количество посещённых узлов дерева поиска.</param>
/// <param name="BestCoveredCellCount">Лучшее найденное число покрытых наблюдаемых ячеек.</param>
public sealed record SolverProgress2D(SolverPhase2D Phase, int ProcessedCandidates,
    int TotalCandidates, long VisitedSearchNodes, int BestCoveredCellCount);
