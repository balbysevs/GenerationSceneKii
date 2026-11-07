namespace SceneGenerator.OpenStreetMap;

public sealed class OsmImportService(IOsmDataProvider provider)
{
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
