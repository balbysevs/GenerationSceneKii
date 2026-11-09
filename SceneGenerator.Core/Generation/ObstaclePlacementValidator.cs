using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Generation;

public class ObstaclePlacementValidator
{
    /// <summary>Проверяет границы участка, отсутствие перекрытия оснований и минимальные расстояния до КИИ и помех.</summary>
    public bool IsValid(
        Scene scene,
        PolygonPrism3D candidate,
        GenerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(settings);

        if (!scene.Bounds.Contains(candidate))
        {
            return false;
        }

        foreach (SceneObject existing in scene.Objects)
        {
            if (existing.Geometry.IntersectsHorizontally(candidate))
            {
                return false;
            }

            double distance = existing.Geometry.GetHorizontalDistanceTo(candidate);

            double requiredDistance = existing.Type == SceneObjectType.Kii
                ? settings.DistanceToNearestObstacle
                : settings.MinDistanceBetweenObstacles;

            const double distanceTolerance = 0.000001;

            if (distance < requiredDistance - distanceTolerance)
            {
                return false;
            }
        }
        return true;
    }
}