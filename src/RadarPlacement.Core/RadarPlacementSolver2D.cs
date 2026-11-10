using RadarPlacement.Core.Configuration;
using RadarPlacement.Core.Discretization;
using RadarPlacement.Core.Geometry;
using RadarPlacement.Core.Models;
using RadarPlacement.Core.Optimization;
using RadarPlacement.Core.Results;

namespace RadarPlacement.Core;

/// <summary>
/// Основная точка входа ядра: подготовка конечной модели, расчёт видимости и точная оптимизация.
/// Не читает файлы, не печатает в консоль, не использует Avalonia и не зависит от генератора сцен.
/// Экземпляр не хранит состояние расчёта; независимые вызовы могут выполняться параллельно.
/// </summary>
public sealed class RadarPlacementSolver2D
{
    /// <summary>
    /// Находит глобальный максимум числа покрытых ячеек среди сгенерированных позиций.
    /// Каждая ячейка имеет одинаковую площадь, поэтому это также максимум дискретной площади.
    /// Область КИИ не наблюдается, но КИИ не перекрывает видимость остальной сцены.
    /// </summary>
    /// <param name="scene">Проверенная неизменяемая сцена с одним прямоугольным КИИ.</param>
    /// <param name="radarSettings">Число радаров и общий радиус.</param>
    /// <param name="discretization">Шаги и лимиты; null использует значения по умолчанию.</param>
    /// <param name="progress">Необязательный получатель стадий и промежуточных показателей.</param>
    /// <param name="cancellationToken">Отмена прекращает расчёт исключением, без выдачи неоптимального результата.</param>
    /// <returns>Ровно заданное число установок и карта покрытия с доказанным дискретным максимумом.</returns>
    /// <exception cref="OperationCanceledException">Получен запрос отмены.</exception>
    /// <exception cref="DiscretizationLimitExceededException">Модель превышает заданные лимиты ресурсов.</exception>
    /// <remarks>
    /// Метод синхронный и вычислительно тяжёлый. Оконное приложение должно запускать его
    /// через Task.Run, передавая CancellationToken и при необходимости Progress&lt;SolverProgress2D&gt;.
    /// В худшем случае рассматриваются C(M, K) сочетаний: точный поиск может быть долгим.
    /// </remarks>
    public RadarPlacementResult2D Solve(Scene2D scene, RadarSettings2D radarSettings,
        DiscretizationSettings2D? discretization = null, IProgress<SolverProgress2D>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(radarSettings);
        radarSettings.Validate();
        var settings = discretization ?? new DiscretizationSettings2D();
        settings.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = CandidatePositionGenerator2D.Generate(scene.CriticalInfrastructure,
            settings.CandidateStep, settings.MaxCandidatePositions);
        progress?.Report(new(SolverPhase2D.PreparingGrid, 0, candidates.Count, 0, 0));
        var grid = new SamplingGrid2D(scene, settings, candidates.Count, radarSettings.RadarCount, cancellationToken);

        // Один кандидат — одна битовая маска. Бит установлен только для свободной,
        // находящейся в радиусе и не закрытой помехами контрольной точки.
        var masks = new ulong[candidates.Count][];
        progress?.Report(new(SolverPhase2D.ComputingVisibility, 0, candidates.Count, 0, 0));
        int reportEvery = Math.Max(1, candidates.Count / 100);
        for (int candidate = 0; candidate < candidates.Count; candidate++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mask = new ulong[grid.WordCount];
            for (int cell = 0; cell < grid.CellCount; cell++)
            {
                if ((cell & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (grid.IsObservable(cell) && Visibility2D.IsVisibleUnchecked(scene,
                        candidates[candidate], grid.GetCenter(cell), radarSettings.Radius))
                    mask[cell >> 6] |= 1UL << (cell & 63);
            }
            masks[candidate] = mask;
            if ((candidate + 1) % reportEvery == 0 || candidate == candidates.Count - 1)
                progress?.Report(new(SolverPhase2D.ComputingVisibility, candidate + 1, candidates.Count, 0, 0));
        }

        // Повторение позиции не добавляет покрытия. Поэтому достаточно оптимизировать
        // min(K, M) различных кандидатов. При K > M затем добавим совпадающие установки;
        // это сохраняет максимум и выполняет требование о точном количестве радаров.
        int distinctCount = Math.Min(radarSettings.RadarCount, candidates.Count);
        var search = new MaximumCoverageSearch(masks, distinctCount, grid.ObservableCount,
            cancellationToken, (visited, best) => progress?.Report(
                new(SolverPhase2D.Searching, candidates.Count, candidates.Count, visited, best))).Run();

        var radars = new PlacedRadar2D[radarSettings.RadarCount];
        var selectedMasks = new ulong[radars.Length][];
        var combined = new ulong[grid.WordCount];
        for (int i = 0; i < radars.Length; i++)
        {
            int candidate = search.Indices[i % search.Indices.Length];
            radars[i] = new(candidates[candidate], radarSettings.Radius);
            selectedMasks[i] = masks[candidate];
            MaximumCoverageSearch.UnionInto(combined, masks[candidate], combined);
        }
        var coverage = new CoverageMap2D(grid, combined, selectedMasks, search.CoveredCount);
        var result = new RadarPlacementResult2D(scene, radarSettings, settings, radars, candidates,
            coverage, search.VisitedNodes, search.PrunedNodes);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new(SolverPhase2D.Completed, candidates.Count, candidates.Count,
            search.VisitedNodes, search.CoveredCount));
        return result;
    }
}
