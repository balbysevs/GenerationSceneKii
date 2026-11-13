using RadarPlacement.Serialization;

namespace RadarPlacement.ConsoleApp;

/// <summary>
/// Управляет диалогом и очередью сцен. Части класса разделены по назначению:
/// здесь навигация/загрузка, в Settings — параметры, в Calculations — расчёт и результаты.
/// Никаких геометрических алгоритмов или собственного разбора JSON в консоли нет.
/// </summary>
internal sealed partial class ConsoleApplication
{
    private readonly ConsoleUi _ui;
    private readonly OperationCancellation _cancellation;
    private readonly string _workingDirectory;
    private readonly string _demoDirectory;
    private readonly SceneJsonReader2D _sceneReader = new();
    private readonly SolverSettingsJson _settingsJson = new();
    private readonly ResultJsonWriter2D _resultWriter = new();
    internal ApplicationSession Session { get; }

    internal ConsoleApplication(TextReader input, TextWriter output, string workingDirectory,
        string demoDirectory, OperationCancellation cancellation, IConsoleTerminal? terminal = null)
    {
        _workingDirectory = Path.GetFullPath(workingDirectory);
        _demoDirectory = Path.GetFullPath(demoDirectory);
        _cancellation = cancellation;
        Session = new(_workingDirectory);
        _ui = new(input, output, _workingDirectory, terminal, BuildHeader);
    }

