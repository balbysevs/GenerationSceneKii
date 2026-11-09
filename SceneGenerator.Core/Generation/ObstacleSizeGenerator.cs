using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Geometry;

namespace SceneGenerator.Core.Generation;

public class ObstacleSizeGenerator
{
    private readonly Random _random;
    /// <summary>Использует переданный источник случайных чисел для выбора размеров помех.</summary>
    public  ObstacleSizeGenerator(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    /// <summary>Независимо выбирает длину, ширину и высоту помехи из соответствующих диапазонов.</summary>
    public Size3D Generate(ObstacleSizeRanges ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        return new Size3D(NextValue(ranges.Length), NextValue(ranges.Width), NextValue(ranges.Height));
    }
    /// <summary>Выбирает случайное значение из диапазона или возвращает его единственное значение.</summary>
    private double NextValue(DoubleRange range)
    {
        return range.Min + _random.NextDouble() * (range.Max - range.Min);
    }
}