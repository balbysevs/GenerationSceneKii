using RadarPlacement.Core.Configuration;
using RadarPlacement.Core.Geometry;
using RadarPlacement.Core.Models;

namespace RadarPlacement.Core.Discretization;

/// <summary>Внутренняя сетка; один бит соответствует одной ячейке, строка за строкой снизу вверх.</summary>
internal sealed class SamplingGrid2D
{
    internal Rectangle2D Bounds { get; }
    internal int Columns { get; }
    internal int Rows { get; }
    internal int CellCount { get; }
    internal int WordCount => ObservableMask.Length;
    internal double CellWidth => Bounds.Width / Columns;
    internal double CellHeight => Bounds.Height / Rows;
    internal double CellArea => Bounds.Area / CellCount;
    internal ulong[] ObservableMask { get; }
    internal int ObservableCount { get; }

    internal SamplingGrid2D(Scene2D scene, DiscretizationSettings2D settings,
        int candidateCount, int radarCount, CancellationToken cancellationToken)
    {
        Bounds = scene.Bounds;
        Columns = GridMath.Divisions(Bounds.Width, settings.CoverageStep, settings.MaxGridCells);
        Rows = GridMath.Divisions(Bounds.Height, settings.CoverageStep, settings.MaxGridCells);
        long total = (long)Columns * Rows;
        if (total > settings.MaxGridCells)
            throw new DiscretizationLimitExceededException(
                $"Требуется {total} ячеек, лимит — {settings.MaxGridCells}. Увеличьте CoverageStep или лимит.");
        CellCount = (int)total;
        int words = (int)((total + 63) / 64);

        // C масок кандидатов, C+1 суффиксных объединений, K+1 рабочих масок поиска,
        // одна маска наблюдаемости и одна итоговая маска. Вычисляем ДО больших выделений памяти.
        long bufferCount = 2L * candidateCount + Math.Min(radarCount, candidateCount) + 4;
        long requiredBytes = bufferCount * words * sizeof(ulong);
        if (requiredBytes > settings.MaxBitsetBytes)
            throw new DiscretizationLimitExceededException(
                $"Битовые буферы требуют {requiredBytes} байт, лимит — {settings.MaxBitsetBytes}. " +
                "Увеличьте шаги дискретизации или MaxBitsetBytes.");

        CheckPrecision(Bounds.MinX, Bounds.Width, Columns);
        CheckPrecision(Bounds.MinY, Bounds.Height, Rows);
        if (CellArea <= 0)
            throw new ArgumentException("Площадь ячейки слишком мала для double.");

        ObservableMask = new ulong[words];
        int observable = 0;
        for (int cell = 0; cell < CellCount; cell++)
        {
            if ((cell & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!scene.IsObservable(GetCenter(cell))) continue;
            ObservableMask[cell >> 6] |= 1UL << (cell & 63);
            observable++;
        }
        ObservableCount = observable;
    }

    internal Point2D GetCenter(int index) => new(
        Bounds.MinX + Bounds.Width * (((index % Columns) + 0.5) / Columns),
        Bounds.MinY + Bounds.Height * (((index / Columns) + 0.5) / Rows));

    internal bool IsObservable(int index) => (ObservableMask[index >> 6] & (1UL << (index & 63))) != 0;

    private static void CheckPrecision(double min, double length, int count)
    {
        double first = min + length * (0.5 / count);
        double last = min + length * ((count - 0.5) / count);
        if (first <= min || last >= min + length ||
            (count > 1 && (first >= min + length * (1.5 / count) ||
                           last <= min + length * ((count - 1.5) / count))))
            throw new ArgumentException("Шаг покрытия слишком мал для точности double при данных координатах.");
    }
}
