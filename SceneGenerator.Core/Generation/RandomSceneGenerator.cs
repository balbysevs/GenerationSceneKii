using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Generation;

public class RandomSceneGenerator
{
    private readonly SceneFactory _sceneFactory = new();
    private readonly ObstaclePlacementValidator _placementValidator = new();
    private readonly NearestObstaclePlacementGenerator _nearestGenerator = new();

    private readonly ObstacleSizeGenerator _sizeGenerator;
    private readonly BoxPlacementGenerator _placementGenerator;

    /// <summary>Инициализирует выбор размеров и позиций помех одним источником случайных чисел.</summary>
    public RandomSceneGenerator(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        _sizeGenerator = new ObstacleSizeGenerator(random);
        _placementGenerator = new BoxPlacementGenerator(random);
    }

    /// <summary>Создаёт сцену с КИИ и заданным числом помех либо сообщает, что за отведённые попытки разместить их не удалось.</summary>
    public Scene Generate(
        string sceneId,
        GenerationSettings settings,
        int maxAttemptsPerObstacle = 1000)
    {
        if (maxAttemptsPerObstacle < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttemptsPerObstacle),
                "Количество попыток должно быть положительным.");
        }

        Scene scene = _sceneFactory.CreateWithKii(sceneId, settings);

        PolygonPrism3D kiiGeometry = scene.Objects[0].Geometry;

        for (int index = 0; index < settings.ObstacleCount; index++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < maxAttemptsPerObstacle; attempt++)
            {
                Size3D size = _sizeGenerator.Generate(
                    settings.ObstacleSizes);

                Box3D candidate;

                if (index == 0)
                {
                    candidate = _nearestGenerator.Generate(
                        kiiGeometry,
                        size,
                        settings.DistanceToNearestObstacle);
                }
                else
                {
                    candidate = _placementGenerator.Generate(
                        scene.Bounds,
                        size);
                }

                if (!_placementValidator.IsValid(scene, candidate, settings))
                {
                    continue;
                }

                SceneObject obstacle = new SceneObject(
                    $"obstacle-{index + 1:D4}",
                    SceneObjectType.Obstacle,
                    candidate);

                scene.AddObstacle(obstacle);

                placed = true;
                break;
            }

            if (!placed)
            {
                throw new InvalidOperationException(
                    $"Не удалось разместить помеху №{index + 1} " +
                    $"за {maxAttemptsPerObstacle} попыток.");
            }
        }

        return scene;
    }
}