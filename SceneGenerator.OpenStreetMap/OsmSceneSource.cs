using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.OpenStreetMap;

public sealed record OsmBuildingSelection(string Id, double TopHeight, bool Included = true, bool HeightEdited = false);

public sealed record OsmSkippedBuilding(string Id, string Reason);


public sealed class OsmSceneSource : ISceneSource
{
    private readonly Scene _scene;
    public IReadOnlyList<OsmSkippedBuilding> SkippedBuildings { get; }

    /// <summary>Создаёт импортированную сцену с одним выбранным КИИ и пользовательскими правками зданий.</summary>
    public OsmSceneSource(OsmImportDocument document, string kiiId,
        IReadOnlyList<OsmBuildingSelection>? selections = null, CancellationToken cancellationToken = default)
        : this(document, new[] { kiiId }, selections, cancellationToken) { }

    /// <summary>Создаёт сцену с выбранными КИИ в заданном порядке, применяет правки и записывает причины пропуска конфликтующих помех.</summary>
    public OsmSceneSource(OsmImportDocument document, IReadOnlyList<string> kiiIds,
        IReadOnlyList<OsmBuildingSelection>? selections = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kiiIds);
        if (kiiIds.Count == 0 || kiiIds.Any(string.IsNullOrWhiteSpace) || kiiIds.Distinct().Count() != kiiIds.Count)
            throw new ArgumentException("Отметьте хотя бы один КИИ без повторяющихся идентификаторов.", nameof(kiiIds));
        var selectedIds = kiiIds.ToHashSet();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(document);
        document.Options.Validate();
        var choices = selections?.ToDictionary(s => s.Id);
        if (choices is not null && choices.Keys.Any(id => !document.Buildings.Any(b => b.Id == id)))
            throw new ArgumentException("Выбрано здание, отсутствующее в исходном импорте.");
        var included = document.Buildings.Where(b => choices is null || !choices.TryGetValue(b.Id, out var choice) || choice.Included).ToArray();
        if (kiiIds.Any(id => !included.Any(b => b.Id == id)))
            throw new ArgumentException("Каждый КИИ должен быть включён в сцену и присутствовать в импорте.", nameof(kiiIds));
        var kii = included.Single(b => b.Id == kiiIds[0]);
        bool threeD = document.Options.Mode == SceneMode.ThreeD;
        PolygonPrism3D Geometry(OsmBuilding b)
        {
            double top = choices is not null && choices.TryGetValue(b.Id, out var choice) ? choice.TopHeight : b.Height.Top;
            if (threeD && (!double.IsFinite(top) || top <= b.Height.Base || top > 1000))
                throw new ArgumentException($"{b.Id}: высота должна превышать отметку основания и быть не более 1000 м.");
            return new PolygonPrism3D(b.Footprint, threeD ? b.Height.Base : 0, threeD ? top - b.Height.Base : 0);
        }
        var geometries = included.ToDictionary(b => b.Id, Geometry);
        double sceneHeight = threeD ? geometries.Values.Max(g => g.BaseElevation + g.Height) : 0;
        _scene = new Scene("osm-" + Guid.NewGuid().ToString("N"), document.Options.Mode,
            new Box3D(new Point3D(0, 0, 0), new Size3D(document.Options.Length, document.Options.Width, sceneHeight)),
            new SceneObject(kii.Id, SceneObjectType.Kii, geometries[kii.Id]));
        foreach (string id in kiiIds.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _scene.AddKii(new SceneObject(id, SceneObjectType.Kii, geometries[id]));
        }
        var skipped = new List<OsmSkippedBuilding>();
        foreach (var building in included.Where(b => !selectedIds.Contains(b.Id)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { _scene.AddObstacle(new SceneObject(building.Id, SceneObjectType.Obstacle, geometries[building.Id])); }
            catch (ArgumentException error)
            {

                skipped.Add(new OsmSkippedBuilding(building.Id, error.Message));
            }
        }
        SkippedBuildings = skipped.AsReadOnly();

        if (threeD)
        {
            double acceptedHeight = _scene.Objects.Max(o => o.Geometry.BaseElevation + o.Geometry.Height);
            if (acceptedHeight != sceneHeight)
            {
                SceneObject[] accepted = _scene.Objects.ToArray();
                _scene = new Scene(_scene.Id, _scene.Mode,
                    new Box3D(new Point3D(0, 0, 0), new Size3D(document.Options.Length, document.Options.Width, acceptedHeight)), accepted[0]);
                foreach (var obstacle in accepted.Skip(1))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (obstacle.Type == SceneObjectType.Kii) _scene.AddKii(obstacle);
                    else _scene.AddObstacle(obstacle);
                }
            }
        }
    }

    /// <summary>Возвращает последовательность из одной уже построенной сцены OSM.</summary>
    public IEnumerable<Scene> GetScenes() { yield return _scene; }
}
