using RadarPlacement.Core.Discretization;
using RadarPlacement.Core.Geometry;

namespace RadarPlacement.Core.Results;

/// <summary>Состояние ячейки относительно наблюдаемой области и объединения покрытий.</summary>
public enum CoverageCellState2D
{
    /// <summary>Центр находится внутри КИИ/помехи или на их границе; ячейка не участвует в целевой функции.</summary>
    Excluded,
    /// <summary>Центр лежит на свободной территории, но не виден ни одному установленному радару.</summary>
    Uncovered,
    /// <summary>Центр свободен и виден хотя бы одному установленному радару.</summary>
    Covered
}

/// <summary>
/// Неизменяемая карта дискретного покрытия, пригодная для отрисовки любым интерфейсом.
/// Столбцы идут слева направо, строки — снизу вверх; индексы начинаются с нуля.
/// Карта содержит объединение покрытий и отдельную маску для каждого установленного радара.
/// </summary>
public sealed class CoverageMap2D
{
    private readonly SamplingGrid2D _grid;
    private readonly ulong[] _combined;
    private readonly ulong[][] _radarMasks;

    /// <summary>Границы сцены, которую покрывает сетка.</summary>
    public Rectangle2D Bounds => _grid.Bounds;
    /// <summary>Количество столбцов сетки.</summary>
    public int Columns => _grid.Columns;
    /// <summary>Количество строк сетки.</summary>
    public int Rows => _grid.Rows;
    /// <summary>Фактическая ширина каждой ячейки, в метрах.</summary>
    public double CellWidth => _grid.CellWidth;
    /// <summary>Фактическая высота каждой ячейки, в метрах.</summary>
    public double CellHeight => _grid.CellHeight;
    /// <summary>Площадь одной ячейки, в квадратных метрах.</summary>
    public double CellArea => _grid.CellArea;
    /// <summary>Число ячеек, центры которых не принадлежат зданиям.</summary>
    public int ObservableCellCount => _grid.ObservableCount;
    /// <summary>Число наблюдаемых ячеек, покрытых хотя бы одним радаром.</summary>
    public int CoveredCellCount { get; }
    /// <summary>Оценка свободной площади: число наблюдаемых ячеек × площадь ячейки.</summary>
    public double ObservableArea => ObservableCellCount * CellArea;
    /// <summary>Оценка покрытой площади, без повторного учёта перекрытий, в квадратных метрах.</summary>
    public double CoveredArea => CoveredCellCount * CellArea;
    /// <summary>
    /// Процент покрытия в дискретной модели. Null означает отсутствие наблюдаемых центров
    /// ячеек; такое возможно и при слишком грубой сетке, а не только при полностью занятой сцене.
    /// </summary>
    public double? CoveragePercent => ObservableCellCount == 0
        ? null : 100.0 * CoveredCellCount / ObservableCellCount;

    internal CoverageMap2D(SamplingGrid2D grid, ulong[] combined, ulong[][] radarMasks, int coveredCount)
    {
        _grid = grid;
        _combined = combined;
        _radarMasks = radarMasks;
        CoveredCellCount = coveredCount;
    }

    /// <summary>Возвращает координаты центра ячейки в метрах.</summary>
    public Point2D GetCellCenter(int column, int row) => _grid.GetCenter(GetIndex(column, row));

    /// <summary>Возвращает состояние ячейки в итоговом объединении покрытий.</summary>
    public CoverageCellState2D GetCellState(int column, int row)
    {
        int index = GetIndex(column, row);
        if (!_grid.IsObservable(index)) return CoverageCellState2D.Excluded;
        return HasBit(_combined, index) ? CoverageCellState2D.Covered : CoverageCellState2D.Uncovered;
    }

    /// <summary>
    /// Показывает, покрывает ли конкретный радар данную ячейку. RadarIndex соответствует
    /// индексу в RadarPlacementResult2D.Radars. Исключённые ячейки всегда возвращают false.
    /// </summary>
    public bool IsCoveredByRadar(int radarIndex, int column, int row)
    {
        if ((uint)radarIndex >= (uint)_radarMasks.Length)
            throw new ArgumentOutOfRangeException(nameof(radarIndex));
        return HasBit(_radarMasks[radarIndex], GetIndex(column, row));
    }

    private int GetIndex(int column, int row)
    {
        if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column));
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
        return row * Columns + column;
    }

    private static bool HasBit(ulong[] mask, int index) => (mask[index >> 6] & (1UL << (index & 63))) != 0;
}
