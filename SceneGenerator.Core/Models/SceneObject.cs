using SceneGenerator.Core.Geometry;

namespace SceneGenerator.Core.Models;

public class SceneObject
{
    public string Id { get; }
    public SceneObjectType Type { get; }
    public PolygonPrism3D Geometry { get; }

    /// <summary>Создаёт объект сцены с непустым идентификатором, допустимым типом и заданной геометрией.</summary>
    public SceneObject(
        string id,
        SceneObjectType type,
        PolygonPrism3D geometry)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Идентификатор объекта не должен быть пустым.",
                nameof(id));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        ArgumentNullException.ThrowIfNull(geometry);

        Id = id;
        Type = type;
        Geometry = geometry;
    }
}