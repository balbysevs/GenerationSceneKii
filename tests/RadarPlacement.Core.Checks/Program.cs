using RadarPlacement.Core;
using RadarPlacement.Core.Configuration;
using RadarPlacement.Core.Discretization;
using RadarPlacement.Core.Geometry;
using RadarPlacement.Core.Models;
using RadarPlacement.Core.Optimization;
using RadarPlacement.Core.Results;

// Автономные проверки без NuGet и без тестового фреймворка.
// Этот исполняемый проект нужен только разработчику: пользовательского CLI здесь нет.
// При первом упавшем сценарии печатается ошибка; остальные сценарии всё равно выполняются.
var checks = new (string Name, Action Run)[]
{
    ("Геометрия: конечные координаты и положительные размеры", CheckGeometryValidation),
    ("Сцена: границы, пересечения, касания и независимость от входного списка", CheckSceneValidation),
    ("Кандидаты: углы, центр, порядок, фактический шаг", CheckCandidates),
    ("Видимость: радиус, непрозрачность помех и прозрачность КИИ", CheckVisibility),
    ("Видимость: касание угла, параллельный луч и начало на границе", CheckBoundaryRays),
    ("Поиск: контрпример для жадного алгоритма", CheckGreedyCounterexample),
    ("Поиск: 200 случайных задач против независимого полного перебора", CheckRandomSearch),
    ("Поиск: одинаковые, пустые маски и единственный вариант", CheckDegenerateMasks),
    ("Полный расчёт: 24 сцены против независимого перебора позиций", CheckSceneOptima),
    ("Полный расчёт: радиусы 3000–7000 м на сцене из диапазонов ТЗ", CheckRealScale),
    ("Карта: одинаковые площади, сдвинутая сцена, неполные шаги", CheckGridGeometry),
    ("Карта: индивидуальное покрытие и отсутствие двойного учёта", CheckCoverageMap),
    ("Ровно 10 радаров при пяти кандидатах", CheckRepeatedPositions),
    ("Пустая наблюдаемая сетка: нулевой объём выборки и неопределённый процент", CheckEmptyGrid),
    ("Настройки: некорректные значения отклоняются", CheckSettingsValidation),
    ("Лимиты: позиции, ячейки, память и чрезвычайно мелкая сетка", CheckLimits),
    ("Отмена: до начала, при видимости и в точном поиске", CheckCancellation),
    ("Прогресс: последовательность стадий и завершение", CheckProgress),
    ("Повторяемость результата и параллельные вызовы одного экземпляра", CheckDeterminism),
    ("Публичные коллекции защищены от изменения", CheckReadOnlyResults)
};

int failures = 0;
foreach (var (name, run) in checks)
{
    try
    {
        run();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {name}\n{exception}");
    }
}
Console.WriteLine($"Проверок: {checks.Length}; успешно: {checks.Length - failures}; ошибок: {failures}.");
return failures == 0 ? 0 : 1;

static Rectangle2D R(double x1, double y1, double x2, double y2) => new(x1, y1, x2, y2);
static Scene2D Example() => new(R(0, 0, 10, 10), R(2, 2, 4, 4), [R(5, 2, 6, 4)]);
static RadarPlacementResult2D Solve(Scene2D scene, int count = 2, double radius = 4,
    double candidateStep = 2, double coverageStep = 1) => new RadarPlacementSolver2D().Solve(
        scene, new() { RadarCount = count, Radius = radius },
        new() { CandidateStep = candidateStep, CoverageStep = coverageStep });

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Near(double actual, double expected, double tolerance = 1e-9) =>
    Require(Math.Abs(actual - expected) <= tolerance, $"Ожидалось {expected}, получено {actual}.");

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Ожидалось исключение {typeof(T).Name}.");
}

static void CheckGeometryValidation()
{
    Throws<ArgumentOutOfRangeException>(() => new Point2D(double.NaN, 0));
    Throws<ArgumentOutOfRangeException>(() => new Point2D(0, double.PositiveInfinity));
    Throws<ArgumentException>(() => R(1, 0, 1, 1));
    Throws<ArgumentException>(() => R(0, 0, -1, 1));
    Throws<ArgumentException>(() => R(0, 0, double.PositiveInfinity, 1));
    Throws<ArgumentException>(() => R(-double.MaxValue, 0, double.MaxValue, 1));
    Near(new Point2D(0, 0).DistanceTo(new(3, 4)), 5);
    Near(R(10, 20, 110, 70).Area, 5000);
}

