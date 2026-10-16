using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Geometry;

namespace SceneGenerator.Core.Generation;

public class ObstacleSizeGenerator
{
    private readonly Random _random;
    public  ObstacleSizeGenerator(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    public Size3D Generate(ObstacleSizeRanges ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        return new Size3D(NextValue(ranges.Length), NextValue(ranges.Width), NextValue(ranges.Height));
    }
    private double NextValue(DoubleRange range)
    {
        return range.Min + _random.NextDouble() * (range.Max - range.Min);
    }
}