namespace RadarPlacement.Core.Discretization;

/// <summary>Общие безопасные вычисления размеров дискретной модели.</summary>
internal static class GridMath
{
    internal static int Divisions(double length, double step, int limit)
    {
        // Проверка до приведения к int предотвращает переполнение при очень маленьком шаге.
        double value = Math.Max(1, Math.Ceiling(length / step));
        if (!double.IsFinite(value) || value > limit)
            throw new DiscretizationLimitExceededException(
                $"Число делений одной стороны превышает лимит {limit}. Увеличьте шаг или лимит.");
        return (int)value;
    }
}
