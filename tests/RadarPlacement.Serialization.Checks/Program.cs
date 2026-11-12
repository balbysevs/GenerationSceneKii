using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RadarPlacement.Core;
using RadarPlacement.Core.Results;
using RadarPlacement.Serialization;

// Исполняемый проект автономных проверок. Это не пользовательское консольное приложение.
// Все проверки используют настоящий JSON экспортёра, сохранённый в Fixtures, и его мутации.
var checks = new (string Name, Action Run)[]
{
    ("Совместимость с настоящим экспортом первой команды", CheckGeneratorFixture),
    ("Сцена без помех и оба направления обхода прямоугольника", CheckRectangleOrder),
    ("Отказ от 3D, другой версии и нескольких КИИ", CheckUnsupportedScenes),
    ("Обязательные поля и типы значений", CheckRequiredFields),
    ("Система координат, плоская земля и область наблюдения", CheckConventions),
    ("Правила установки: ссылка на КИИ, поверхность, разрешённые позиции", CheckPlacementRules),
    ("Геометрия: диагонали, поворот, отверстия и повторные вершины", CheckInvalidGeometry),
    ("Объекты: границы, пересечения, идентификаторы и порядок", CheckInvalidObjects),
    ("Высоты 2D и неограниченные числа не принимаются молча", CheckNumbers),
    ("Повреждённый JSON, повторные ключи и строгий синтаксис", CheckSyntax),
    ("Ограничения размера и глубины документа", CheckReadLimits),
    ("Настройки: обязательные поля и значения по умолчанию", CheckDefaultSettings),
    ("Настройки: границы ТЗ и ошибки с путями", CheckSettingsRanges),
    ("Настройки: неизвестные имена, null и некорректные лимиты", CheckSettingsStructure),
    ("Настройки: полный цикл записи и чтения, культура ru-RU", CheckSettingsRoundTrip),
    ("Результат: сохранность исходной сцены и дополнительных полей", CheckResultPreservation),
    ("Результат: координаты, ссылки, настройки и показатели", CheckResultContract),
    ("Результат: все биты общей и индивидуальных карт", CheckGridExport),
    ("Результат: пустая сетка и null вместо неопределённого процента", CheckEmptyCoverage),
    ("Результат: дубли позиций и уникальные идентификаторы", CheckRadarIdentifiers),
    ("Результат: защита от чужой сцены и повторного импорта результата", CheckResultAssociation),
    ("Файлы: чтение UTF-8 и BOM синхронно и асинхронно", CheckFileReading),
    ("Файлы: повреждённый UTF-8, отсутствие файла, размер", CheckFileErrors),
    ("Файлы: сохранение, явная перезапись, очистка временных файлов", CheckFileSaving),
    ("Отмена чтения, записи и экспорта без изменения готового файла", CheckCancellation)
};

int failed = 0;
foreach (var (name, run) in checks)
{
    try { run(); Console.WriteLine("PASS: " + name); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL: {name}\n{exception}"); }
}
Console.WriteLine($"Проверок: {checks.Length}; успешно: {checks.Length - failed}; ошибок: {failed}.");
return failed == 0 ? 0 : 1;

static string Fixture() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "generated-2d.json"));
static JsonObject SceneNode() => JsonNode.Parse(Fixture())!.AsObject();
static JsonArray Objects(JsonObject root) => root["objects"]!.AsArray();
static JsonObject Geometry(JsonObject root, int index = 0) => Objects(root)[index]!["geometry"]!.AsObject();
static JsonArray Outer(JsonObject root, int index = 0) => Geometry(root, index)["footprint"]!["outer"]!.AsArray();
static JsonObject Zone(JsonObject root) => root["radarPlacementZones"]![0]!.AsObject();
static string Changed(Action<JsonObject> change)
{
    var root = SceneNode();
    change(root);
    return root.ToJsonString();
}
static SceneDocument2D Read(string? json = null) => new SceneJsonReader2D().Parse(json ?? Fixture());
static SolverSettings2D Settings(int radarCount = 2) => new()
{
    Radars = new() { RadarCount = radarCount, Radius = 3000 },
    Discretization = new() { CandidateStep = 500, CoverageStep = 1000 }
};
static RadarPlacementResult2D Solve(SceneDocument2D source, SolverSettings2D? settings = null)
{
    settings ??= Settings();
    return new RadarPlacementSolver2D().Solve(source.Scene, settings.Radars, settings.Discretization);
}
static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
static T Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T exception) { return exception; }
    throw new InvalidOperationException($"Ожидалось {typeof(T).Name}.");
}
static void BadScene(Action<JsonObject> change, string path) =>
    Require(Throws<RadarJsonException>(() => Read(Changed(change))).JsonPath == path, "Неверный путь ошибки: " + path);
