using SceneGenerator.Core.Models;
namespace SceneGenerator.Core.Configuration;

public class GenerationSettingsValidator
{
    /// <summary>Возвращает список нарушений ограничений генерации; пустой список означает, что настройки допустимы.</summary>
    public IReadOnlyList<string> Validate(GenerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        List<string> errors = new();

        if (!Enum.IsDefined(settings.Mode))
        {
            errors.Add("Указан неизвестный режим сцены.");
        }

        if (settings.SceneCount < 1 || settings.SceneCount > 5000)
        {
            errors.Add("Количество сцен должно быть от 1 до 5000.");
        }

        if (settings.ObstacleCount < 0)
        {
            errors.Add("Количество помех не может быть отрицательным.");
        }

        if (settings.ObstacleCount > 0 &&
            (!double.IsFinite(settings.DistanceToNearestObstacle) ||
             settings.DistanceToNearestObstacle < 100 ||
             settings.DistanceToNearestObstacle > 2000))
        {
            errors.Add(
                "Расстояние до ближайшей помехи должно быть от 100 до 2000 м.");
        }

        if (!double.IsFinite(settings.MinDistanceBetweenObstacles) ||
            settings.MinDistanceBetweenObstacles < 10 ||
            settings.MinDistanceBetweenObstacles > 100)
        {
            errors.Add(
                "Минимальное расстояние между помехами должно быть от 10 до 100 м.");
        }
        ValidateHorizontalSizes(settings, errors);
        ValidateHeights(settings, errors);

        return errors.AsReadOnly();
    }
    /// <summary>Добавляет ошибки размеров участка, КИИ и диапазонов размеров помех по осям X и Y.</summary>
    private static void ValidateHorizontalSizes(
        GenerationSettings settings,
        List<string> errors)
    {
        if (settings.SceneSize is null ||
            settings.KiiSize is null ||
            settings.ObstacleSizes is null)
        {
            errors.Add("Необходимо указать размеры сцены, КИИ и помех.");
            return;
        }

        DoubleRange sceneRange = new DoubleRange(10000, 20000);
        DoubleRange kiiRange = new DoubleRange(50, 4000);
        DoubleRange obstacleRange = new DoubleRange(20, 100);

        if (!sceneRange.Contains(settings.SceneSize.Length) ||
            !sceneRange.Contains(settings.SceneSize.Width))
        {
            errors.Add(
                "Длина и ширина сцены должны быть от 10000 до 20000 м.");
        }

        if (!kiiRange.Contains(settings.KiiSize.Length) ||
            !kiiRange.Contains(settings.KiiSize.Width))
        {
            errors.Add(
                "Длина и ширина КИИ должны быть от 50 до 4000 м.");
        }

        ObstacleSizeRanges sizes = settings.ObstacleSizes;

        if (!obstacleRange.Contains(sizes.Length.Min) ||
            !obstacleRange.Contains(sizes.Length.Max) ||
            !obstacleRange.Contains(sizes.Width.Min) ||
            !obstacleRange.Contains(sizes.Width.Max))
        {
            errors.Add(
                "Диапазоны длины и ширины помех должны находиться в пределах 20–100 м.");
        }
    }

    /// <summary>Проверяет нулевые высоты для 2D и допустимые высоты сцены и объектов для 3D.</summary>
    private static void ValidateHeights(
        GenerationSettings settings,
        List<string> errors)
    {
        if (settings.SceneSize is null ||
            settings.KiiSize is null ||
            settings.ObstacleSizes is null)
        {
            return;
        }

        double sceneHeight = settings.SceneSize.Height;
        double kiiHeight = settings.KiiSize.Height;
        DoubleRange obstacleHeight = settings.ObstacleSizes.Height;

        if (settings.Mode == SceneMode.TwoD)
        {
            if (sceneHeight != 0 ||
                kiiHeight != 0 ||
                obstacleHeight.Min != 0 ||
                obstacleHeight.Max != 0)
            {
                errors.Add(
                    "В режиме 2D высота сцены, КИИ и помех должна быть равна нулю.");
            }

            return;
        }

        if (settings.Mode != SceneMode.ThreeD)
        {
            return;
        }

        DoubleRange sceneRange = new DoubleRange(3, 50);
        DoubleRange kiiRange = new DoubleRange(3, 30);
        DoubleRange obstacleRange = new DoubleRange(3, 40);

        if (!sceneRange.Contains(sceneHeight))
        {
            errors.Add("Высота сцены должна быть от 3 до 50 м.");
        }

        if (!kiiRange.Contains(kiiHeight))
        {
            errors.Add("Высота КИИ должна быть от 3 до 30 м.");
        }

        if (!obstacleRange.Contains(obstacleHeight.Min) ||
            !obstacleRange.Contains(obstacleHeight.Max))
        {
            errors.Add(
                "Диапазон высоты помех должен находиться в пределах 3–40 м.");
        }

        if (kiiHeight > sceneHeight)
        {
            errors.Add("Высота КИИ превышает высоту сцены.");
        }

        if (settings.ObstacleCount > 0 &&
            obstacleHeight.Max > sceneHeight)
        {
            errors.Add(
                "Максимальная высота помех превышает высоту сцены.");
        }
    }
    
}