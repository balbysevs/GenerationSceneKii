using RadarPlacement.ConsoleApp;

/// <summary>
/// Проверки экранного режима на той же программе и реальном ядре.
/// Терминал хранит кадры вместо обращения к окну ОС, но ввод проходит через редактор клавиш.
/// Проверяем видимое поведение: контекст сцены, отсутствие старого меню, доступность текста,
/// пределы окна и отмену во время перерисовки.
/// </summary>
internal static class ScreenChecks
{
    internal static void Workflow()
    {
        var terminal = new RecordingTerminal();
        terminal.Lines("8", "", "4", "1", "", "3", "1", "3", "", "0", "5", "1", "", "7", "1", "", "0");
        using var h = new Harness("", terminal);
        Require(h.Run() == 0 && terminal.Remaining == 0, "Экранный диалог не завершён.");
        var calculations = terminal.Frames.Where(f => f.Contains("── Расчёт сцен", StringComparison.Ordinal)).ToArray();
        Require(calculations.Length > 0 && calculations.All(f => !f.Contains("8 — Добавить пример", StringComparison.Ordinal)),
            "Новое действие должно заменять главное меню.");
        Require(calculations.All(f => f.Contains("ID: generator-compatibility-2d", StringComparison.Ordinal)), "Во время расчёта потерян ID.");
        Require(terminal.Frames.Any(f => f.Contains("Радары: 3", StringComparison.Ordinal) && f.Contains("ДРУГИЕ НАСТРОЙКИ", StringComparison.Ordinal)),
            "Сводка не показывает изменение параметров и устаревание результата.");
        Require(h.App.Session.CurrentScene is null && h.App.Session.Scenes.Count == 0, "Сводка ссылается на удалённую сцену.");
        Require(Directory.GetFiles(h.App.Session.OutputDirectory, "*.solved.json", SearchOption.AllDirectories).Length == 1,
            "Экранный режим нарушил сохранение результата.");
        Require(h.Text.Length == 0, "Экранный режим не должен дописывать строки в обычный поток.");
    }

    internal static void Error()
    {
        var terminal = new RecordingTerminal();
        terminal.Lines("1", "missing.json", "", "0");
        using var h = new Harness("", terminal);
        h.Run();
        Require(terminal.Frames.Any(f => f.Contains("Ошибка:", StringComparison.Ordinal) &&
            f.Contains("Enter — продолжить", StringComparison.Ordinal) && f.Contains("Радары:", StringComparison.Ordinal)),
            "Ошибка исчезла до подтверждения либо скрыла сводку.");
    }

    internal static void PagingAndInput()
    {
        var terminal = new RecordingTerminal { Size = (42, 12) };
        using var output = new StringWriter();
        var ui = new ConsoleUi(TextReader.Null, output, Environment.CurrentDirectory, terminal,
            () => ["ID: test-scene", "Радары: 2", "Шаг: 500 м"]);
        ui.BeginScreen("Длинный список");
        for (int i = 0; i < 25; i++) ui.Line($"Элемент {i:D2}");
        for (int i = 0; i < 12; i++) terminal.Key(ConsoleKey.PageDown);
        terminal.Key(ConsoleKey.PageUp);
        terminal.Key(ConsoleKey.PageDown);
        string path = "C:\\папка\\" + new string('я', 180) + ".json";
        terminal.Text(path);
        terminal.Key(ConsoleKey.LeftArrow);
        terminal.Key(ConsoleKey.Backspace);
        terminal.Text("o");
        terminal.Key(ConsoleKey.End);
        terminal.Key(ConsoleKey.Enter);
        int keys = 0;
        terminal.OnRead = () => { if (++keys == 4) terminal.Size = (35, 10); };
        Require(ui.Read("Путь:") == path, "Редактирование длинного пути потеряло символы.");
        Require(terminal.Frames.Any(f => f.Contains("Элемент 24", StringComparison.Ordinal) && f.Contains("PgUp/PgDn", StringComparison.Ordinal)),
            "Последняя страница списка недоступна.");
        Require(terminal.Frames.All(f => f.StartsWith("ID: test-scene", StringComparison.Ordinal)), "Сводка исчезает при прокрутке.");
        foreach (var size in new[] { (1, 1), (15, 4), (80, 24) })
        {
            terminal.Size = size;
            terminal.Lines("");
            ui.Pause(); // Даже минимальное окно не должно приводить к выходу координат за границы.
        }
    }

    internal static void ProgressAndCancellation()
    {
        var terminal = new RecordingTerminal();
        using var output = new StringWriter();
        var ui = new ConsoleUi(TextReader.Null, output, Environment.CurrentDirectory, terminal, () => ["ID: progress"]);
        ui.BeginScreen("Расчёт");
        ui.Progress("Прогресс: 1");
        ui.Progress("Прогресс: 2");
        Require(terminal.Frames[^1].Contains("Прогресс: 2", StringComparison.Ordinal) &&
            !terminal.Frames[^1].Contains("Прогресс: 1", StringComparison.Ordinal), "Отчёты накапливаются вместо замены.");

        var appTerminal = new RecordingTerminal();
        appTerminal.Lines("8", "", "4", "1", "", "4", "1", "", "0");
        using var h = new Harness("", appTerminal);
        bool cancelled = false;
        appTerminal.OnDraw = frame =>
        {
            if (!cancelled && frame.Contains("Подготовка сетки наблюдения", StringComparison.Ordinal))
            {
                cancelled = true;
                Require(h.Cancellation.RequestCancellation(), "Нет активной операции.");
            }
        };
        h.Run();
        Require(cancelled && appTerminal.Remaining == 0 && h.App.Session.CurrentScene?.SavedPath is not null,
            "Отмена экранного расчёта помешала повторному запуску.");
        Require(appTerminal.Frames.Any(f => f.Contains("Расчёт отменён", StringComparison.Ordinal) && f.Contains("Enter — продолжить", StringComparison.Ordinal)),
            "Сообщение об отмене не осталось на экране.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed class RecordingTerminal : IConsoleTerminal
{
    private readonly Queue<ConsoleKeyInfo> _keys = new();
    public (int Width, int Height) Size { get; set; } = (100, 28);
    internal List<string> Frames { get; } = [];
    internal Action<string>? OnDraw { get; set; }
    internal Action? OnRead { get; set; }
    internal int Remaining => _keys.Count;

    public void Draw(ConsoleFrame frame)
    {
        if (frame.Lines.Length > Size.Height || frame.Lines.Any(line => line.Length > Size.Width) ||
            frame.CursorColumn is int column && (column < 0 || column >= Size.Width) ||
            frame.CursorRow is int row && (row < 0 || row >= Size.Height))
            throw new InvalidOperationException("Кадр/курсор вышел за границы окна.");
        string text = string.Join('\n', frame.Lines);
        if (Frames.Count == 0 || Frames[^1] != text) Frames.Add(text);
        OnDraw?.Invoke(text);
    }

    public ConsoleKeyInfo? ReadKey()
    {
        OnRead?.Invoke();
        return _keys.Count > 0 ? _keys.Dequeue() : throw new EndOfInputException();
    }

    internal void Key(ConsoleKey key) => _keys.Enqueue(new('\0', key, false, false, false));
    internal void Text(string text)
    {
        foreach (char c in text) _keys.Enqueue(new(c, (ConsoleKey)0, false, false, false));
    }
    internal void Lines(params string[] lines)
    {
        foreach (string line in lines) { Text(line); Key(ConsoleKey.Enter); }
    }
}
