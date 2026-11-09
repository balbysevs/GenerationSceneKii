using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Generation;

public class SceneFactory
{
    /// <summary>Проверяет настройки и создаёт сцену без помех с КИИ в центре участка по X и Y.</summary>
    public Scene CreateWithKii(
        string sceneId,
        GenerationSettings settings)
    {
        GenerationSettingsValidator validator = new();

        IReadOnlyList<string> errors = validator.Validate(settings);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(Environment.NewLine, errors),
                nameof(settings));
        }

        Box3D bounds = new Box3D(
            new Point3D(0, 0, 0),
            settings.SceneSize);

        double kiiX =
            (settings.SceneSize.Length - settings.KiiSize.Length) / 2;

        double kiiY =
            (settings.SceneSize.Width - settings.KiiSize.Width) / 2;

        Box3D kiiGeometry = new Box3D(
            new Point3D(kiiX, kiiY, 0),
            settings.KiiSize);

        SceneObject kii = new SceneObject(
            "kii-001",
            SceneObjectType.Kii,
            kiiGeometry);

        return new Scene(
            sceneId,
            settings.Mode,
            bounds,
            kii);
    }
}