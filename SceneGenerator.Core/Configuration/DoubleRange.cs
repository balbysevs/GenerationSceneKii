namespace SceneGenerator.Core.Configuration;

public class DoubleRange
{
    public double Min { get; }
    public double Max { get; }

    public DoubleRange(double min, double max)
    {
        if (!double.IsFinite(min))
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), "Минимум должен быть конечным числом.");
        }

        if (!double.IsFinite(max))
        {
            throw new ArgumentOutOfRangeException(
                nameof(max), "Максимум должен быть конечным числом.");
        }

        if (min > max)
        {
            throw new ArgumentException(
                "Минимум не должен превышать максимум.");
        }

        Min = min;
        Max = max;
    }

    public bool Contains(double value)
    {
        return double.IsFinite(value) &&
               value >= Min &&
               value <= Max;
    }
}