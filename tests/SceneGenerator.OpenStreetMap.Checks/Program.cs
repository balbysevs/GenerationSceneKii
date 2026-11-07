using System.Globalization;
using System.Net;
using System.Text.Json;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Models;
using SceneGenerator.OpenStreetMap;
using SceneGenerator.Serialization;
using SceneGenerator.Serialization.Dto;

int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
void Throws<T>(Action action, string label) where T : Exception
{
    try { action(); } catch (T) { checks++; return; }
    throw new Exception("Expected exception: " + label);
}
async Task ThrowsAsync<T>(Func<Task> action, string label) where T : Exception
{
    try { await action(); } catch (T) { checks++; return; }
    throw new Exception("Expected exception: " + label);
}
object[] Ring(double south, double west, double north, double east) =>
    [new { lat = south, lon = west }, new { lat = south, lon = east }, new { lat = north, lon = east },
        new { lat = north, lon = west }, new { lat = south, lon = west }];
object Way(long id, object[] geometry, Dictionary<string,string>? tags = null) =>
    new { type = "way", id, geometry, tags = tags ?? new() { ["building"] = "yes", ["height"] = "18" } };
string Json(params object[] elements) => JsonSerializer.Serialize(new { version = 0.6, elements });
var options = new OsmImportOptions { Latitude = 0, Longitude = 0, Length = 1000, Width = 1000 };
var parser = new OsmBuildingParser();
OsmImportDocument Parse(string text, OsmImportOptions? settings = null) =>
    parser.Parse(new OsmData(text, "synthetic acceptance fixture", DateTimeOffset.UtcNow), settings ?? options);
var west = Ring(-.002, -.002, -.001, -.001);
var east = Ring(.001, .001, .002, .002);
var northWest = Ring(.001, -.002, .002, -.001);
var outer = Ring(-.002, .0005, 0, .0025);
var hole = Ring(-.0015, .001, -.0005, .002);
var relation = new
{
    type = "relation", id = 90,
    tags = new Dictionary<string,string> { ["type"] = "multipolygon", ["building"] = "yes", ["height"] = "24", ["name"] = "Здание с двором" },
    members = new object[]
    {
        new { type = "way", @ref = 50, role = "outer", geometry = outer[..3] },
        new { type = "way", @ref = 51, role = "outer", geometry = new[] { outer[0], outer[3], outer[2] } },
        new { type = "way", @ref = 54, role = "inner", geometry = hole }
    }
};
string fixture = Json(
    Way(1, west),
    Way(2, east, new() { ["building"] = "yes", ["building:levels"] = "5", ["roof:height"] = "2" }),
    Way(3, northWest, new() { ["building"] = "yes" }),
    Way(4, Ring(.02, .02, .021, .021)),
    Way(50, outer), relation);
var imported = Parse(fixture);
Check(imported.Buildings.Count == 4, "Four complete buildings, relation member not duplicated");
Check(imported.Issues.Any(i => i.Contains("way/4")), "Boundary exclusion is reported");
Check(imported.Buildings.Single(b => b.Id == "relation/90").Footprint.Holes.Count == 1, "Joined/reversed relation members and courtyard");
Check(imported.Buildings.Single(b => b.Id == "way/2").Height is { Top: 17, Origin: HeightOrigin.OsmLevels }, "Levels + roof estimate");
Check(imported.Buildings.Single(b => b.Id == "way/3").Height.Origin == HeightOrigin.Default, "Unknown height marked as default");