static string SettingsJson() => """{"schemaVersion":"1.0","mode":"2D","radarCount":2,"radius":3000}""";
static void BadSettings(string name, JsonNode? value, string? expectedPath = null)
{
    var root = JsonNode.Parse(SettingsJson())!.AsObject();
    root[name] = value;
    var error = Throws<RadarJsonException>(() => new SolverSettingsJson().Parse(root.ToJsonString()));
    Require(error.JsonPath == (expectedPath ?? "$." + name), "Неверный путь ошибки настроек.");
}

static void CheckGeneratorFixture()
{
    var source = Read();
    Require(source.SceneId == "generator-compatibility-2d" && source.KiiObjectId == "kii-001", "Потеряны идентификаторы.");
    Require(source.Scene.Bounds.Width == 10000 && source.Scene.CriticalInfrastructure.Width == 1000, "Неверные размеры.");
    Require(source.Scene.Obstacles.Count == 2 && source.Scene.Obstacles[1].Height == 80, "Неверные помехи.");
    // OriginalJson остаётся доступен после освобождения внутреннего JsonDocument.
    Require(source.OriginalJson.GetProperty("source").GetProperty("seed").GetInt32() == 123, "Потеряны метаданные.");
}

static void CheckRectangleOrder()
{
    var root = SceneNode();
    var points = Outer(root).Select(p => p!.DeepClone()).Reverse().ToArray();
    Geometry(root)["footprint"]!["outer"] = new JsonArray(points.Skip(1).Concat(points.Take(1)).ToArray());
    while (Objects(root).Count > 1) Objects(root).RemoveAt(1);
    Require(Read(root.ToJsonString()).Scene.Obstacles.Count == 0, "Допустимая сцена без помех отклонена.");
}

static void CheckUnsupportedScenes()
{
    BadScene(r => r["mode"] = "3D", "$.mode");
    BadScene(r => r["schemaVersion"] = "1.1", "$.schemaVersion");
    BadScene(r => Objects(r)[1]!["type"] = "kii", "$.objects[1].type");
}

static void CheckRequiredFields()
{
    foreach (string name in new[] { "sceneId", "mode", "schemaVersion", "bounds", "objects", "coordinateSystem", "source", "terrain", "observationArea", "radarPlacementZones" })
        BadScene(r => r.Remove(name), "$." + name);
    BadScene(r => Geometry(r).Remove("height"), "$.objects[0].geometry.height");
    BadScene(r => r["objects"] = null, "$.objects");
    BadScene(r => r["objects"] = new JsonArray(), "$.objects");
    BadScene(r => r["sceneId"] = " ", "$.sceneId");
    BadScene(r => r["bounds"]!["min"]!["x"] = "0", "$.bounds.min.x");
    Throws<RadarJsonException>(() => Read("[]"));
    Throws<RadarJsonException>(() => Read("null"));
}

static void CheckConventions()
{
    BadScene(r => r["coordinateSystem"]!["units"] = "kilometers", "$.coordinateSystem.units");
    BadScene(r => r["coordinateSystem"]!["yAxis"] = "south", "$.coordinateSystem.yAxis");
    BadScene(r => r["terrain"]!["type"] = "heightMap", "$.terrain.type");
    BadScene(r => r["observationArea"]!["excludeObjectFootprints"] = false, "$.observationArea.excludeObjectFootprints");
    BadScene(r => r["observationArea"]!["type"] = "airVolume", "$.observationArea.type");
    BadScene(r => r["source"]!["type"] = "unknown", "$.source.type");
}

