namespace RadarPlacement.ConsoleApp;

internal sealed partial class ConsoleApplication
{
    /// <summary>
    /// Строит общую для всех экранов панель из актуального состояния сеанса.
    /// Текущая сцена меняется при загрузке, выборе, просмотре и переходе к следующей
    /// сцене серии. Параметры панели относятся к следующему запуску; старый результат
    /// явно отмечается, если он был рассчитан с другими параметрами.
    /// </summary>
    private IReadOnlyList<string> BuildHeader()
    {
        var settings = Session.Settings;
        var entry = Session.CurrentScene;
        int ready = Session.Scenes.Count(s => s.UsesSettings(settings));
        int unsaved = Session.Scenes.Count(s => s.Result is not null && s.SavedPath is null);
        string scene = entry is null ? "Сцена: не выбрана" :
            $"Сцена #{Session.Scenes.IndexOf(entry) + 1} | ID: {entry.Document.SceneId}";
        string geometry = entry is null ? "Загрузите JSON: пункт 1, 2 или 8." :
            $"Участок {ConsoleUi.Number(entry.Document.Scene.Bounds.Width)} × {ConsoleUi.Number(entry.Document.Scene.Bounds.Height)} м | " +
            $"Помех: {entry.Document.Scene.Obstacles.Count}";
        string result = entry?.Result is null ? "Результат: нет" :
            $"Результат: {Percentage(entry.Result.Coverage.CoveragePercent)} | " +
            (entry.UsesSettings(settings) ? "актуален" : "ДРУГИЕ НАСТРОЙКИ") + " | " +
            (entry.SavedPath is null ? "НЕ СОХРАНЁН" : "сохранён");
        if (entry?.LastFailure is not null) result += " | последняя попытка: сбой/отмена";
        return
        [
            $"RadarPlacement | 2D | Сцен: {Session.Scenes.Count} | Готово: {ready} | Не сохранено: {unsaved}",
            scene,
            $"Радары: {settings.Radars.RadarCount} | Радиус: {ConsoleUi.Number(settings.Radars.Radius)} м",
            $"Шаг позиций: {ConsoleUi.Number(settings.Discretization.CandidateStep)} м | Покрытия: {ConsoleUi.Number(settings.Discretization.CoverageStep)} м",
            result,
            geometry,
            "Вывод: " + Session.OutputDirectory,
            "Карты покрытия в JSON: " + (Session.IncludeCoverageGrid ? "включены" : "выключены")
        ];
    }
}