static void CheckSceneValidation()
{
    Throws<ArgumentException>(() => new Scene2D(R(0, 0, 10, 10), R(-1, 1, 2, 2)));
    Throws<ArgumentException>(() => new Scene2D(R(0, 0, 10, 10), R(1, 1, 2, 2), [R(9, 9, 11, 11)]));
    Throws<ArgumentException>(() => new Scene2D(R(0, 0, 10, 10), R(1, 1, 3, 3), [R(2, 2, 4, 4)]));
    Throws<ArgumentException>(() => new Scene2D(R(0, 0, 10, 10), R(1, 1, 2, 2), [R(5, 5, 7, 7), R(6, 6, 8, 8)]));
    var obstacles = new List<Rectangle2D> { R(4, 2, 5, 4) };
    var scene = new Scene2D(R(0, 0, 10, 10), R(2, 2, 4, 4), obstacles);
    obstacles.Clear();
    Require(scene.Obstacles.Count == 1, "Сцена не должна зависеть от изменения входного списка.");
    Require(!scene.IsObservable(new(4, 3)), "Граница здания исключается из наблюдаемой области.");
}

static void CheckCandidates()
{
    var kii = R(-2, 4, 8, 10);
    var points = CandidatePositionGenerator2D.Generate(kii, 3);
    Require(points.Count == 13 && points.Distinct().Count() == 13, "Ожидалось 12 точек периметра и центр.");
    Require(points[0] == new Point2D(-2, 4) && points[^1] == kii.Center, "Неверные начало/центр.");
    foreach (var corner in new[] { new Point2D(-2, 4), new(8, 4), new(8, 10), new(-2, 10) })
        Require(points.Contains(corner), "Потерян угол.");
    for (int i = 0; i < points.Count - 1; i++)
    {
        Point2D p = points[i];
        Require(p.X == kii.MinX || p.X == kii.MaxX || p.Y == kii.MinY || p.Y == kii.MaxY, "Кандидат вне края.");
        Require(p.DistanceTo(points[(i + 1) % (points.Count - 1)]) <= 3, "Превышен шаг периметра.");
    }
}

static void CheckVisibility()
{
    var scene = Example();
    Require(!Visibility2D.CanObserve(scene, new(3, 3), new(8, 3), 100), "Помеха не закрыла луч.");
    Require(Visibility2D.CanObserve(scene, new(3, 3), new(0, 3), 3), "КИИ не должен закрывать обзор; граница круга включена.");
    Require(!Visibility2D.CanObserve(scene, new(3, 3), new(0, 3), 2.99), "Точка вне радиуса.");
    Require(Visibility2D.CanObserve(scene, new(3, 3), new(8, 8), 100), "Свободный луч должен проходить.");
    Require(!Visibility2D.CanObserve(scene, new(3, 3), new(3, 3), 100), "Внутренность КИИ не наблюдается.");
    Require(!Visibility2D.CanObserve(scene, new(3, 3), new(5, 3), 100), "Граница помехи не наблюдается.");
    Require(!Visibility2D.CanObserve(scene, new(3, 3), new(-1, 1), 100), "Точка за границами сцены.");
}

static void CheckBoundaryRays()
{
    var tangent = new Scene2D(R(0, 0, 10, 10), R(2, 2, 4, 4), [R(4, 5, 5, 6)]);
    Require(!Visibility2D.CanObserve(tangent, new(3, 3), new(7, 7), 100), "Касание угла помехи закрывает луч.");
    Require(Visibility2D.CanObserve(tangent, new(3, 3), new(3, 9), 100), "Параллельный свободный луч.");
    Require(Visibility2D.CanObserve(tangent, new(3, 3), new(0, 0), 100), "Помеха позади луча не мешает.");
    var touching = new Scene2D(R(0, 0, 10, 10), R(2, 2, 4, 4), [R(4, 2, 5, 4)]);
    Require(Visibility2D.CanObserve(touching, new(4, 3), new(1, 1), 100), "Касание только в начале не перекрывает исходящий луч.");
    Require(!Visibility2D.CanObserve(touching, new(4, 3), new(8, 3), 100), "Входящий в помеху луч должен перекрываться.");
}