static void CheckPlacementRules()
{
    BadScene(r => Zone(r)["objectId"] = "missing", "$.radarPlacementZones[0].objectId");
    BadScene(r => Zone(r)["surface"] = "ground", "$.radarPlacementZones[0].surface");
    BadScene(r => Zone(r)["allowedPositions"] = new JsonArray("center"), "$.radarPlacementZones[0].allowedPositions");
    BadScene(r => Zone(r)["allowedPositions"] = new JsonArray("outerBoundary", "center", "interior"), "$.radarPlacementZones[0].allowedPositions");
    BadScene(r => r["radarPlacementZones"] = new JsonArray(), "$.radarPlacementZones");
    Read(Changed(r => Zone(r)["allowedPositions"] = new JsonArray("center", "outerBoundary")));
}

static void CheckInvalidGeometry()
{
    const string path = "$.objects[0].geometry.footprint.outer";
    BadScene(r => Outer(r)[0]!["x"] = 4700, path);
    BadScene(r => Geometry(r)["footprint"]!["holes"] = JsonNode.Parse("[[{\"x\":4700,\"y\":4700}]]"), "$.objects[0].geometry.footprint.holes");
    BadScene(r => Outer(r).Add(Outer(r)[0]!.DeepClone()), path);
    BadScene(r => Outer(r)[1] = Outer(r)[0]!.DeepClone(), path);
    BadScene(r =>
    {
        var a = Outer(r)[1]!.DeepClone();
        Outer(r)[1] = Outer(r)[2]!.DeepClone();
        Outer(r)[2] = a;
    }, path);
    BadScene(r => Geometry(r)["type"] = "sphere", "$.objects[0].geometry.type");
}

static void CheckInvalidObjects()
{
    BadScene(r => Objects(r)[1]!["id"] = "kii-001", "$.objects[1].id");
    BadScene(r => Objects(r)[0]!["type"] = "obstacle", "$.objects[0].type");
    BadScene(r => Objects(r)[1]!["geometry"] = Geometry(r).DeepClone(), "$.objects[1].geometry");
    BadScene(r => r["bounds"]!["max"]!["x"] = 6000, "$.objects[1].geometry");
    BadScene(r => Objects(r)[2]!["geometry"] = Geometry(r, 1).DeepClone(), "$.objects[2].geometry");
}

static void CheckNumbers()
{
    BadScene(r => Geometry(r)["height"] = 3, "$.objects[0].geometry.height");
    BadScene(r => Geometry(r)["baseElevation"] = 1, "$.objects[0].geometry.baseElevation");
    BadScene(r => r["bounds"]!["max"]!["z"] = 10, "$.bounds.max.z");
    BadScene(r => r["terrain"]!["elevation"] = 1, "$.terrain.elevation");
    var overflow = Fixture().Replace("10000", "1e999", StringComparison.Ordinal);
    Require(Throws<RadarJsonException>(() => Read(overflow)).JsonPath == "$.bounds.max.x", "Переполнение должно проверяться.");
}

static void CheckSyntax()
{
    Throws<RadarJsonException>(() => Read("{"));
    Throws<RadarJsonException>(() => Read(Fixture() + " {}"));
    Throws<RadarJsonException>(() => Read(Fixture().Replace("\"mode\": \"2D\"", "\"mode\": \"2D\",\"mode\":\"3D\"", StringComparison.Ordinal)));
    Throws<RadarJsonException>(() => Read("/*comment*/" + Fixture()));
    Throws<RadarJsonException>(() => Read(Changed(r => r["extension"] = JsonNode.Parse("{\"valid\":1}"))
        .Replace("\"valid\":1", "\"valid\":1,\"valid\":2", StringComparison.Ordinal)));
}

static void CheckReadLimits()
{
    var reader = new SceneJsonReader2D();
    Throws<RadarJsonException>(() => reader.Parse(Fixture(), new() { MaxBytes = 100 }));
    Throws<RadarJsonException>(() => reader.Parse(Fixture(), new() { MaxDepth = 2 }));
    Throws<ArgumentOutOfRangeException>(() => reader.Parse(Fixture(), new() { MaxDepth = 0 }));
    Throws<ArgumentOutOfRangeException>(() => reader.Parse(Fixture(), new() { MaxBytes = 0 }));
    var unicode = Changed(r => r["note"] = "Привет");
    reader.Parse(unicode, new() { MaxBytes = Encoding.UTF8.GetByteCount(unicode) });
    Throws<RadarJsonException>(() => reader.Parse(unicode, new() { MaxBytes = Encoding.UTF8.GetByteCount(unicode) - 1 }));
}

