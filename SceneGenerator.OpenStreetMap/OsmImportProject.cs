using System.Text.Json;
using System.Text.Json.Serialization;

namespace SceneGenerator.OpenStreetMap;


public sealed record OsmImportProject
{
    public string Format { get; init; } = "scene-generator-osm-project-v1";
    public required OsmImportOptions Options { get; init; }
    public required string RawOverpassJson { get; init; }
    public string Source { get; init; } = "";
    public DateTimeOffset LoadedAt { get; init; }
    public string? KiiId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? KiiIds { get; init; }
    [JsonIgnore]
    public IReadOnlyList<string> EffectiveKiiIds => KiiIds ?? (KiiId is null ? [] : new[] { KiiId });
    public IReadOnlyList<OsmBuildingSelection> Selections { get; init; } = [];
    public IReadOnlyList<string> Issues { get; init; } = [];
    public string Projection { get; init; } = "WGS84 ECEF to ENU; geographic origin=(latitude,longitude,0); x=east+length/2; y=north+width/2; terrain=flat; up discarded";
    public string Attribution { get; init; } = "© OpenStreetMap contributors, ODbL; https://www.openstreetmap.org/copyright";
    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static OsmImportProject? TryRead(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("format", out var format)) return null;
        if (format.GetString() is not ("scene-generator-osm-project-v1" or "scene-generator-osm-project-v2")) throw new FormatException("Неизвестная версия проекта импорта OSM.");
        var project = JsonSerializer.Deserialize<OsmImportProject>(json, JsonOptions) ?? throw new FormatException("Пустой проект импорта.");
        if (project.Options is null || project.RawOverpassJson is null || project.Selections is null || project.Issues is null)
            throw new FormatException("Неполный проект импорта OSM.");
        project.Options.Validate();
        if (project.Selections.Any(s => s is null || string.IsNullOrWhiteSpace(s.Id) || !double.IsFinite(s.TopHeight) || s.TopHeight <= 0 || s.TopHeight > 1000) ||
            project.Selections.Select(s => s.Id).Distinct().Count() != project.Selections.Count)
            throw new FormatException("Некорректные высоты или повторяющиеся здания в проекте импорта.");
        if ((project.Format == "scene-generator-osm-project-v2" && (project.KiiIds is null || project.KiiIds.Count == 0)) ||
            (project.Format == "scene-generator-osm-project-v1" && project.KiiIds is not null) ||
            project.EffectiveKiiIds.Any(string.IsNullOrWhiteSpace) ||
            project.EffectiveKiiIds.Distinct().Count() != project.EffectiveKiiIds.Count ||
            (project.KiiIds is not null && project.KiiId is not null) ||
            project.EffectiveKiiIds.Any(id => project.Selections.Any(s => s.Id == id && !s.Included)))
            throw new FormatException("Некорректный список КИИ в проекте импорта.");
        return project;
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        Options.Validate();
        string json = JsonSerializer.Serialize(this, JsonOptions);
        TryRead(json);
        string fullPath = Path.GetFullPath(path);
        string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json, cancellationToken);
            File.Move(temporary, fullPath, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