static void CheckGreedyCounterexample()
{
    // A покрывает 5 точек, B и C — по 4. Жадно выбирается A, затем C: всего 7.
    // Глобальное решение B+C покрывает 8. Ещё две точки недостижимы вообще.
    ulong[][] masks = [[0b00011111], [0b00100111], [0b11011000]];
    var result = new MaximumCoverageSearch(masks, 2, 10, default).Run();
    Require(result.CoveredCount == 8 && result.Indices.SequenceEqual(new[] { 1, 2 }), "Не найден глобальный максимум B+C.");
    Require(result.VisitedNodes > 0, "Контрпример должен потребовать точного поиска.");
}

static void CheckRandomSearch()
{
    var random = new Random(81371);
    for (int trial = 0; trial < 200; trial++)
    {
        int cells = random.Next(1, 91); // Часть случаев пересекает границу машинного слова в 64 бита.
        int candidates = random.Next(5, 11);
        int count = random.Next(1, 5);
        var masks = new ulong[candidates][];
        for (int i = 0; i < candidates; i++)
        {
            masks[i] = new ulong[(cells + 63) / 64];
            for (int cell = 0; cell < cells; cell++)
                if (random.Next(4) == 0) masks[i][cell / 64] |= 1UL << (cell % 64);
        }
        var result = new MaximumCoverageSearch(masks, count, cells, default).Run();
        int expected = BruteForce(masks, count);
        Require(result.CoveredCount == expected, $"Задача {trial}: ожидалось {expected}, получено {result.CoveredCount}.");
        Require(result.Indices.Length == count && result.Indices.Distinct().Count() == count, "Неверное количество разных кандидатов.");
        Require(UnionSize(masks, result.Indices) == expected, "Выбранные индексы не соответствуют значению цели.");
    }
}

static void CheckDegenerateMasks()
{
    ulong[][] masks = [[0], [3], [3], [0], [4]];
    for (int count = 1; count <= masks.Length; count++)
        Require(new MaximumCoverageSearch(masks, count, 8, default).Run().CoveredCount == BruteForce(masks, count),
            "Ошибка при одинаковых/пустых покрытиях.");
    Require(new MaximumCoverageSearch([[0]], 1, 0, default).Run().Indices.Single() == 0, "Единственный пустой кандидат.");
}

static void CheckSceneOptima()
{
    var random = new Random(671);
    for (int trial = 0; trial < 24; trial++)
    {
        var obstacle = R(6, random.Next(0, 7), 7, 9);
        var scene = new Scene2D(R(0, 0, 10, 10), R(2, 2, 4, 4), [obstacle]);
        int count = 1 + trial % 3;
        double radius = 2 + random.NextDouble() * 5;
        var result = Solve(scene, count, radius, 1);
        var masks = new ulong[result.CandidatePositions.Count][];
        for (int candidate = 0; candidate < masks.Length; candidate++)
        {
            masks[candidate] = new ulong[2];
            for (int row = 0; row < 10; row++)
                for (int column = 0; column < 10; column++)
                    if (Visibility2D.CanObserve(scene, result.CandidatePositions[candidate], new(column + 0.5, row + 0.5), radius))
                    {
                        int cell = row * 10 + column;
                        masks[candidate][cell / 64] |= 1UL << (cell % 64);
                    }
        }
        Require(result.Coverage.CoveredCellCount == BruteForce(masks, count), $"Ошибка максимума в сцене {trial}.");
        Require(result.IsGloballyOptimalForDiscreteModel && result.Radars.Count == count, "Неверный контракт результата.");
    }
}

static void CheckRealScale()
{
    var scene = new Scene2D(R(0, 0, 10000, 10000), R(4500, 4500, 5500, 5500), [R(6500, 5000, 6600, 5100)]);
    var small = Solve(scene, 2, 3000, 500, 500);
    var large = Solve(scene, 2, 7000, 500, 500);
    Require(small.Coverage.CoveredCellCount > 0, "Пустое покрытие в реальном масштабе.");
    Require(large.Coverage.CoveredCellCount >= small.Coverage.CoveredCellCount, "Увеличение радиуса не может ухудшать максимум.");
    Require(large.Coverage.CoveredArea <= large.Coverage.ObservableArea, "Покрытие больше наблюдаемой области.");
}