ISceneSource source = new OsmSceneSource(imported, "way/1");
var scene = source.GetScenes().Single();
Check(scene.Objects.Count == 4 && scene.Objects[0].Id == "way/1" && scene.Objects[0].Type == SceneObjectType.Kii, "Shared source contract, KII first");
Check(scene.Bounds.Size.Height == 24, "Imported scene height derives from actual objects, no 50 m clipping");
Check(scene.Objects.Single(b => b.Id == "relation/90").Geometry.Footprint.Holes.Count == 1, "Courtyard retained in Scene");
var choices = imported.Buildings.Select(b => new OsmBuildingSelection(b.Id, b.Id == "way/1" ? 75 : b.Height.Top, b.Id != "way/3")).ToArray();
var edited = new OsmSceneSource(imported, "way/1", choices).GetScenes().Single();
Check(edited.Objects.Count == 3 && edited.Bounds.Size.Height == 75, "Manual height and exclusions, no random-generator limits");
Throws<ArgumentException>(() => new OsmSceneSource(imported, "missing"), "Missing KII");
Throws<ArgumentException>(() => new OsmSceneSource(imported, "way/3", choices), "Excluded KII");
var twoD = new OsmSceneSource(imported with { Options = options with { Mode = SceneMode.TwoD } }, "way/1").GetScenes().Single();
Check(twoD.Bounds.Height == 0 && twoD.Objects.All(o => o.Geometry.Height == 0 && o.Geometry.BaseElevation == 0), "2D all elevations zero");
var touching = Parse(Json(Way(11, Ring(-.002, -.002, -.001, -.001)), Way(12, Ring(-.002, -.001, -.001, 0))));
Check(new OsmSceneSource(touching, "way/11").GetScenes().Single().Objects.Count == 2, "Touching buildings accepted without altering Core");
var overlap = Parse(Json(Way(11, west), Way(12, Ring(-.0019, -.0019, -.0011, -.0011))));
var filtered = new OsmSceneSource(overlap, "way/11");
Check(filtered.GetScenes().Single().Objects.Count == 1 && filtered.SkippedBuildings.Single().Id == "way/12", "Overlapping obstacle excluded automatically");
Check(filtered.GetScenes().Single().Objects[0].Id == "way/11" && filtered.SkippedBuildings[0].Reason.Length > 0, "KII preserved and exclusion reason recorded");
var swapped = new OsmSceneSource(overlap, "way/12");
Check(swapped.GetScenes().Single().Objects[0].Id == "way/12" && swapped.SkippedBuildings.Single().Id == "way/11", "Selected KII takes priority over input ordering");
var mixed = Parse(Json(Way(11, west), Way(12, west, new() { ["building"] = "yes", ["height"] = "300" }), Way(13, east)));
var mixedSource = new OsmSceneSource(mixed, "way/11");
Check(mixedSource.GetScenes().Single().Objects.Select(o => o.Id).SequenceEqual(new[] { "way/11", "way/13" }), "Remaining valid buildings added after a conflict");
Check(mixedSource.GetScenes().Single().Bounds.Height == 18, "Rejected tall building does not inflate scene height");
var obstacleConflict = Parse(Json(Way(11, west), Way(12, east), Way(13, east), Way(14, northWest)));
var obstacleFiltered = new OsmSceneSource(obstacleConflict, "way/11");
Check(obstacleFiltered.SkippedBuildings.Single().Id == "way/13" && obstacleFiltered.GetScenes().Single().Objects.Count == 3, "Conflicts between obstacles also skipped");
var filtered2D = new OsmSceneSource(mixed with { Options = options with { Mode = SceneMode.TwoD } }, "way/11");
Check(filtered2D.SkippedBuildings.Count == 1 && filtered2D.GetScenes().Single().Objects.All(o => o.Geometry.Height == 0), "Automatic filtering also works in 2D");
var raised = Parse(Json(Way(20, west, new() { ["building"] = "yes", ["min_height"] = "10", ["height"] = "18" })));
var raisedGeometry = new OsmSceneSource(raised, "way/20").GetScenes().Single().Objects[0].Geometry;
Check(raisedGeometry.BaseElevation == 10 && raisedGeometry.Height == 8, "OSM height includes min_height, no double counting");
Check(BuildingHeightResolver.TryMeters("30 ft", out double feet) && Math.Abs(feet - 9.144) < 1e-9, "Feet converted to metres");
Check(BuildingHeightResolver.TryMeters("12,5 m", out double metres) && metres == 12.5, "Decimal comma height");
Check(!BuildingHeightResolver.TryMeters("12;18", out _), "Ambiguous height rejected");
var invalid = Parse(Json(Way(30, west[..^1])));
Check(invalid.Buildings.Count == 0 && invalid.Issues.Count == 1, "Unclosed outline reported");
Throws<FormatException>(() => Parse("{\"elements\":[],\"remark\":\"runtime error\"}"), "Partial Overpass response rejected");
Throws<FormatException>(() => Parse("{\"objects\":[]}"), "Wrong input format");
Throws<ArgumentException>(() => (options with { Latitude = double.NaN }).Validate(), "Non-finite coordinates");
Throws<ArgumentException>(() => (options with { Length = 20001 }).Validate(), "Area limit");
Throws<ArgumentException>(() => (options with { Longitude = 180 }).Validate(), "Date-line crossing explicit");
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
Throws<OperationCanceledException>(() => parser.Parse(new OsmData(fixture,"test",DateTimeOffset.UtcNow), options, cancelled.Token), "Parser cancellation");
Throws<OperationCanceledException>(() => new OsmSceneSource(imported, "way/1", cancellationToken: cancelled.Token), "Scene build cancellation");

