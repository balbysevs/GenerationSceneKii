using RadarPlacement.Serialization;

namespace RadarPlacement.ConsoleApp;

internal sealed partial class ConsoleApplication
{
    /// <summary>Изменяет только проверенные настройки. Результаты прошлых расчётов не уничтожаются.</summary>
    private async Task SettingsMenuAsync()
    {
        while (true)
        {
            var settings = Session.Settings;
            var grid = settings.Discretization;
            _ui.BeginScreen("Настройки расчёта");
            _ui.Line("Настройки расчёта");
            _ui.Line($"1 — Количество радаров: {settings.Radars.RadarCount} (1–10)");
            _ui.Line($"2 — Радиус каждого: {ConsoleUi.Number(settings.Radars.Radius)} м (3000–7000)");
            _ui.Line($"3 — Шаг позиций на КИИ: {ConsoleUi.Number(grid.CandidateStep)} м");
            _ui.Line($"4 — Шаг покрытия: {ConsoleUi.Number(grid.CoverageStep)} м");
            _ui.Line($"5 — Лимиты: {grid.MaxCandidatePositions} позиций, {grid.MaxGridCells} ячеек, " +
                $"{ConsoleUi.Number(grid.MaxBitsetBytes / 1048576.0)} МиБ битовых буферов");
            _ui.Line("6 — Загрузить настройки из JSON");
            _ui.Line("7 — Сохранить настройки в JSON");
            _ui.Line("8 — Восстановить настройки по умолчанию");
            _ui.Line("0 — Назад");
            try
            {
                string choice = _ui.Read("Выберите параметр: ");
                _ui.BeginScreen("Изменение настроек");
                switch (choice)
                {
                    case "0": return;
                    case "1":
                        SetSettings(settings with { Radars = settings.Radars with {
                            RadarCount = (int)_ui.ReadInteger("Количество радаров", settings.Radars.RadarCount, 1, 10) } });
                        break;
                    case "2":
                        SetSettings(settings with { Radars = settings.Radars with {
                            Radius = _ui.ReadNumber("Радиус, м", settings.Radars.Radius, 3000, 7000) } });
                        break;
                    case "3":
                        SetSettings(settings with { Discretization = grid with {
                            CandidateStep = _ui.ReadNumber("Шаг позиций, м (положительное число)", grid.CandidateStep, double.Epsilon, double.MaxValue) } });
                        break;
                    case "4":
                        SetSettings(settings with { Discretization = grid with {
                            CoverageStep = _ui.ReadNumber("Шаг покрытия, м (положительное число)", grid.CoverageStep, double.Epsilon, double.MaxValue) } });
                        break;
                    case "5": EditResourceLimits(); break;
                    case "6": await LoadSettingsAsync(); break;
                    case "7": await SaveSettingsAsync(); break;
                    case "8": SetSettings(new()); break;
                    default: _ui.Line("Введите номер пункта от 0 до 8."); break;
                }
            }
            catch (OperationCanceledException) { _ui.Line("Операция с настройками отменена."); }
            catch (Exception exception) when (ConsoleErrors.IsExpected(exception)) { _ui.Line("Ошибка: " + ConsoleUi.Safe(exception.Message)); }
            _ui.Pause();
        }
    }

    private void SetSettings(SolverSettings2D settings)
    {
        Session.SetSettings(settings);
        _ui.Line("Настройки применены.");
        int oldResults = Session.Scenes.Count(s => s.Result is not null && !s.UsesSettings(settings));
        if (oldResults > 0) _ui.Line($"Результатов с другими настройками: {oldResults}. Для обновления выберите расчёт в главном меню.");
    }

    private void EditResourceLimits()
    {
        var current = Session.Settings.Discretization;
        // Все три значения собираются локально. При EOF или ошибке старый набор остаётся целым.
        int positions = (int)_ui.ReadInteger("Максимум позиций", current.MaxCandidatePositions, 1, int.MaxValue);
        int cells = (int)_ui.ReadInteger("Максимум ячеек", current.MaxGridCells, 1, int.MaxValue);
        long bytes = _ui.ReadInteger("Максимум битовых буферов, байт (256 МиБ = 268435456)", current.MaxBitsetBytes, 1, long.MaxValue);
        SetSettings(Session.Settings with { Discretization = current with {
            MaxCandidatePositions = positions, MaxGridCells = cells, MaxBitsetBytes = bytes } });
    }

    private async Task LoadSettingsAsync()
    {
        string? path = _ui.ReadPath("Путь к JSON-настройкам");
        if (path is null) return;
        using var operation = _cancellation.Begin();
        var settings = await _settingsJson.LoadAsync(path, cancellationToken: operation.Token);
        SetSettings(settings);
    }

    private async Task SaveSettingsAsync()
    {
        string? path = _ui.ReadPath("Куда сохранить настройки", "solver-settings.json");
        if (path is null) return;
        bool overwrite = File.Exists(path);
        if (overwrite && !_ui.Confirm("Заменить существующий файл " + ConsoleUi.Safe(path) + "?")) return;
        using var operation = _cancellation.Begin();
        await _settingsJson.SaveAsync(Session.Settings, path, overwrite, operation.Token);
        _ui.Line("Настройки сохранены: " + ConsoleUi.Safe(path));
    }

    private async Task OutputMenuAsync()
    {
        while (true)
        {
            _ui.BeginScreen("Сохранение результатов");
            _ui.Line("Сохранение результатов");
            _ui.Line("1 — Папка: " + ConsoleUi.Safe(Session.OutputDirectory));
            _ui.Line("2 — Карты покрытия в JSON: " + (Session.IncludeCoverageGrid ? "включены" : "выключены") + " (переключить)");
            _ui.Line("3 — Сохранить имеющиеся результаты в новую папку запуска");
            _ui.Line("0 — Назад");
            try
            {
                string choice = _ui.Read("Выберите пункт: ");
                _ui.BeginScreen("Параметры сохранения");
                switch (choice)
                {
                    case "0": return;
                    case "1":
                        string? path = _ui.ReadPath("Папка результатов", Session.OutputDirectory);
                        if (path is null) break;
                        if (File.Exists(path)) throw new ArgumentException("Этот путь указывает на файл. Выберите папку.");
                        Session.OutputDirectory = path;
                        _ui.Line("Папка результатов изменена. Она будет создана при сохранении.");
                        break;
                    case "2":
                        Session.IncludeCoverageGrid = !Session.IncludeCoverageGrid;
                        _ui.Line("Экспорт карт " + (Session.IncludeCoverageGrid ? "включён." : "выключен."));
                        break;
                    case "3": await SaveExistingResultsAsync(); break;
                    default: _ui.Line("Введите номер пункта от 0 до 3."); break;
                }
            }
            catch (OperationCanceledException) { _ui.Line("Сохранение отменено. Уже записанные файлы остаются на диске."); }
            catch (Exception exception) when (ConsoleErrors.IsExpected(exception)) { _ui.Line("Ошибка: " + ConsoleUi.Safe(exception.Message)); }
            _ui.Pause();
        }
    }
}
