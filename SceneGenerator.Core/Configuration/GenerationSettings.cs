using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Configuration;

public class GenerationSettings
{
    public required SceneMode Mode { get; init; }

    public required Size3D SceneSize { get; init; }

    public required Size3D KiiSize { get; init; }

    public required ObstacleSizeRanges ObstacleSizes { get; init; }

    public required int ObstacleCount { get; init; }

    public required double DistanceToNearestObstacle { get; init; }

    public required double MinDistanceBetweenObstacles { get; init; }

    public required int SceneCount { get; init; }

    public int? Seed { get; init; }
}