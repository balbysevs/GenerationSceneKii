using System.Diagnostics;
using RadarPlacement.Core;

namespace RadarPlacement.ConsoleApp;

/// <summary>
/// Выводит стадии и редкие обновления вместо строки на каждый узел поиска.
/// Реализует IProgress напрямую: Report выполняется синхронно, поэтому отложенные
/// сообщения не появятся после возвращения в меню, как это возможно с Progress&lt;T&gt;.
/// </summary>
internal sealed class ProgressDisplay(ConsoleUi ui) : IProgress<SolverProgress2D>
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private SolverPhase2D? _previous;
    private TimeSpan _lastReport;

    public void Report(SolverProgress2D progress)
    {
        TimeSpan now = _elapsed.Elapsed;
        if (progress.Phase == _previous && now - _lastReport < TimeSpan.FromSeconds(2)) return;
        _previous = progress.Phase;
        _lastReport = now;
        string text = progress.Phase switch
        {
            SolverPhase2D.PreparingGrid => "Подготовка сетки наблюдения…",
            SolverPhase2D.ComputingVisibility => $"Видимость: {progress.ProcessedCandidates}/{progress.TotalCandidates} позиций " +
                $"({100.0 * progress.ProcessedCandidates / progress.TotalCandidates:F0}%).",
            // Доля просмотренных сочетаний неизвестна из-за отсечений. Процент завершения
            // поиска не выдумываем; лучшее покрытие до окончания — лишь нижняя оценка.
            SolverPhase2D.Searching => $"Точный поиск: узлов {progress.VisitedSearchNodes:N0}; " +
                $"лучшее покрытие — {progress.BestCoveredCellCount:N0} ячеек; прошло {now.TotalSeconds:F1} с.",
            SolverPhase2D.Completed => $"Оптимум дискретной модели найден: {progress.BestCoveredCellCount:N0} ячеек.",
            _ => throw new ArgumentOutOfRangeException(nameof(progress))
        };
        ui.Line("  " + text);
    }
}
