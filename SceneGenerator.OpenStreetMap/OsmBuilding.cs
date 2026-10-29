using SceneGenerator.Core.Geometry;

namespace SceneGenerator.OpenStreetMap;

public enum HeightOrigin { OsmHeight, OsmLevels, Default, Manual }

public sealed record BuildingHeight(double Top, double Base, HeightOrigin Origin);

public sealed record OsmBuilding(string Id, string Name, Polygon2D Footprint, BuildingHeight Height);

public sealed record OsmImportDocument(OsmImportOptions Options, IReadOnlyList<OsmBuilding> Buildings,
    IReadOnlyList<string> Issues, string RawJson, string Source, DateTimeOffset LoadedAt, OsmImportProject? SavedProject = null);

public sealed record OsmData(string Json, string Source, DateTimeOffset LoadedAt, OsmImportProject? SavedProject = null);
