using System.Diagnostics;
using System.Globalization;
using RadarPlacement.Core;
using RadarPlacement.Serialization;

namespace RadarPlacement.ConsoleApp;

internal sealed partial class ConsoleApplication
{
    /// <summary>
    /// Обрабатывает выбранные сцены последовательно. После каждого успешного расчёта
    /// сохраняет отдельный JSON; сбой одной сцены не обнуляет всю серию.
    /// Старый результат заменяется в памяти только после успешного завершения нового поиска.
    /// </summary>
    private async Task CalculateAsync()
    {
        var selected = SelectScenes("Рассчитать");
        if (selected.Length == 0) return;
        using var operation = _cancellation.Begin();
        string runDirectory = CreateRunDirectory();
        var settings = Session.Settings;
        _ui.Line("Результаты запуска: " + ConsoleUi.Safe(runDirectory));
        _ui.Line("Ctrl+C — отменить оставшуюся часть расчёта и вернуться в меню.");
        int calculated = 0, saved = 0, failed = 0;
        bool cancelled = false;
        for (int i = 0; i < selected.Length; i++)
        {
            var entry = selected[i];
            bool newResultReady = false;
            try
            {
                operation.Token.ThrowIfCancellationRequested();
                _ui.Line($"[{i + 1}/{selected.Length}] Расчёт: {ConsoleUi.Safe(Path.GetFileName(entry.SourcePath))}");
                var watch = Stopwatch.StartNew();
                var progress = new ProgressDisplay(_ui);
                var result = await Task.Run(() => new RadarPlacementSolver2D().Solve(entry.Document.Scene,
                    settings.Radars, settings.Discretization, progress, operation.Token), operation.Token);
                entry.Result = result;
                entry.CalculationTime = watch.Elapsed;
                entry.SavedPath = null;
                entry.LastFailure = null;
                newResultReady = true;
                calculated++;
                _ui.Line($"  Покрытие: {ConsoleUi.Number(result.Coverage.CoveredArea)} м²; {Percentage(result.Coverage.CoveragePercent)}; " +
                    $"время расчёта: {entry.CalculationTime.TotalSeconds.ToString("F2", ConsoleUi.Russian)} с.");
                await SaveEntryAsync(entry, runDirectory, operation.Token);
                saved++;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                entry.LastFailure = "Операция отменена пользователем.";
                _ui.Line(newResultReady
                    ? "Расчёт завершён, сохранение отменено. Результат остался в памяти: его можно сохранить через пункт 6."
                    : "Расчёт отменён. Ранее готовый результат этой сцены, если он был, остаётся доступен.");
                break;
            }
            catch (Exception exception) when (ConsoleErrors.IsExpected(exception))
            {
                failed++;
                entry.LastFailure = exception.Message;
                _ui.Line((newResultReady ? "Расчёт готов, но не сохранён: " : "Ошибка расчёта: ") + ConsoleUi.Safe(exception.Message));
                if (newResultReady) _ui.Line("Результат остался в памяти. Измените папку и сохраните его через пункт 6.");
            }
        }
        _ui.Line($"Итог: рассчитано {calculated}, сохранено {saved}, ошибок {failed}" + (cancelled ? "; серия отменена." : "."));
    }

    private async Task SaveExistingResultsAsync()
    {
        var selected = SelectScenes("Сохранить");
        var ready = selected.Where(s => s.Result is not null).ToArray();
        if (selected.Length == 0) return;
        if (ready.Length == 0) { _ui.Line("У выбранных сцен ещё нет готовых результатов."); return; }
        using var operation = _cancellation.Begin();
        string directory = CreateRunDirectory();
        _ui.Line("Новая папка сохранения: " + ConsoleUi.Safe(directory));
        int saved = 0, failed = 0;
        foreach (var entry in ready)
        {
            operation.Token.ThrowIfCancellationRequested();
            if (!entry.UsesSettings(Session.Settings))
                _ui.Line("Сохраняется прежний результат со своими настройками: " + ConsoleUi.Safe(entry.Document.SceneId));
            try { await SaveEntryAsync(entry, directory, operation.Token); saved++; }
            catch (Exception exception) when (ConsoleErrors.IsExpected(exception))
            {
                failed++;
                _ui.Line("Не удалось сохранить " + ConsoleUi.Safe(entry.Document.SceneId) + ": " + ConsoleUi.Safe(exception.Message));
            }
        }
        _ui.Line($"Сохранено {saved}, ошибок {failed}, без результата {selected.Length - ready.Length}.");
    }