static void CheckDefaultSettings()
{
    var parsed = new SolverSettingsJson().Parse(SettingsJson());
    Require(parsed.Radars.RadarCount == 2 && parsed.Discretization.CandidateStep == 100, "Неверные значения по умолчанию.");
    Require(parsed.Discretization.MaxBitsetBytes == 256L * 1024 * 1024, "Потерян лимит памяти.");
    foreach (string name in new[] { "schemaVersion", "mode", "radarCount", "radius" })
    {
        var root = JsonNode.Parse(SettingsJson())!.AsObject();
        root.Remove(name);
        Require(Throws<RadarJsonException>(() => new SolverSettingsJson().Parse(root.ToJsonString())).JsonPath == "$." + name,
            "Не проверено обязательное поле настроек.");
    }
}

static void CheckSettingsRanges()
{
    foreach (int count in new[] { 0, -1, 11 }) BadSettings("radarCount", JsonValue.Create(count));
    BadSettings("radarCount", JsonValue.Create(2.5));
    foreach (double radius in new[] { 0, 2999.99, 7000.01 }) BadSettings("radius", JsonValue.Create(radius));
    foreach (int radius in new[] { 3000, 7000 })
        foreach (int count in new[] { 1, 10 })
            SolverSettingsJson.Validate(new() { Radars = new() { RadarCount = count, Radius = radius } });
    Throws<RadarJsonException>(() => new SolverSettingsJson().Serialize(new() { Radars = new() { Radius = double.NaN } }));
}

static void CheckSettingsStructure()
{
    BadSettings("radarcount", JsonValue.Create(2));
    BadSettings("radius", JsonValue.Create("3000"));
    BadSettings("discretization", null);
    foreach (string name in new[] { "candidateStep", "coverageStep", "maxCandidatePositions", "maxGridCells", "maxBitsetBytes" })
        BadSettings("discretization", new JsonObject { [name] = 0 }, "$.discretization." + name);
    BadSettings("discretization", new JsonObject { ["coverageStepp"] = 500 }, "$.discretization.coverageStepp");
    BadSettings("discretization", new JsonObject { ["maxGridCells"] = (long)int.MaxValue + 1 }, "$.discretization.maxGridCells");
}

static void CheckSettingsRoundTrip()
{
    var codec = new SolverSettingsJson();
    var settings = Settings() with { Discretization = Settings().Discretization with { CandidateStep = 12.5, MaxBitsetBytes = 5_000_000_000 } };
    var previous = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        string json = codec.Serialize(settings);
        Require(json.Contains("12.5", StringComparison.Ordinal), "Дробь должна иметь точку независимо от культуры.");
        Require(codec.Parse(json) == settings && codec.Parse(codec.Serialize(settings, false)) == settings, "Настройки изменились при round-trip.");
    }
    finally { CultureInfo.CurrentCulture = previous; }
}

static void CheckResultPreservation()
{
    var source = Read(Changed(r =>
    {
        r["note"] = "Учебная сцена";
        r["extension"] = JsonNode.Parse("{\"nested\":[true,null,123]}");
        Objects(r)[0]!["label"] = "Здание КИИ";
    }));
    string originalBefore = source.OriginalJson.GetRawText();
    using var written = JsonDocument.Parse(new ResultJsonWriter2D().Serialize(source, Solve(source)));
    foreach (var property in source.OriginalJson.EnumerateObject())
        Require(JsonElement.DeepEquals(property.Value, written.RootElement.GetProperty(property.Name)), "Изменено исходное поле: " + property.Name);
    Require(source.OriginalJson.GetRawText() == originalBefore, "Исходный документ мутировал.");
}

