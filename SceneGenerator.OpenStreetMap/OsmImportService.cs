namespace SceneGenerator.OpenStreetMap;

/// <summary>Координирует загрузку и разбор данных через переданный поставщик OSM.</summary>
public sealed class OsmImportService(IOsmDataProvider provider)
{
    /// <summary>Загружает данные, разбирает геометрию вне вызывающего потока и проверяет, что здания сохранённого проекта восстановлены.</summary>
    public async Task<OsmImportDocument> LoadAsync(OsmImportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var data = await provider.LoadAsync(options, cancellationToken);
        var document = await Task.Run(() => new OsmBuildingParser().Parse(data, data.SavedProject?.Options ?? options, cancellationToken), cancellationToken);
        if (data.SavedProject is { } project && project.Selections.Select(s => s.Id).Concat(project.EffectiveKiiIds).Any(id => !document.Buildings.Any(b => b.Id == id)))
            throw new FormatException("Не все здания проекта удалось восстановить из исходных данных.");
        return document;
    }
}