    private async Task SaveEntryAsync(SceneEntry entry, string directory, CancellationToken token)
    {
        int sceneNumber = Session.Scenes.IndexOf(entry) + 1;
        string path = Path.Combine(directory, $"{sceneNumber:D3}-{FileStem(entry.SourcePath)}.solved.json");
        await _resultWriter.SaveAsync(entry.Document, entry.Result!, path,
            new ResultJsonOptions { IncludeCoverageGrid = Session.IncludeCoverageGrid }, cancellationToken: token);
        entry.SavedPath = path;
        _ui.Line("  Сохранено: " + ConsoleUi.Safe(path));
    }

    private string CreateRunDirectory()
    {
        // Уникальная подпапка защищает предыдущие результаты и исходные сцены.
        // Имена не зависят от sceneId: чужие строки вроде '../../file' не становятся путями.
        string name = "run-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8];
        string path = Path.Combine(Session.OutputDirectory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FileStem(string sourcePath)
    {
        string stem = string.Concat(Path.GetFileNameWithoutExtension(sourcePath).Take(60)
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        return stem.Length == 0 ? "scene" : stem;
    }

    private static string Percentage(double? value) => value is double number
        ? ConsoleUi.Number(number) + "%" : "процент не определён (нет наблюдаемых центров ячеек)";

    private void ViewResults()
    {
        foreach (var entry in SelectScenes("Просмотреть"))
        {
            var scene = entry.Document.Scene;
            _ui.Line();
            _ui.Line("Сцена: " + ConsoleUi.Safe(entry.Document.SceneId));
            _ui.Line("Источник: " + ConsoleUi.Safe(entry.SourcePath));
            _ui.Line($"Участок: {ConsoleUi.Number(scene.Bounds.Width)} × {ConsoleUi.Number(scene.Bounds.Height)} м; " +
                $"КИИ: {ConsoleUi.Number(scene.CriticalInfrastructure.Width)} × {ConsoleUi.Number(scene.CriticalInfrastructure.Height)} м; помех: {scene.Obstacles.Count}.");
            if (entry.LastFailure is not null) _ui.Line("Последняя неудачная попытка: " + ConsoleUi.Safe(entry.LastFailure));
            if (entry.Result is not { } result) { _ui.Line("Расчёт ещё не завершён."); continue; }
            if (!entry.UsesSettings(Session.Settings))
                _ui.Line("Это ранее рассчитанный результат с другими настройками. Новые настройки к нему не применялись.");
            _ui.Line($"Радаров: {result.Radars.Count}; радиус: {ConsoleUi.Number(result.RadarSettings.Radius)} м.");
            _ui.Line($"Шаги этого расчёта: позиции {ConsoleUi.Number(result.Discretization.CandidateStep)} м, покрытие {ConsoleUi.Number(result.Discretization.CoverageStep)} м.");
            _ui.Line($"Сетка: {result.Coverage.Columns} × {result.Coverage.Rows}; кандидатов: {result.CandidatePositions.Count}.");
            _ui.Line($"Покрыто: {result.Coverage.CoveredCellCount} из {result.Coverage.ObservableCellCount} наблюдаемых ячеек; {Percentage(result.Coverage.CoveragePercent)}.");
            _ui.Line($"Площадь покрытия: {ConsoleUi.Number(result.Coverage.CoveredArea)} м²; " +
                $"наблюдаемая площадь по сетке: {ConsoleUi.Number(result.Coverage.ObservableArea)} м².");
            _ui.Line($"Глобальный максимум дискретной модели подтверждён. Узлов поиска: {result.VisitedSearchNodes}; отсечено: {result.PrunedSearchNodes}.");
            _ui.Line("Координаты радаров, метры:");
            for (int i = 0; i < result.Radars.Count; i++)
            {
                var point = result.Radars[i].Position;
                _ui.Line($"  {i + 1}. X = {point.X.ToString("G17", ConsoleUi.Russian)}; Y = {point.Y.ToString("G17", ConsoleUi.Russian)}");
            }
            _ui.Line("Файл результата: " + (entry.SavedPath is null ? "не сохранён; используйте пункт 6" : ConsoleUi.Safe(entry.SavedPath)));
        }
    }
}
