using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.OpenStreetMap;

public sealed record OsmBuildingSelection(string Id, double TopHeight, bool Included = true, bool HeightEdited = false);

public sealed record OsmSkippedBuilding(string Id, string Reason);

/// <summary>An adapter from a reviewed OSM import to the same source contract as random generation.</summary>
public sealed class OsmSceneSource : ISceneSource
{
    private readonly Scene _scene;
    public IReadOnlyList<OsmSkippedBuilding> SkippedBuildings { get; }

    public OsmSceneSource(OsmImportDocument document, string kiiId,
        IReadOnlyList<OsmBuildingSelection>? selections = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(document);
        document.Options.Validate();
        var choices = selections?.ToDictionary(s => s.Id);
        if (choices is not null && choices.Keys.Any(id => !document.Buildings.Any(b => b.Id == id)))
            throw new ArgumentException("Выбрано здание, отсутствующее в исходном импорте.");
        var included = document.Buildings.Where(b => choices is null || !choices.TryGetValue(b.Id, out var choice) || choice.Included).ToArray();
        var kii = included.SingleOrDefault(b => b.Id == kiiId)
            ?? throw new ArgumentException("Выберите включённое в сцену здание КИИ.", nameof(kiiId));
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
        var skipped = new List<OsmSkippedBuilding>();
        foreach (var building in included.Where(b => b.Id != kii.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { _scene.AddObstacle(new SceneObject(building.Id, SceneObjectType.Obstacle, geometries[building.Id])); }
            catch (ArgumentException error)
            {
                // Scene validates before adding: a rejected obstacle leaves the accepted objects intact.
                skipped.Add(new OsmSkippedBuilding(building.Id, error.Message));
            }
        }
        SkippedBuildings = skipped.AsReadOnly();
        // A skipped tall building must not enlarge the final scene's vertical bounds.
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
                    _scene.AddObstacle(obstacle);
                }
            }
        }
    }

    public IEnumerable<Scene> GetScenes() { yield return _scene; }
}