static void CheckResultContract()
{
    var source = Read();
    var result = Solve(source);
    string json = new ResultJsonWriter2D().Serialize(source, result);
    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;
    var radars = root.GetProperty("radars");
    Require(radars.GetArrayLength() == result.Radars.Count, "Неверное количество радаров.");
    for (int i = 0; i < radars.GetArrayLength(); i++)
    {
        var r = radars[i];
        Require(r.GetProperty("objectId").GetString() == source.KiiObjectId, "Потеряна связь с КИИ.");
        Require(r.GetProperty("position").GetProperty("x").GetDouble() == result.Radars[i].Position.X, "Неверная координата.");
        Require(r.GetProperty("position").GetProperty("z").GetDouble() == 0, "2D должна иметь Z=0.");
        Require(r.GetProperty("radius").GetDouble() == 3000, "Неверный радиус.");
    }
    var solver = root.GetProperty("solver");
    Require(solver.GetProperty("globallyOptimalForDiscreteModel").GetBoolean(), "Нет признака оптимальности.");
    Require(!solver.TryGetProperty("coverageGrid", out _), "Большая карта должна быть опциональной.");
    Require(solver.GetProperty("coverage").GetProperty("coveredCellCount").GetInt32() == result.Coverage.CoveredCellCount, "Неверные показатели.");
    Require(new SolverSettingsJson().Parse(solver.GetProperty("settings").GetRawText()) == Settings(), "Настройки нельзя воспроизвести.");
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "sample-result-2d.json"), json + Environment.NewLine, new UTF8Encoding(false));
}

static void CheckGridExport()
{
    var source = Read();
    var result = Solve(source);
    using var document = JsonDocument.Parse(new ResultJsonWriter2D().Serialize(source, result, new() { IncludeCoverageGrid = true, WriteIndented = false }));
    var root = document.RootElement;
    var grid = root.GetProperty("solver").GetProperty("coverageGrid");
    Require(grid.GetProperty("encoding").GetString() == "base64-lsb0", "Неверная кодировка.");
    var excluded = grid.GetProperty("excludedMask").GetBytesFromBase64();
    var covered = grid.GetProperty("coveredMask").GetBytesFromBase64();
    var individual = grid.GetProperty("radarMasks").EnumerateArray().Select(r => r.GetProperty("coveredMask").GetBytesFromBase64()).ToArray();
    int total = result.Coverage.Columns * result.Coverage.Rows;
    Require(covered.Length == (total + 7) / 8, "Неверная длина маски.");
    for (int i = 0; i < covered.Length * 8; i++)
    {
        bool Bit(byte[] mask) => (mask[i / 8] & (1 << (i % 8))) != 0;
        if (i >= total)
        {
            Require(!Bit(excluded) && !Bit(covered) && individual.All(m => !Bit(m)), "Хвостовые биты должны быть нулевыми.");
            continue;
        }
        int x = i % result.Coverage.Columns, y = i / result.Coverage.Columns;
        var state = result.Coverage.GetCellState(x, y);
        Require(Bit(excluded) == (state == CoverageCellState2D.Excluded) && Bit(covered) == (state == CoverageCellState2D.Covered), "Повреждена общая карта.");
        for (int radar = 0; radar < individual.Length; radar++)
            Require(Bit(individual[radar]) == result.Coverage.IsCoveredByRadar(radar, x, y), "Повреждена карта радара.");
    }
    for (int radar = 0; radar < individual.Length; radar++)
        Require(grid.GetProperty("radarMasks")[radar].GetProperty("radarId").GetString() == root.GetProperty("radars")[radar].GetProperty("id").GetString(), "Перепутаны идентификаторы масок.");
}

static void CheckEmptyCoverage()
{
    var source = Read();
    var settings = Settings() with { Discretization = Settings().Discretization with { CoverageStep = 20000 } };
    var result = Solve(source, settings);
    using var document = JsonDocument.Parse(new ResultJsonWriter2D().Serialize(source, result, new() { IncludeCoverageGrid = true }));
    Require(document.RootElement.GetProperty("solver").GetProperty("coverage").GetProperty("coveragePercent").ValueKind == JsonValueKind.Null,
        "Неопределённый процент должен быть null.");
}

static void CheckRadarIdentifiers()
{
    var source = Read(Changed(r => Objects(r)[1]!["id"] = "radar-001"));
    var settings = Settings(10) with { Discretization = Settings().Discretization with { CandidateStep = 10000 } };
    var result = Solve(source, settings);
    using var document = JsonDocument.Parse(new ResultJsonWriter2D().Serialize(source, result));
    var ids = document.RootElement.GetProperty("radars").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToArray();
    Require(ids.Length == 10 && ids.Distinct().Count() == 10 && !ids.Contains("radar-001"), "Идентификаторы должны быть уникальны даже при одинаковых координатах.");
}

