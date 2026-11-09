using System.Text;
using System.Text.Json;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Models;
using SceneGenerator.Serialization.Dto;
using SceneGenerator.Serialization.Mapping;

namespace SceneGenerator.Serialization;

public sealed class JsonSceneExporter : ISceneExporter
{
    private readonly SceneDtoMapper _mapper = new();
    private readonly SceneSourceDto _source;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    /// <summary>Создаёт экспортёр с метаданными источника, которые будут включены в JSON каждой сцены.</summary>
    public JsonSceneExporter(SceneSourceDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source with { };
    }

    /// <summary>Сохраняет сцену в отдельный JSON-файл через временный файл, не перезаписывая готовый результат.</summary>
    public void Export(Scene scene, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        SceneDto dto = _mapper.ToDto(scene, _source);
        string json = JsonSerializer.Serialize(dto, _options) + Environment.NewLine;
        string fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);


        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