var projection = new LocalCoordinateProjector(options);
Throws<ArgumentException>(() => projection.Project(0, 180), "Antipodal coordinates cannot fold into local scene");
var center = projection.Project(0, 0);
Check(center.X == 500 && center.Y == 500, "Local origin at rectangle centre");
var east100 = projection.Project(0, 100 / 6378137d * 180 / Math.PI);
Check(Math.Abs(east100.X - 600) < 1e-6 && Math.Abs(east100.Y - 500) < 1e-8, "Equatorial 100 m east independent reference");
var north100 = projection.Project(100 / 6335439.32729282 * 180 / Math.PI, 0);
Check(Math.Abs(north100.Y - 600) < 1e-6 && Math.Abs(north100.X - 500) < 1e-8, "WGS84 100 m north reference");
var yaro = new LocalCoordinateProjector(options with { Latitude = 57.6261, Longitude = 39.8845 });
Check(yaro.Project(57.6261,39.8845) == center && yaro.Project(57.6261,39.8855).X > center.X &&
    yaro.Project(57.6271,39.8845).Y > center.Y, "Yaroslavl origin and axis directions");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
Check(OverpassDataProvider.CreateQuery(options).Contains("out body geom;") &&
    !OverpassDataProvider.CreateQuery(options).Contains("building:part"), "Invariant query and whole-building import");


var multi = new OsmSceneSource(imported, new[] { "way/2", "way/1" }).GetScenes().Single();
Check(multi.KiiObjects.Select(o => o.Id).SequenceEqual(new[] { "way/2", "way/1" }) &&
    multi.Objects.Take(2).All(o => o.Type == SceneObjectType.Kii) && multi.Objects.Skip(2).All(o => o.Type == SceneObjectType.Obstacle), "All KII precede obstacles in selected order");
Throws<ArgumentException>(() => new OsmSceneSource(imported, Array.Empty<string>()), "Empty KII list");
Throws<ArgumentException>(() => new OsmSceneSource(imported, new[] { "way/1", "way/1" }), "Duplicate KII list");
Throws<ArgumentException>(() => new OsmSceneSource(imported, new[] { "way/1", "missing" }), "Missing second KII");
Throws<ArgumentException>(() => new OsmSceneSource(imported, new[] { "way/1", "way/3" }, choices), "Excluded second KII");
Throws<ArgumentException>(() => new OsmSceneSource(overlap, new[] { "way/11", "way/12" }), "Selected overlapping KII cannot be silently dropped");
var multiPriority = new OsmSceneSource(obstacleConflict, new[] { "way/11", "way/13" });
Check(multiPriority.SkippedBuildings.Single().Id == "way/12" && multiPriority.GetScenes().Single().KiiObjects.Count == 2, "Second KII wins over earlier obstacle");
var multiHeight = new OsmSceneSource(mixed, new[] { "way/11", "way/13" }).GetScenes().Single();
Check(multiHeight.KiiObjects.Count == 2 && multiHeight.Bounds.Height == 18, "Rebuilding bounds retains every KII");
var multi2D = new OsmSceneSource(imported with { Options = options with { Mode = SceneMode.TwoD } }, new[] { "way/1", "way/2" }).GetScenes().Single();
Check(multi2D.KiiObjects.Count == 2 && multi2D.Objects.All(o => o.Geometry.Height == 0), "Multi KII in 2D");
var incremental = new Scene("incremental", scene.Mode, scene.Bounds, scene.Objects[0]);
incremental.AddObstacle(scene.Objects.Single(o => o.Id == "way/3"));
var secondKii = new SceneObject("second", SceneObjectType.Kii, scene.Objects.Single(o => o.Id == "way/2").Geometry);
incremental.AddKii(secondKii);
Check(incremental.KiiObjects.Count == 2 && incremental.Objects[1] == secondKii, "Adding KII after an obstacle preserves ordering");
Throws<ArgumentException>(() => incremental.AddKii(secondKii), "Duplicate KII id rejected atomically");
Throws<ArgumentException>(() => incremental.AddKii(new SceneObject("overlap", SceneObjectType.Kii, secondKii.Geometry)), "Core KII overlap rejected atomically");
Throws<ArgumentException>(() => incremental.AddKii(scene.Objects.Single(o => o.Id == "way/3")), "Wrong role rejected");
Check(incremental.KiiObjects.Count == 2 && incremental.Objects.Count == 3, "Failed additions leave scene intact");
var mapper = new SceneGenerator.Serialization.Mapping.SceneDtoMapper();
var multiDto = mapper.ToDto(multi, new SceneSourceDto { Type = "openstreetmap" });
Check(multiDto.SchemaVersion == "1.1" && multiDto.RadarPlacementZones.Count == 2 &&
    multiDto.RadarPlacementZones.Select(z => z.ObjectId).SequenceEqual(multi.KiiObjects.Select(o => o.Id)) &&
    multiDto.RadarPlacementZones.Select(z => z.Id).Distinct().Count() == 2, "Multi export contains distinct placement zones per KII");
