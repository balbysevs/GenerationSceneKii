using SceneGenerator.Core.Geometry;

namespace SceneGenerator.Core.Models;

public class Scene
{
    private readonly List<SceneObject> _objects = new();

    public string Id { get; }
    public SceneMode Mode {get;}
    public Box3D Bounds {get;}
    public IReadOnlyList<SceneObject> Objects { get; }

    public Scene(
        string id,
        SceneMode mode,
        Box3D bounds,
        SceneObject Kii)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Идентификатор сцен не должен быть пустым", nameof(id));
        }

        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(Kii);
        if (Kii.Type != SceneObjectType.Kii)
        {
            throw new ArgumentException("Первый объект сцены должен быть КИИ.", nameof(Kii));
        }

        if (!bounds.Contains(Kii.Geometry))
        {
            throw new ArgumentException("Объект КИИ должен полность находиться в границах сцены.", nameof(Kii));
        }
        Id = id;
        Mode = mode;
        Bounds = bounds;
        
        _objects.Add(Kii);
        Objects = _objects.AsReadOnly();
    }
    public void AddObstacle(SceneObject obstacle)
    {
        ArgumentNullException.ThrowIfNull(obstacle);

        if (obstacle.Type != SceneObjectType.Obstacle)
        {
            throw new ArgumentException(
                "Добавляемый объект должен быть помехой.",
                nameof(obstacle));
        }

        if (_objects.Any(item => item.Id == obstacle.Id))
        {
            throw new ArgumentException(
                "Объект с таким идентификатором уже существует в сцене.",
                nameof(obstacle));
        }

        if (!Bounds.Contains(obstacle.Geometry))
        {
            throw new ArgumentException(
                "Помеха должна полностью находиться в границах сцены.",
                nameof(obstacle));
        }
        if (_objects.Any(item =>
                item.Geometry.IntersectsHorizontally(obstacle.Geometry)))
        {
            throw new ArgumentException(
                "Основание помехи пересекается с другим объектом сцены.",
                nameof(obstacle));
        }
        _objects.Add(obstacle);
    }
}