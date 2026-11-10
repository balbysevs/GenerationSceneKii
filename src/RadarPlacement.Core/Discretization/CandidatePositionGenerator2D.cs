using RadarPlacement.Core.Geometry;

namespace RadarPlacement.Core.Discretization;

/// <summary>Создаёт конечное множество разрешённых позиций на прямоугольном КИИ.</summary>
public static class CandidatePositionGenerator2D
{
    /// <summary>
    /// Равномерно разбивает каждую сторону с промежутком не больше maximumStep,
    /// добавляет углы без повторений и затем центр. Порядок стабилен: от нижнего левого
    /// угла против часовой стрелки, после обхода — центр. Результат неизменяем.
    /// </summary>
    public static IReadOnlyList<Point2D> Generate(Rectangle2D criticalInfrastructure,
        double maximumStep, int maxPositions = 10_000)
    {
        ArgumentNullException.ThrowIfNull(criticalInfrastructure);
        if (!double.IsFinite(maximumStep) || maximumStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumStep));
        if (maxPositions < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPositions));
        int nx = GridMath.Divisions(criticalInfrastructure.Width, maximumStep, maxPositions);
        int ny = GridMath.Divisions(criticalInfrastructure.Height, maximumStep, maxPositions);
        long count = 2L * (nx + (long)ny) + 1;
        if (count > maxPositions)
            throw new DiscretizationLimitExceededException(
                $"Требуется {count} позиций радаров, лимит — {maxPositions}. Увеличьте CandidateStep или лимит.");

        var result = new List<Point2D>((int)count);
        var kii = criticalInfrastructure;
        for (int i = 0; i < nx; i++) result.Add(new(kii.MinX + kii.Width * ((double)i / nx), kii.MinY));
        for (int i = 0; i < ny; i++) result.Add(new(kii.MaxX, kii.MinY + kii.Height * ((double)i / ny)));
        for (int i = 0; i < nx; i++) result.Add(new(kii.MaxX - kii.Width * ((double)i / nx), kii.MaxY));
        for (int i = 0; i < ny; i++) result.Add(new(kii.MinX, kii.MaxY - kii.Height * ((double)i / ny)));
        result.Add(kii.Center);

        // На астрономически больших координатах double может не различать близкие позиции.
        // Такой ввод отклоняем, чтобы не выдавать более грубую сетку за заданную пользователем.
        if (result.Distinct().Count() != result.Count)
            throw new ArgumentException("Шаг позиций слишком мал для точности double при данных координатах.");
        return result.AsReadOnly();
    }
}
