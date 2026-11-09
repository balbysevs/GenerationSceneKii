using SceneGenerator.Core.Geometry;

namespace SceneGenerator.Core.Generation;

public class BoxPlacementGenerator
{
    private readonly Random _random;

    /// <summary>Использует переданный источник случайных чисел для выбора позиции объекта.</summary>
    public BoxPlacementGenerator(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
    }

    /// <summary>Выбирает случайную горизонтальную позицию, в которой объект целиком помещается в заданные границы.</summary>
    public Box3D Generate(Box3D bounds, Size3D size)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(size);

        if (size.Length > bounds.Size.Length ||
            size.Width > bounds.Size.Width ||
            size.Height > bounds.Size.Height)
        {
            throw new ArgumentException("Размеры объекта превышают размеры области размещения.", nameof(size));
        }
        double availableX = bounds.Size.Length - size.Length;
        double availableY = bounds.Size.Width - size.Width;

        double x = bounds.Origin.X + _random.NextDouble() * availableX;
        double y = bounds.Origin.Y + _random.NextDouble() * availableY;
        double z = bounds.Origin.Z;
        
        return new Box3D(new Point3D(x, y, z), size);
    }
}