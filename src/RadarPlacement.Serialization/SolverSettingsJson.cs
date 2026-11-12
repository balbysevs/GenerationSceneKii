using System.Text.Json;
using RadarPlacement.Core.Configuration;
using RadarPlacement.Serialization.Internal;

namespace RadarPlacement.Serialization;

/// <summary>
/// Чтение и запись собственного формата настроек решателя версии 1.0.
/// Имена полей чувствительны к регистру; неизвестные настройки отклоняются,
/// чтобы опечатка не привела к незаметному применению значения по умолчанию.
/// </summary>
public sealed class SolverSettingsJson
{
    /// <summary>
    /// Читает настройки. schemaVersion, mode, radarCount и radius обязательны.
    /// Объект discretization и его отдельные поля можно опустить: применяются настройки ядра по умолчанию.
    /// </summary>
    public SolverSettings2D Parse(string json, JsonReadLimits? limits = null)
    {
        var root = JsonInput.Parse(json, limits);
        JsonInput.OnlyProperties(root, "$", "schemaVersion", "mode", "radarCount", "radius", "discretization");
        JsonInput.Expect(root, "schemaVersion", "1.0", "$");
        JsonInput.Expect(root, "mode", "2D", "$");
        long count = JsonInput.Integer(root, "radarCount", "$");
        if (count is < 1 or > 10) throw new RadarJsonException("$.radarCount", "Допускается от 1 до 10 радаров.");
        var defaults = new DiscretizationSettings2D();
        var grid = defaults;
        if (root.TryGetProperty("discretization", out var d))
        {
            const string path = "$.discretization";
            JsonInput.OnlyProperties(d, path, "candidateStep", "coverageStep", "maxCandidatePositions", "maxGridCells", "maxBitsetBytes");
            grid = new()
            {
                CandidateStep = OptionalNumber(d, "candidateStep", defaults.CandidateStep),
                CoverageStep = OptionalNumber(d, "coverageStep", defaults.CoverageStep),
                MaxCandidatePositions = OptionalPositiveInt(d, "maxCandidatePositions", defaults.MaxCandidatePositions),
                MaxGridCells = OptionalPositiveInt(d, "maxGridCells", defaults.MaxGridCells),
                MaxBitsetBytes = d.TryGetProperty("maxBitsetBytes", out _) ? JsonInput.Integer(d, "maxBitsetBytes", path) : defaults.MaxBitsetBytes
            };
        }
        var settings = new SolverSettings2D
        {
            Radars = new() { RadarCount = (int)count, Radius = JsonInput.Number(root, "radius", "$") },
            Discretization = grid
        };
        Validate(settings);
        return settings;
    }

    /// <summary>Проверяет настройки приложения, в том числе созданные программно или заполненные через UI.</summary>
    public static void Validate(SolverSettings2D settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Radars is null) throw new RadarJsonException("$", "Параметры радаров отсутствуют.");
        if (settings.Discretization is null) throw new RadarJsonException("$.discretization", "Параметры дискретизации отсутствуют.");
        if (settings.Radars.RadarCount is < 1 or > 10)
            throw new RadarJsonException("$.radarCount", "Допускается от 1 до 10 радаров.");
        if (!double.IsFinite(settings.Radars.Radius) || settings.Radars.Radius is < 3000 or > 7000)
            throw new RadarJsonException("$.radius", "Радиус должен быть от 3000 до 7000 метров включительно.");
        var grid = settings.Discretization;
        Positive(grid.CandidateStep, "$.discretization.candidateStep");
        Positive(grid.CoverageStep, "$.discretization.coverageStep");
        if (grid.MaxCandidatePositions < 1) throw new RadarJsonException("$.discretization.maxCandidatePositions", "Лимит должен быть положительным.");
        if (grid.MaxGridCells < 1) throw new RadarJsonException("$.discretization.maxGridCells", "Лимит должен быть положительным.");
        if (grid.MaxBitsetBytes < 1) throw new RadarJsonException("$.discretization.maxBitsetBytes", "Лимит должен быть положительным.");
    }

    /// <summary>Создаёт JSON с явно записанными настройками, включая все значения по умолчанию.</summary>
    public string Serialize(SolverSettings2D settings, bool indented = true)
    {
        Validate(settings);
        return JsonOutput.Serialize(writer => Write(writer, settings), indented);
    }

    /// <summary>Синхронно загружает настройки из UTF-8-файла.</summary>
    public SolverSettings2D Load(string path, JsonReadLimits? limits = null) => LoadAsync(path, limits).GetAwaiter().GetResult();

    /// <summary>Асинхронно загружает настройки с ограничением размера и отменой.</summary>
    public async Task<SolverSettings2D> LoadAsync(string path, JsonReadLimits? limits = null, CancellationToken cancellationToken = default)
    {
        string json = await JsonFiles.ReadAsync(path, limits, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = Parse(json, limits);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Сохраняет настройки в UTF-8. По умолчанию существующий файл не заменяется.</summary>
    public void Save(SolverSettings2D settings, string path, bool overwrite = false) => SaveAsync(settings, path, overwrite).GetAwaiter().GetResult();

    /// <summary>Асинхронно сохраняет проверенные настройки через временный файл в той же папке.</summary>
    public Task SaveAsync(SolverSettings2D settings, string path, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return JsonFiles.WriteAsync(path, Serialize(settings), overwrite, cancellationToken);
    }

    internal static void Write(Utf8JsonWriter writer, SolverSettings2D settings)
    {
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", "1.0");
        writer.WriteString("mode", "2D");
        writer.WriteNumber("radarCount", settings.Radars.RadarCount);
        writer.WriteNumber("radius", settings.Radars.Radius);
        writer.WriteStartObject("discretization");
        writer.WriteNumber("candidateStep", settings.Discretization.CandidateStep);
        writer.WriteNumber("coverageStep", settings.Discretization.CoverageStep);
        writer.WriteNumber("maxCandidatePositions", settings.Discretization.MaxCandidatePositions);
        writer.WriteNumber("maxGridCells", settings.Discretization.MaxGridCells);
        writer.WriteNumber("maxBitsetBytes", settings.Discretization.MaxBitsetBytes);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static double OptionalNumber(JsonElement parent, string name, double fallback) =>
        parent.TryGetProperty(name, out _) ? JsonInput.Number(parent, name, "$.discretization") : fallback;

    private static int OptionalPositiveInt(JsonElement parent, string name, int fallback)
    {
        if (!parent.TryGetProperty(name, out _)) return fallback;
        long value = JsonInput.Integer(parent, name, "$.discretization");
        if (value is < 1 or > int.MaxValue) throw new RadarJsonException("$.discretization." + name, "Требуется положительное целое число Int32.");
        return (int)value;
    }

    private static void Positive(double value, string path)
    {
        if (!double.IsFinite(value) || value <= 0) throw new RadarJsonException(path, "Требуется положительное конечное число.");
    }
}