    internal async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h" or "/?")
        {
            ShowHelp();
            return 0;
        }
        if (args.Length != 0)
        {
            _ui.Line("Неизвестные аргументы. Запустите без аргументов для меню или с --help для справки.");
            return 2;
        }
        _ui.Line("RadarPlacement — размещение радаров, режим 2D");
        _ui.Line("Рабочая папка: " + ConsoleUi.Safe(_workingDirectory));
        _ui.Line("Для первого знакомства выберите 8 — готовый пример, затем 4 — расчёт.");
        try
        {
            while (true)
            {
                ShowMainMenu();
                try
                {
                    string choice = _ui.Read("Выберите пункт: ");
                    _ui.BeginScreen(choice switch
                    {
                        "1" => "Загрузка сцены", "2" => "Загрузка папки", "4" => "Расчёт сцен",
                        "5" => "Просмотр результатов", "7" => "Удаление из списка", "8" => "Загрузка примера",
                        "9" or "?" or "help" => "Справка", "0" => "Завершение работы", _ => "Выбор действия"
                    });
                    switch (choice)
                    {
                        case "1": await AddFileAsync(); break;
                        case "2": await AddDirectoryAsync(); break;
                        case "3": await SettingsMenuAsync(); continue;
                        case "4": await CalculateAsync(); break;
                        case "5": ViewResults(); continue;
                        case "6": await OutputMenuAsync(); continue;
                        case "7": RemoveScenes(); break;
                        case "8": await LoadDemoAsync(); break;
                        case "9": case "?": case "help": ShowHelp(); break;
                        case "0":
                            if (Session.Scenes.Any(s => s.Result is not null && s.SavedPath is null) &&
                                !_ui.Confirm("Есть несохранённые результаты в памяти. Выйти?")) break;
                            _ui.Line("Работа завершена. Сохранённые файлы остаются на диске.");
                            return 0;
                        default: _ui.Line("Введите номер пункта от 0 до 9."); break;
                    }
                }
                catch (OperationCanceledException)
                {
                    _ui.Line("Операция отменена. Ранее загруженные сцены и готовые результаты сохранены в сеансе.");
                }
                catch (Exception exception) when (ConsoleErrors.IsExpected(exception))
                {
                    _ui.Line("Ошибка: " + ConsoleUi.Safe(exception.Message));
                }
                _ui.Pause();
            }
        }
        catch (EndOfInputException)
        {
            _ui.Line("Ввод завершён. Выход из программы.");
            if (Session.Scenes.Any(s => s.Result is not null && s.SavedPath is null))
                _ui.Line("Несохранённые результаты этого сеанса не записаны на диск.");
            return 0;
        }
    }

    private void ShowMainMenu()
    {
        _ui.BeginScreen("Главное меню");
        _ui.Line("Для знакомства: 8 — пример, затем 4 — расчёт.");
        _ui.Line("1 — Добавить сцену из JSON-файла");
        _ui.Line("2 — Добавить сцены из папки");
        _ui.Line("3 — Настройки расчёта: изменить / загрузить / сохранить");
        _ui.Line("4 — Рассчитать сцены и сохранить результаты");
        _ui.Line("5 — Просмотреть сцены, результаты и координаты радаров");
        _ui.Line("6 — Папка результатов, карты покрытия, повторное сохранение");
        _ui.Line("7 — Убрать сцены из списка");
        _ui.Line("8 — Добавить пример и применить настройки примера");
        _ui.Line("9 — Справка");
        _ui.Line("0 — Выход");
    }

    private async Task AddFileAsync()
    {
        string? path = _ui.ReadPath("Путь к JSON-сцене");
        if (path is null) return;
        using var operation = _cancellation.Begin();
        await AddSceneAsync(path, operation.Token);
    }

    private async Task<bool> AddSceneAsync(string path, CancellationToken token)
    {
        if (Session.Contains(path))
        {
            Session.CurrentScene = Session.Scenes.First(s => string.Equals(s.SourcePath, path, StringComparison.OrdinalIgnoreCase));
            _ui.Line("Уже в списке: " + ConsoleUi.Safe(path));
            return false;
        }
        // Сначала полностью загружаем и проверяем документ. Ошибочный файл
        // не меняет уже загруженные сцены и не добавляет пустую запись в список.
        var document = await _sceneReader.LoadAsync(path, cancellationToken: token);
        var entry = new SceneEntry(path, document);
        Session.Scenes.Add(entry);
        Session.CurrentScene = entry;
        _ui.Line($"Добавлена сцена #{Session.Scenes.Count}: {ConsoleUi.Safe(document.SceneId)}; помех: {document.Scene.Obstacles.Count}.");
        return true;
    }

    private async Task AddDirectoryAsync()
    {
        string? directory = _ui.ReadPath("Папка со сценами (только JSON непосредственно в этой папке)");
        if (directory is null) return;
        using var operation = _cancellation.Begin();
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        int added = 0, duplicates = 0, errors = 0;
        foreach (string path in files)
        {
            operation.Token.ThrowIfCancellationRequested();
            try
            {
                if (await AddSceneAsync(path, operation.Token)) added++;
                else duplicates++;
            }
            catch (Exception exception) when (ConsoleErrors.IsExpected(exception))
            {
                errors++;
                _ui.Line($"Не загружен {ConsoleUi.Safe(Path.GetFileName(path))}: {ConsoleUi.Safe(exception.Message)}");
            }
        }
        _ui.Line($"Загрузка папки завершена: добавлено {added}, уже в списке {duplicates}, ошибок {errors}.");
        if (files.Length == 0) _ui.Line("JSON-файлы не найдены. Подпапки автоматически не обходятся.");
    }

    private async Task LoadDemoAsync()
    {
        using var operation = _cancellation.Begin();
        string scenePath = Path.Combine(_demoDirectory, "scene-2d.json");
        // Настройки читаем до изменения сеанса: неполный комплект примера не должен
        // оставлять пользователя с неожиданно изменившимися параметрами.
        var settings = await _settingsJson.LoadAsync(Path.Combine(_demoDirectory, "solver-settings-2d.json"), cancellationToken: operation.Token);
        await AddSceneAsync(scenePath, operation.Token);
        Session.SetSettings(settings);
        _ui.Line("Пример готов: два радара, радиус 3000 м, шаг позиций 500 м, шаг покрытия 1000 м.");
        _ui.Line("Это грубая демонстрационная сетка. Для расчёта выберите пункт 4.");
    }

    private void ListScenes()
    {
        for (int i = 0; i < Session.Scenes.Count; i++)
        {
            var entry = Session.Scenes[i];
            string status = entry.Result is null ? "не рассчитана" :
                entry.UsesSettings(Session.Settings) ? "готова" : "результат с другими настройками";
            if (entry.Result is not null && entry.SavedPath is null) status += "; не сохранена";
            if (entry.LastFailure is not null) status += "; последняя попытка не завершена";
            _ui.Line($"{i + 1}. {ConsoleUi.Safe(Path.GetFileName(entry.SourcePath))} — {ConsoleUi.Safe(entry.Document.SceneId)} [{status}]");
        }
    }

    private SceneEntry[] SelectScenes(string action)
    {
        if (Session.Scenes.Count == 0)
        {
            _ui.Line("Сначала добавьте сцену: пункт 1, 2 или 8.");
            return [];
        }
        ListScenes();
        string selection = _ui.Read(action + " — номер сцены [Enter или «все» — все; 0 — назад]: ");
        if (selection == "0") return [];
        if (selection.Length == 0 || selection.Equals("все", StringComparison.OrdinalIgnoreCase) || selection == "*")
        {
            Session.CurrentScene = Session.Scenes[0];
            return Session.Scenes.ToArray();
        }
        if (int.TryParse(selection, out int number) && number >= 1 && number <= Session.Scenes.Count)
        {
            Session.CurrentScene = Session.Scenes[number - 1];
            return [Session.Scenes[number - 1]];
        }
        _ui.Line("Такого номера сцены нет.");
        return [];
    }

    private void RemoveScenes()
    {
        var selected = SelectScenes("Убрать из списка");
        if (selected.Length == 0) return;
        if ((selected.Length > 1 || selected.Any(s => s.Result is not null && s.SavedPath is null)) &&
            !_ui.Confirm($"Убрать {selected.Length} сцен из памяти сеанса? Файлы на диске останутся")) return;
        foreach (var scene in selected) Session.Scenes.Remove(scene);
        if (Session.CurrentScene is not null && !Session.Scenes.Contains(Session.CurrentScene))
            Session.CurrentScene = Session.Scenes.FirstOrDefault();
        _ui.Line($"Из списка убрано: {selected.Length}. Файлы не удалялись.");
    }

    private void ShowHelp()
    {
        _ui.Line("RadarPlacement — интерактивная консоль, 2D.");
        _ui.Line("Запуск без аргументов открывает меню. --help, -h или /? выводят эту справку.");
        _ui.Line("В окне экраны заменяют друг друга; сводка сверху показывает текущую сцену и параметры.");
        _ui.Line("PgUp/PgDn — страницы длинного экрана; Enter после результата/ошибки — продолжить.");
        _ui.Line("Рабочая папка: " + ConsoleUi.Safe(_workingDirectory));
        _ui.Line("Начало: 1/2 — загрузить сцены; 3 — настроить радары и сетку; 4 — рассчитать; 5 — посмотреть координаты.");
        _ui.Line("Быстрая проверка: 8 — пример, 4 — расчёт, Enter — все сцены.");
        _ui.Line("Готовые результаты автоматически записываются в Result/run-…/ (папка меняется в пункте 6).");
        _ui.Line("Там же можно включить экспорт карт покрытия и повторно сохранить результаты из памяти.");
        _ui.Line("Ctrl+C во время загрузки, расчёта или сохранения отменяет операцию и возвращает меню.");
        _ui.Line("В остальных случаях Ctrl+C завершает программу; обычный выход — пункт 0.");
        _ui.Line("Числа вводятся с точкой или запятой. Enter при редактировании оставляет прежнее значение.");
        _ui.Line("Пути могут содержать пробелы, кириллицу, кавычки и %USERPROFILE%. Относительные пути считаются от рабочей папки.");
        _ui.Line("Поддерживаются JSON версии 1.0, один прямоугольный КИИ и прямоугольные помехи без дворов.");
        _ui.Line("Максимум точен для заданных позиций и сетки покрытия. Мелкая сетка увеличивает время и память.");
        _ui.Line("Готовый результат с прежними настройками остаётся доступен; для новых настроек запустите расчёт повторно.");
        _ui.Line("Загруженные сцены — снимки файлов. Для чтения изменённого файла уберите его из списка и добавьте заново.");
    }
}