static void CheckGridGeometry()
{
    var scene = new Scene2D(R(-7, 13, 3, 20), R(-3, 15, -1, 17));
    var result = Solve(scene, 1, 100, coverageStep: 3);
    var map = result.Coverage;
    Require(map.Columns == 4 && map.Rows == 3, "Число строк/столбцов должно округляться вверх.");
    Near(map.CellWidth, 2.5);
    Near(map.CellHeight, 7.0 / 3);
    Near(map.CellArea * map.Columns * map.Rows, 70);
    Near(map.GetCellCenter(0, 0).X, -5.75);
    Near(map.GetCellCenter(0, 0).Y, 13 + 7.0 / 6);
    Throws<ArgumentOutOfRangeException>(() => map.GetCellCenter(-1, 0));
    Throws<ArgumentOutOfRangeException>(() => map.GetCellState(0, map.Rows));
    Throws<ArgumentOutOfRangeException>(() => map.IsCoveredByRadar(1, 0, 0));
}

static void CheckCoverageMap()
{
    var scene = Example();
    var result = Solve(scene, 3, 5, 1);
    int observed = 0, covered = 0, individualSum = 0;
    for (int row = 0; row < result.Coverage.Rows; row++)
        for (int column = 0; column < result.Coverage.Columns; column++)
        {
            Point2D point = result.Coverage.GetCellCenter(column, row);
            bool free = scene.IsObservable(point);
            bool any = false;
            if (free) observed++;
            for (int radar = 0; radar < result.Radars.Count; radar++)
            {
                bool visible = Visibility2D.CanObserve(scene, result.Radars[radar].Position, point, result.Radars[radar].Radius);
                Require(result.Coverage.IsCoveredByRadar(radar, column, row) == visible, "Ошибка индивидуальной карты.");
                if (visible) individualSum++;
                any |= visible;
            }
            if (any) covered++;
            var expected = !free ? CoverageCellState2D.Excluded : any ? CoverageCellState2D.Covered : CoverageCellState2D.Uncovered;
            Require(result.Coverage.GetCellState(column, row) == expected, "Ошибка объединённой карты.");
        }
    Require(covered == result.Coverage.CoveredCellCount && observed == result.Coverage.ObservableCellCount, "Ошибка подсчёта ячеек.");
    Require(individualSum > covered, "Проверочная сцена должна содержать перекрытие покрытий.");
    Near(result.Coverage.CoveragePercent!.Value, 100.0 * covered / observed);
}

static void CheckRepeatedPositions()
{
    var result = Solve(Example(), 10, 4, 100);
    Require(result.CandidatePositions.Count == 5 && result.Radars.Count == 10, "Должно быть 10 установок на пяти позициях.");
    Require(result.Radars.Select(r => r.Position).Distinct().Count() == 5, "Должны быть использованы все пять позиций.");
    Require(result.Coverage.CoveredCellCount == Solve(Example(), 5, 4, 100).Coverage.CoveredCellCount, "Дубли не увеличивают покрытие.");
}

static void CheckEmptyGrid()
{
    var scene = new Scene2D(R(0, 0, 10, 10), R(4, 4, 6, 6));
    var result = Solve(scene, 3, 10, coverageStep: 100);
    Require(result.Coverage.ObservableCellCount == 0 && result.Coverage.CoveredCellCount == 0, "Единственный центр должен быть исключён.");
    Require(result.Coverage.CoveragePercent is null && result.Radars.Count == 3, "Неверный результат пустой сетки.");
}

static void CheckSettingsValidation()
{
    var solver = new RadarPlacementSolver2D();
    foreach (int count in new[] { 0, -1, 11 })
        Throws<ArgumentOutOfRangeException>(() => solver.Solve(Example(), new() { RadarCount = count }));
    foreach (double radius in new[] { 0, -1, double.NaN, double.PositiveInfinity })
        Throws<ArgumentOutOfRangeException>(() => solver.Solve(Example(), new() { Radius = radius }));
    foreach (var settings in new[] {
        new DiscretizationSettings2D { CandidateStep = 0 }, new() { CoverageStep = double.NaN },
        new() { MaxCandidatePositions = 0 }, new() { MaxGridCells = 0 }, new() { MaxBitsetBytes = 0 } })
        Throws<ArgumentOutOfRangeException>(() => solver.Solve(Example(), new(), settings));
}

static void CheckLimits()
{
    var solver = new RadarPlacementSolver2D();
    foreach (var settings in new[] {
        new DiscretizationSettings2D { MaxCandidatePositions = 4 },
        new() { CoverageStep = 1, MaxGridCells = 50 },
        new() { CoverageStep = 1, MaxBitsetBytes = 1 },
        new() { CandidateStep = double.Epsilon }, new() { CoverageStep = double.Epsilon } })
        Throws<DiscretizationLimitExceededException>(() => solver.Solve(Example(), new(), settings));
}

