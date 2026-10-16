using System.Dynamic;
using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Generation;

public class ObstaclePlacementValidator
{
    public bool IsValid(
        Scene scene,
        Box3D candidate,
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

            if (distance < requiredDistance)
            {
                return false;
            }
        }
        return true;
    }
}