static void CheckResultAssociation()
{
    var first = Read();
    var second = Read();
    var writer = new ResultJsonWriter2D();
    Throws<ArgumentException>(() => writer.Serialize(first, Solve(second)));
    Throws<RadarJsonException>(() => Read(writer.Serialize(first, Solve(first))));
    BadScene(r => r["solver"] = new JsonObject(), "$");
    BadScene(r => r["radars"] = new JsonArray(), "$");
}

static void CheckFileReading()
{
    using var temp = new TestDirectory();
    string path = Path.Combine(temp.Path, "сцена.json");
    File.WriteAllText(path, Fixture(), new UTF8Encoding(true));
    var reader = new SceneJsonReader2D();
    Require(reader.Load(path).SceneId == reader.LoadAsync(path).GetAwaiter().GetResult().SceneId, "Результат синхронного/асинхронного чтения различается.");
    Require(reader.Parse("\uFEFF" + Fixture()).SceneId == "generator-compatibility-2d", "BOM строки должен приниматься.");
}

static void CheckFileErrors()
{
    using var temp = new TestDirectory();
    string path = Path.Combine(temp.Path, "broken.json");
    var reader = new SceneJsonReader2D();
    Throws<FileNotFoundException>(() => reader.Load(path));
    File.WriteAllBytes(path, [0xFF, 0xFE, 0xFF]);
    Throws<RadarJsonException>(() => reader.Load(path));
    File.WriteAllText(path, Fixture());
    Throws<RadarJsonException>(() => reader.Load(path, new() { MaxBytes = 100 }));
}

static void CheckFileSaving()
{
    using var temp = new TestDirectory();
    string path = Path.Combine(temp.Path, "nested", "settings.json");
    var codec = new SolverSettingsJson();
    codec.Save(Settings(), path);
    Require(codec.Load(path) == Settings(), "Настройки не восстановились из файла.");
    string before = File.ReadAllText(path);
    Throws<IOException>(() => codec.Save(Settings(3), path));
    Require(File.ReadAllText(path) == before, "Существующий файл был изменён без overwrite.");
    codec.SaveAsync(Settings(3), path, overwrite: true).GetAwaiter().GetResult();
    Require(codec.LoadAsync(path).GetAwaiter().GetResult() == Settings(3), "Явная перезапись не выполнена.");
    var source = Read();
    var result = Solve(source);
    string resultPath = Path.Combine(temp.Path, "result.json");
    var writer = new ResultJsonWriter2D();
    writer.Save(source, result, resultPath);
    Throws<IOException>(() => writer.Save(source, result, resultPath));
    writer.SaveAsync(source, result, resultPath, new() { IncludeCoverageGrid = true }, overwrite: true).GetAwaiter().GetResult();
    Require(!Directory.EnumerateFiles(temp.Path, "*.tmp", SearchOption.AllDirectories).Any(), "Остались временные файлы.");
    byte[] bytes = File.ReadAllBytes(resultPath);
    Require(!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "Запись должна быть UTF-8 без BOM.");
}

static void CheckCancellation()
{
    using var temp = new TestDirectory();
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    string path = Path.Combine(temp.Path, "unchanged.json");
    File.WriteAllText(path, "unchanged");
    Throws<OperationCanceledException>(() => new SceneJsonReader2D().LoadAsync(path, cancellationToken: cancellation.Token).GetAwaiter().GetResult());
    Throws<OperationCanceledException>(() => new SolverSettingsJson().SaveAsync(Settings(), path, true, cancellation.Token).GetAwaiter().GetResult());
    var source = Read();
    var result = Solve(source);
    Throws<OperationCanceledException>(() => new ResultJsonWriter2D().SaveAsync(source, result, path, overwrite: true, cancellationToken: cancellation.Token).GetAwaiter().GetResult());
    Require(File.ReadAllText(path) == "unchanged", "Отмена повредила существующий файл.");
    Require(!Directory.EnumerateFiles(temp.Path, "*.tmp").Any(), "Отмена оставила временный файл.");
}

internal sealed class TestDirectory : IDisposable
{
    private readonly string _root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "TestArtifacts"));
    public string Path { get; }
    public TestDirectory()
    {
        Path = System.IO.Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        // Удаляем только уникальную папку, созданную этим тестом внутри собственного каталога.
        string fullPath = System.IO.Path.GetFullPath(Path);
        if (!fullPath.StartsWith(_root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Путь очистки вышел за пределы тестового каталога.");
        Directory.Delete(fullPath, recursive: true);
    }
}