static void CheckCancellation()
{
    var solver = new RadarPlacementSolver2D();
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    Throws<OperationCanceledException>(() => solver.Solve(Example(), new(), cancellationToken: cancelled.Token));
    using var during = new CancellationTokenSource();
    bool completed = false;
    var progress = new InlineProgress(value =>
    {
        if (value.Phase == SolverPhase2D.ComputingVisibility) during.Cancel();
        if (value.Phase == SolverPhase2D.Completed) completed = true;
    });
    Throws<OperationCanceledException>(() => solver.Solve(Example(), new(), new() { CoverageStep = 1 }, progress, during.Token));
    Require(!completed, "Отмена не должна выдавать Completed.");
    using var searching = new CancellationTokenSource();
    ulong[][] masks = [[0b00011111], [0b00100111], [0b11011000]];
    var search = new MaximumCoverageSearch(masks, 2, 10, searching.Token,
        (visited, _) => { if (visited > 0) searching.Cancel(); });
    Throws<OperationCanceledException>(() => search.Run());
}

static void CheckProgress()
{
    var updates = new List<SolverProgress2D>();
    var result = new RadarPlacementSolver2D().Solve(Example(), new() { RadarCount = 2, Radius = 4 },
        new() { CandidateStep = 1, CoverageStep = 1 }, new InlineProgress(updates.Add));
    Require(updates[0].Phase == SolverPhase2D.PreparingGrid && updates[^1].Phase == SolverPhase2D.Completed, "Неверные стадии начала/конца.");
    Require(updates.Any(p => p.Phase == SolverPhase2D.Searching), "Отсутствует стадия поиска.");
    Require(updates[^1].BestCoveredCellCount == result.Coverage.CoveredCellCount, "Прогресс не совпадает с результатом.");
}

static void CheckDeterminism()
{
    var solver = new RadarPlacementSolver2D();
    var scene = Example();
    var settings = new RadarSettings2D { RadarCount = 3, Radius = 4 };
    var grid = new DiscretizationSettings2D { CandidateStep = 1, CoverageStep = 1 };
    var expected = solver.Solve(scene, settings, grid);
    Parallel.For(0, 8, _ =>
    {
        var result = solver.Solve(scene, settings, grid);
        Require(result.Radars.SequenceEqual(expected.Radars), "Позиции должны быть воспроизводимыми.");
        Require(result.Coverage.CoveredCellCount == expected.Coverage.CoveredCellCount, "Параллельные вызовы влияют друг на друга.");
    });
}

static void CheckReadOnlyResults()
{
    var result = Solve(Example());
    Throws<NotSupportedException>(() => ((IList<PlacedRadar2D>)result.Radars).Clear());
    Throws<NotSupportedException>(() => ((IList<Point2D>)result.CandidatePositions).Clear());
    Throws<NotSupportedException>(() => ((IList<Rectangle2D>)result.Scene.Obstacles).Clear());
}

// Эталон намеренно не использует оптимизатор, его оценки, PopCount или объединение ulong.
// Он перебирает все сочетания, собирая номера видимых ячеек в обычный HashSet<int>.
static int BruteForce(ulong[][] masks, int count)
{
    int best = 0;
    var selected = new int[count];
    void Enumerate(int start, int depth)
    {
        if (depth == count)
        {
            best = Math.Max(best, UnionSize(masks, selected));
            return;
        }
        for (int i = start; i <= masks.Length - (count - depth); i++)
        {
            selected[depth] = i;
            Enumerate(i + 1, depth + 1);
        }
    }
    Enumerate(0, 0);
    return best;
}

static int UnionSize(ulong[][] masks, int[] indices)
{
    var cells = new HashSet<int>();
    foreach (int index in indices)
        for (int word = 0; word < masks[index].Length; word++)
            for (int bit = 0; bit < 64; bit++)
                if ((masks[index][word] & (1UL << bit)) != 0) cells.Add(word * 64 + bit);
    return cells.Count;
}

internal sealed class InlineProgress(Action<SolverProgress2D> report) : IProgress<SolverProgress2D>
{
    public void Report(SolverProgress2D value) => report(value);
}