var singleDto = mapper.ToDto(scene, new SceneSourceDto { Type = "openstreetmap" });
Check(singleDto.SchemaVersion == "1.0" && singleDto.RadarPlacementZones.Single().Id == "kii-placement-zone", "Single KII contract unchanged");

string temp = Path.Combine(Path.GetTempPath(), "osm-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string scenePath = Path.Combine(temp,"scene.json");
    new JsonSceneExporter(new SceneSourceDto { Type = "openstreetmap" }).Export(scene, scenePath);
    using var json = JsonDocument.Parse(File.ReadAllText(scenePath));
    Check(json.RootElement.GetProperty("source").GetProperty("type").GetString() == "openstreetmap", "Existing export tags OSM source");
    Check(!json.RootElement.GetProperty("source").TryGetProperty("seed", out _), "No invented seed for real territory");
    Check(json.RootElement.GetProperty("objects")[0].GetProperty("type").GetString() == "kii", "Same solver JSON and first KII");
    var project = new OsmImportProject { Options = options, RawOverpassJson = fixture, Source = "fixture",
        LoadedAt = DateTimeOffset.UtcNow, KiiId = "way/1", Selections = choices, Issues = imported.Issues };
    string projectPath = Path.Combine(temp,"territory.osm-project.json");
    await project.SaveAsync(projectPath);
    var reopened = await new OsmImportService(new FileOsmDataProvider(projectPath)).LoadAsync(new OsmImportOptions());
    Check(reopened.Options == options && reopened.SavedProject?.KiiId == "way/1", "Local project restores geographic area and KII");
    Check(reopened.SavedProject!.Selections.Single(s => s.Id == "way/1").TopHeight == 75, "Local project keeps height corrections");
    await ThrowsAsync<IOException>(() => project.SaveAsync(projectPath), "Project save refuses replacement");
    var multiProject = project with { Format = "scene-generator-osm-project-v2", KiiId = null, KiiIds = new[] { "way/1", "way/2" } };
    string multiPath = Path.Combine(temp, "multi.osm-project.json");
    await multiProject.SaveAsync(multiPath);
    var multiReopened = await new OsmImportService(new FileOsmDataProvider(multiPath)).LoadAsync(options);
    Check(multiReopened.SavedProject!.EffectiveKiiIds.SequenceEqual(new[] { "way/1", "way/2" }), "Multi project roundtrip");
    Check(new OsmSceneSource(multiReopened, multiReopened.SavedProject.EffectiveKiiIds, multiReopened.SavedProject.Selections).GetScenes().Single().KiiObjects.Count == 2, "Multi project restores usable scene");
    await ThrowsAsync<FormatException>(() => (multiProject with { KiiIds = new[] { "way/1", "way/1" } }).SaveAsync(Path.Combine(temp, "invalid.json")), "Invalid project list rejected before save");
    string multiScenePath = Path.Combine(temp, "multi-scene.json");
    new JsonSceneExporter(new SceneSourceDto { Type = "openstreetmap" }).Export(multi, multiScenePath);
    using var multiJson = JsonDocument.Parse(File.ReadAllText(multiScenePath));
    Check(multiJson.RootElement.GetProperty("schemaVersion").GetString() == "1.1" && multiJson.RootElement.GetProperty("radarPlacementZones").GetArrayLength() == 2, "Actual serialized JSON exports multiple zones");
    string rawPath = Path.Combine(temp,"overpass.json"); File.WriteAllText(rawPath, fixture);
    Check((await new OsmImportService(new FileOsmDataProvider(rawPath)).LoadAsync(options)).Buildings.Count == 4, "Offline raw response import");

    var handler = new ReplyHandler(fixture);
    using var client = new HttpClient(handler);
    var online = new OverpassDataProvider(client, Path.Combine(temp,"cache"));
    var result = await online.LoadAsync(options);
    await online.LoadAsync(options);
    Check(handler.Calls == 1 && handler.HadAgent && handler.Query!.Contains("data="), "Identified POST request and cache reuse");
    Check(result.Json == fixture && Directory.GetFiles(Path.Combine(temp,"cache"),"*.tmp").Length == 0, "Complete response cached atomically");
    await ThrowsAsync<OperationCanceledException>(() => online.LoadAsync(options, cancelled.Token), "Cached read cancellation");
    using var rateClient = new HttpClient(new ReplyHandler("busy", HttpStatusCode.TooManyRequests));
    await ThrowsAsync<HttpRequestException>(() => new OverpassDataProvider(rateClient).LoadAsync(options), "Rate limit handled without request storm");
    using var partialClient = new HttpClient(new ReplyHandler("{\"elements\":[],\"remark\":\"timeout\"}"));
    await ThrowsAsync<FormatException>(() => new OverpassDataProvider(partialClient).LoadAsync(options), "No partial response accepted or cached");

    if (args.Contains("--write-example"))
    {
        string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/examples/territory-demo.osm-project.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        if (File.Exists(output)) File.Delete(output);
        await project.SaveAsync(output);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!, "yaroslavl-overpass.ql"), OverpassDataProvider.CreateQuery(new OsmImportOptions()));
        Console.WriteLine("Synthetic offline example: " + output);
    }
}
finally { Directory.Delete(temp, true); }

