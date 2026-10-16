using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;
using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Generation;

namespace SceneGenerator.Console;
class Program
{
    static void Main(string[] args)
    {
        Point3D point = new Point3D(100, 200, 5);
        
        Size3D  size = new Size3D(200, 100, 20);
        
        Box3D box = new Box3D(point, size);

        SceneObject Kii = new SceneObject("Kii-001", SceneObjectType.Kii, box);

        Box3D bounds = new Box3D(
            new Point3D(0, 0, 0),
            new Size3D(1000, 1000, 50));

        Scene scene = new Scene("scene-001",
            SceneMode.ThreeD,
            bounds,
            Kii);
        Box3D first = new Box3D(
            new Point3D(0, 0, 0),
            new Size3D(100, 100, 20));

        Box3D second = new Box3D(
            new Point3D(130, 140, 0),
            new Size3D(50, 50, 10));

        double distance = first.GetHorizontalDistanceTo(second);
        
        SceneObject obstacle = new SceneObject(
            "obstacle-001",
            SceneObjectType.Obstacle,
            new Box3D(
                new Point3D(500, 200, 0),
                new Size3D(60, 80, 15)));

        scene.AddObstacle(obstacle);

        System.Console.WriteLine(
            $"Количество объектов после добавления: {scene.Objects.Count}");
        DoubleRange lengthRange = new DoubleRange(20, 100);

        System.Console.WriteLine(
            $"Диапазон длины: {lengthRange.Min}–{lengthRange.Max} м");

        System.Console.WriteLine(
            $"60 м входит в диапазон: {lengthRange.Contains(60)}");

        System.Console.WriteLine(
            $"150 м входит в диапазон: {lengthRange.Contains(150)}");
        ObstacleSizeRanges obstacleSizes = new ObstacleSizeRanges(
            new DoubleRange(20, 100),
            new DoubleRange(20, 100),
            new DoubleRange(3, 40));

        System.Console.WriteLine(
            $"Длина помех: {obstacleSizes.Length.Min}–{obstacleSizes.Length.Max} м");

        System.Console.WriteLine(
            $"Ширина помех: {obstacleSizes.Width.Min}–{obstacleSizes.Width.Max} м");

        System.Console.WriteLine(
            $"Высота помех: {obstacleSizes.Height.Min}–{obstacleSizes.Height.Max} м");
        GenerationSettings settings = new GenerationSettings
        {
            Mode = SceneMode.ThreeD,

            SceneSize = new Size3D(10000, 10000, 40),

            KiiSize = new Size3D(200, 100, 20),

            ObstacleSizes = new ObstacleSizeRanges(
                new DoubleRange(20, 100),
                new DoubleRange(20, 100),
                new DoubleRange(3, 40)),

            ObstacleCount = 20,

            DistanceToNearestObstacle = 500,

            MinDistanceBetweenObstacles = 30,

            SceneCount = 10,

            Seed = 12345
        };

        System.Console.WriteLine(
            $"Режим: {settings.Mode}, сцен: {settings.SceneCount}, " +
            $"помех на сцену: {settings.ObstacleCount}");
        GenerationSettingsValidator validator = new();

        IReadOnlyList<string> errors = validator.Validate(settings);

        if (errors.Count == 0)
        {
            System.Console.WriteLine("Проверенные параметры корректны.");
        }
        else
        {
            foreach (string error in errors)
            {
                System.Console.WriteLine($"Ошибка: {error}");
            }
        }
        SceneFactory factory = new();

        Scene generatedScene = factory.CreateWithKii(
            "scene-001",
            settings);

        Point3D kiiOrigin = generatedScene.Objects[0].Geometry.Origin;

        System.Console.WriteLine(
            $"Угол основания КИИ: X={kiiOrigin.X}, " +
            $"Y={kiiOrigin.Y}, Z={kiiOrigin.Z}");
        Random random = settings.Seed.HasValue
            ? new Random(settings.Seed.Value)
            : new Random();

        ObstacleSizeGenerator sizeGenerator = new(random);

        for (int i = 0; i < 5; i++)
        {
            Size3D obstacleSize = sizeGenerator.Generate(
                settings.ObstacleSizes);

            System.Console.WriteLine(
                $"Помеха {i + 1}: " +
                $"{obstacleSize.Length:F2} × " +
                $"{obstacleSize.Width:F2} × " +
                $"{obstacleSize.Height:F2} м");BoxPlacementGenerator placementGenerator = new(random);

            Size3D candidateSize = sizeGenerator.Generate(
                settings.ObstacleSizes);

            Box3D candidate = placementGenerator.Generate(
                generatedScene.Bounds,
                candidateSize);

            System.Console.WriteLine(
                $"Положение помехи: " +
                $"X={candidate.Origin.X:F2}, " +
                $"Y={candidate.Origin.Y:F2}, " +
                $"Z={candidate.Origin.Z:F2}");

            System.Console.WriteLine(
                $"Внутри сцены: {generatedScene.Bounds.Contains(candidate)}");
            ObstaclePlacementValidator placementValidator = new();

            Box3D kiiBox = generatedScene.Objects[0].Geometry;

            bool overlappingAllowed = placementValidator.IsValid(
                generatedScene,
                kiiBox,
                settings);

            System.Console.WriteLine(
                $"Размещение поверх КИИ разрешено: {overlappingAllowed}");

            Box3D nearbyCandidate = new Box3D(
                new Point3D(
                    kiiBox.Origin.X + kiiBox.Size.Length + 50,
                    kiiBox.Origin.Y,
                    0),
                new Size3D(20, 20, settings.ObstacleSizes.Height.Min));

            bool nearbyAllowed = placementValidator.IsValid(
                generatedScene,
                nearbyCandidate,
                settings);

            System.Console.WriteLine(
                $"Размещение в 50 м от КИИ разрешено: {nearbyAllowed}");
            
            
        }
    }
}