using SceneGenerator.Core.Geometry;

namespace SceneGenerator.Core.Generation;

public class NearestObstaclePlacementGenerator
{
    public Box3D Generate(PolygonPrism3D Kii, Size3D obstacleSize, double distance)
    {
        ArgumentNullException.ThrowIfNull(Kii);
        ArgumentNullException.ThrowIfNull(obstacleSize);

        if (!double.IsFinite(distance) || distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Расстояние должно быть конечным и неотрицательным.");
        }

        double x = Kii.Origin.X + Kii.Size.Length + distance;
        double y = Kii.Origin.Y;
        double z = Kii.Origin.Z;
        
        return new Box3D(new Point3D(x, y, z), obstacleSize);
    }
}