if (args.Contains("--input"))
{
    string input = args[Array.IndexOf(args, "--input") + 1];
    string kiiId = args[Array.IndexOf(args, "--kii") + 1];
    var timer = System.Diagnostics.Stopwatch.StartNew();
    var actualDocument = await new OsmImportService(new FileOsmDataProvider(input)).LoadAsync(new OsmImportOptions());
    var actualSource = new OsmSceneSource(actualDocument, kiiId);
    var actualScene = actualSource.GetScenes().Single();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        acceptedObjects = actualScene.Objects.Count,
        kii = actualScene.Objects[0].Id,
        skipped = actualSource.SkippedBuildings.Select(b => b.Id).ToArray(),
        elapsedMilliseconds = timer.ElapsedMilliseconds
    }));
    Check(actualScene.Objects[0].Id == kiiId, "Real input preserves selected KII");
}

if (args.Contains("--live"))
{
    using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    var live = await new OsmImportService(new OverpassDataProvider(client, endpoint: args.Contains("--endpoint") ? new Uri(args[Array.IndexOf(args, "--endpoint") + 1]) : null)).LoadAsync(new OsmImportOptions { Length = 500, Width = 500 });
    Console.WriteLine($"Live Yaroslavl: {live.Buildings.Count} buildings, {live.Issues.Count} warnings.");
    if (live.Buildings.Count > 0)
    {
        var liveScene = new OsmSceneSource(live, live.Buildings[0].Id).GetScenes().Single();
        Console.WriteLine($"Live Scene via ISceneSource: {liveScene.Objects.Count} objects.");
    }
}
Console.WriteLine($"Passed {checks} OSM checks.");

sealed class ReplyHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool HadAgent { get; private set; }
    public string? Query { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        HadAgent = request.Headers.UserAgent.ToString().Contains("SceneGeneratorKii");
        Query = await request.Content!.ReadAsStringAsync(token);
        return new(status) { Content = new StringContent(json) };
    }
}
