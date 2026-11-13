using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RadarPlacement.ConsoleApp;
using RadarPlacement.Serialization;

// Здесь проверяется диалог вместе с реальными JSON-библиотекой, ядром и файловой системой.
// Ввод/вывод подменены только для воспроизводимости. В конце запускается отдельный процесс приложения.
var checks = new (string Name, Action Run)[]
{
    ("Справка и ошибки аргументов не запускают расчёт", CheckArguments),
    ("Неверный пункт меню и EOF в середине изменения лимитов", CheckNavigation),
    ("Пример → расчёт → автоматическое сохранение → координаты", CheckDemoWorkflow),
    ("Пути с кавычками и кириллицей, дубли и повреждённые файлы", CheckFileLoading),
    ("Папка: частичный успех, регистр расширения и отсутствие рекурсии", CheckFolderLoading),
    ("Изменение параметров, границы и десятичная запятая", CheckSettingsEditing),
    ("Ошибочные настройки сохраняют прежнее состояние", CheckInvalidSettingsLoad),
    ("Подтверждение замены JSON-настроек", CheckSettingsOverwrite),
    ("Серия продолжает работу после ошибки одной сцены", CheckBatchFailure),
    ("Одинаковые имена файлов не перезаписывают результаты", CheckDuplicateNames),
    ("Неудачный пересчёт сохраняет предыдущий результат и его параметры", CheckOldResults),
    ("Повторный экспорт добавляет карты без повторного расчёта", CheckCoverageExport),
    ("Отмена возвращает меню; следующий запуск получает новый токен", CheckCancellationAndRetry),
    ("Отмена серии сохраняет уже готовые файлы", CheckBatchCancellation),
    ("Ошибка сохранения оставляет результат в памяти для повторной записи", CheckSavingRecovery),
    ("Удаление из списка не удаляет исходники и результаты", CheckRemoval),
    ("Недоступная папка результатов возвращает меню до расчёта", CheckOutputFailure),
    ("Настоящий процесс: справка, код ошибки и полный диалог через stdin", CheckRealProcess),
    ("Сменяемые экраны: сводка, расчёт, старые настройки и удаление сцены", ScreenChecks.Workflow),
    ("Экран ошибки остаётся до подтверждения", ScreenChecks.Error),
    ("Постраничный просмотр, изменение размера и длинный ввод", ScreenChecks.PagingAndInput),
    ("Прогресс заменяется на месте, отмена возвращает рабочее меню", ScreenChecks.ProgressAndCancellation)
};
int failed = 0;
foreach (var (name, run) in checks)
{
    try { run(); Console.WriteLine("PASS: " + name); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL: {name}\n{exception}"); }
}
Console.WriteLine($"Проверок: {checks.Length}; успешно: {checks.Length - failed}; ошибок: {failed}.");
return failed == 0 ? 0 : 1;

static string Lines(params string[] input) => string.Join(Environment.NewLine, input) + Environment.NewLine;
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static string FixturePath() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "scene-2d.json");
static SolverSettings2D ExampleSettings() => new SolverSettingsJson().Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "solver-settings-2d.json"));
static string CopyScene(string directory, string file = "scene.json")
{
    Directory.CreateDirectory(directory);
    string path = Path.Combine(directory, file);
    File.Copy(FixturePath(), path);
    return path;
}
static string[] Results(string directory) => Directory.Exists(directory)
    ? Directory.GetFiles(directory, "*.solved.json", SearchOption.AllDirectories) : [];

static void CheckArguments()
{
    using var help = new Harness("");
    Require(help.Run("--help") == 0 && help.Text.Contains("Ctrl+C", StringComparison.Ordinal), "Нет справки.");
    Require(help.App.Session.Scenes.Count == 0 && !Directory.Exists(help.App.Session.OutputDirectory), "Справка не должна менять состояние.");
    using var bad = new Harness("");
    Require(bad.Run("--unknown") == 2 && bad.Text.Contains("Неизвестные аргументы", StringComparison.Ordinal), "Неверный код аргументов.");
}

static void CheckNavigation()
{
    using var h = new Harness(Lines("abc", "42", "3", "5", "123"));
    Require(h.Run() == 0 && h.Text.Contains("Ввод завершён", StringComparison.Ordinal), "EOF должен завершать диалог.");
    Require(h.Text.Contains("Введите номер пункта", StringComparison.Ordinal), "Неверный пункт должен объясняться.");
    Require(h.App.Session.Settings.Discretization.MaxCandidatePositions == 10000, "Незавершённое изменение лимитов не должно применяться.");
}

static void CheckDemoWorkflow()
{
    using var h = new Harness(Lines("8", "4", "", "5", "1", "0"));
    Require(h.Run() == 0, "Ошибка сеанса.");
    var entry = h.App.Session.Scenes.Single();
    Require(entry.Result is not null && entry.SavedPath is not null && File.Exists(entry.SavedPath), "Нет автоматически сохранённого результата.");
    using var result = JsonDocument.Parse(File.ReadAllText(entry.SavedPath!));
    Require(result.RootElement.GetProperty("radars").GetArrayLength() == 2, "Неверное количество радаров.");
    Require(h.Text.Contains("Координаты радаров", StringComparison.Ordinal) && h.Text.Contains("Глобальный максимум", StringComparison.Ordinal), "Нет просмотра результата.");
}

static void CheckFileLoading()
{
    using var h = new Harness(root =>
    {
        string path = CopyScene(root, "моя сцена.json");
        File.WriteAllText(Path.Combine(root, "bad.json"), "{");
        return Lines("1", "\"" + path + "\"", "1", "моя сцена.json", "1", "bad.json", "0");
    });
    h.Run();
    Require(h.App.Session.Scenes.Count == 1, "Дубли/ошибки не должны создавать записи.");
    Require(h.Text.Contains("Уже в списке", StringComparison.Ordinal) && h.Text.Contains("Некорректный JSON", StringComparison.Ordinal), "Нет сообщений о проблемах загрузки.");
}

static void CheckFolderLoading()
{
    using var h = new Harness(root =>
    {
        string directory = Path.Combine(root, "scenes");
        CopyScene(directory, "one.json");
        CopyScene(directory, "two.JSON");
        CopyScene(Path.Combine(directory, "nested"), "not-loaded.json");
        File.WriteAllText(Path.Combine(directory, "bad.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "note.txt"), "text");
        return Lines("2", directory, "2", directory, "0");
    });
    h.Run();
    Require(h.App.Session.Scenes.Count == 2, "Папка должна добавлять только два корректных файла без рекурсии и дублей.");
    Require(h.Text.Contains("добавлено 2", StringComparison.Ordinal) && h.Text.Contains("ошибок 1", StringComparison.Ordinal), "Нет сводки частичной загрузки.");
}

static void CheckSettingsEditing()
{
    using var h = new Harness(Lines("3", "1", "11", "3", "2", "2999", "3500,5", "3", "0", "250,25", "4", "500", "7", "", "0", "0"));
    h.Run();
    var settings = new SolverSettingsJson().Load(Path.Combine(h.Root, "solver-settings.json"));
    Require(settings.Radars.RadarCount == 3 && settings.Radars.Radius == 3500.5, "Потеряны параметры радаров.");
    Require(settings.Discretization.CandidateStep == 250.25 && settings.Discretization.CoverageStep == 500, "Неверные шаги.");
    Require(h.Text.Contains("положительное конечное число", StringComparison.Ordinal), "Нет понятной ошибки нулевого шага.");
}

static void CheckInvalidSettingsLoad()
{
    using var h = new Harness(root =>
    {
        File.WriteAllText(Path.Combine(root, "bad-settings.json"), "{\"radius\":5}");
        return Lines("8", "3", "6", "bad-settings.json", "0", "0");
    });
    h.Run();
    Require(h.App.Session.Settings == ExampleSettings(), "Ошибка загрузки не должна менять настройки.");
}

static void CheckSettingsOverwrite()
{
    using var h = new Harness(root =>
    {
        new SolverSettingsJson().Save(new(), Path.Combine(root, "settings.json"));
        return Lines("8", "3", "7", "settings.json", "", "7", "settings.json", "да", "0", "0");
    });
    int confirmations = 0;
    h.Output.OnWrite = text =>
    {
        if (text.Contains("Заменить существующий файл", StringComparison.Ordinal))
        {
            confirmations++;
            Require(new SolverSettingsJson().Load(Path.Combine(h.Root, "settings.json")).Radars.RadarCount == 1,
                "Отказ в первом диалоге должен оставить файл неизменным до второго подтверждения.");
        }
    };
    h.Run();
    Require(confirmations == 2 && new SolverSettingsJson().Load(Path.Combine(h.Root, "settings.json")) == ExampleSettings(), "Явная замена не выполнена.");
}

static void CheckBatchFailure()
{
    using var h = new Harness(root =>
    {
        var large = JsonNode.Parse(File.ReadAllText(FixturePath()))!;
        large["bounds"]!["max"]!["x"] = 20000;
        large["bounds"]!["max"]!["y"] = 20000;
        File.WriteAllText(Path.Combine(root, "large.json"), large.ToJsonString());
        CopyScene(root, "normal.json");
        return Lines("1", "large.json", "1", "normal.json", "4", "", "0");
    });
    h.App.Session.SetSettings(ExampleSettings() with { Discretization = ExampleSettings().Discretization with { MaxGridCells = 150 } });
    h.Run();
    Require(h.App.Session.Scenes[0].Result is null && h.App.Session.Scenes[1].Result is not null, "Ошибка первой сцены не должна мешать второй.");
    Require(Results(h.App.Session.OutputDirectory).Length == 1 && h.Text.Contains("ошибок 1", StringComparison.Ordinal), "Неверная сводка серии.");
}

static void CheckDuplicateNames()
{
    using var h = new Harness(root => Lines("1", CopyScene(Path.Combine(root, "one")), "1", CopyScene(Path.Combine(root, "two")), "4", "", "0"));
    h.App.Session.SetSettings(ExampleSettings());
    h.Run();
    var files = Results(h.App.Session.OutputDirectory);
    Require(files.Length == 2 && files.Select(Path.GetFileName).Distinct().Count() == 2, "Одинаковые исходные имена должны получить разные выходные файлы.");
}

static void CheckOldResults()
{
    using var h = new Harness(Lines("8", "4", "", "3", "5", "4", "", "", "0", "4", "", "5", "1", "0"));
    h.Run();
    var entry = h.App.Session.Scenes.Single();
    Require(entry.Result is not null && entry.Result.Discretization.MaxCandidatePositions == 10000, "Неудачный пересчёт уничтожил прежний результат.");
    Require(entry.SavedPath is not null && File.Exists(entry.SavedPath) && !entry.UsesSettings(h.App.Session.Settings), "Прежний результат должен оставаться сохранённым и отмеченным как устаревший.");
    Require(h.Text.Contains("ранее рассчитанный результат с другими настройками", StringComparison.Ordinal), "Старый результат нельзя выдавать за новый.");
}

static void CheckCoverageExport()
{
    using var h = new Harness(Lines("8", "4", "", "6", "2", "3", "", "0", "0"));
    h.Run();
    var files = Results(h.App.Session.OutputDirectory);
    Require(files.Length == 2, "Повторный экспорт должен создавать новый файл.");
    int withGrid = files.Count(path =>
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("solver").TryGetProperty("coverageGrid", out _);
    });
    Require(withGrid == 1, "Карты должны добавиться только при повторном экспорте.");
}

static void CheckCancellationAndRetry()
{
    using var h = new Harness(Lines("8", "4", "", "4", "", "0"));
    int starts = 0;
    h.Output.OnWrite = text =>
    {
        if (text.Contains("Подготовка сетки наблюдения", StringComparison.Ordinal) && ++starts == 1)
            Require(h.Cancellation.RequestCancellation(), "Нет активной операции для отмены.");
    };
    h.Run();
    Require(starts == 2 && h.App.Session.Scenes.Single().Result is not null, "После отмены должен работать повторный расчёт.");
    Require(Results(h.App.Session.OutputDirectory).Length == 1 && h.Text.Contains("Расчёт отменён", StringComparison.Ordinal), "Отменённый расчёт не должен сохранять частичный результат.");
    Require(!h.Cancellation.RequestCancellation(), "После операции токен должен освобождаться.");
}

static void CheckBatchCancellation()
{
    using var h = new Harness(root => Lines("1", CopyScene(root, "one.json"), "1", CopyScene(root, "two.json"), "4", "", "0"));
    h.App.Session.SetSettings(ExampleSettings());
    int starts = 0;
    h.Output.OnWrite = text =>
    {
        if (text.Contains("Подготовка сетки наблюдения", StringComparison.Ordinal) && ++starts == 2) h.Cancellation.RequestCancellation();
    };
    h.Run();
    Require(h.App.Session.Scenes[0].SavedPath is not null && h.App.Session.Scenes[1].Result is null, "Состояния серии после отмены неверны.");
    Require(Results(h.App.Session.OutputDirectory).Length == 1, "Результат первой сцены должен остаться на диске.");
}

static void CheckSavingRecovery()
{
    using var h = new Harness(Lines("8", "4", "", "6", "3", "", "0", "0"));
    string? blockedPath = null;
    h.Output.OnWrite = text =>
    {
        if (text.Contains("Оптимум дискретной модели найден", StringComparison.Ordinal) && blockedPath is null)
        {
            string run = Directory.GetDirectories(h.App.Session.OutputDirectory).Single();
            blockedPath = Path.Combine(run, "001-scene-2d.solved.json");
            File.WriteAllText(blockedPath, "sentinel"); // Имитируем конфликт файла между расчётом и сохранением.
        }
    };
    h.Run();
    Require(blockedPath is not null && File.ReadAllText(blockedPath) == "sentinel", "Существующий файл был перезаписан.");
    var entry = h.App.Session.Scenes.Single();
    Require(entry.Result is not null && entry.SavedPath is not null && entry.SavedPath != blockedPath, "Результат не удалось повторно сохранить.");
    using var saved = JsonDocument.Parse(File.ReadAllText(entry.SavedPath!));
    Require(saved.RootElement.GetProperty("radars").GetArrayLength() == 2 && h.Text.Contains("Результат остался в памяти", StringComparison.Ordinal), "Нет восстановления после ошибки сохранения.");
}

static void CheckRemoval()
{
    using var h = new Harness(Lines("8", "4", "", "7", "1", "0"));
    h.Run();
    Require(h.App.Session.Scenes.Count == 0 && Results(h.App.Session.OutputDirectory).Length == 1 && File.Exists(FixturePath()),
        "Удаление из сеанса должно сохранять файлы на диске.");
}

static void CheckOutputFailure()
{
    using var h = new Harness(Lines("8", "4", "", "0"));
    File.WriteAllText(h.App.Session.OutputDirectory, "not a directory");
    Require(h.Run() == 0 && h.App.Session.Scenes.Single().Result is null, "Ошибка папки должна вернуть меню до расчёта.");
    Require(File.ReadAllText(h.App.Session.OutputDirectory) == "not a directory" && h.Text.Contains("Ошибка:", StringComparison.Ordinal), "Файл не должен заменяться папкой.");
}

static void CheckRealProcess()
{
    using var h = new Harness("");
    var help = RunProcess(h.Root, "", "--help");
    Require(help.Code == 0 && help.Output.Contains("интерактивная консоль", StringComparison.Ordinal), "Справка настоящего процесса не работает.");
    Require(RunProcess(h.Root, "", "--bad").Code == 2, "Неверный код завершения настоящего процесса.");
    var workflow = RunProcess(h.Root, Lines("8", "4", "", "5", "1", "0"));
    Require(workflow.Code == 0 && workflow.Output.Contains("Координаты радаров", StringComparison.Ordinal), "Полный диалог настоящего процесса не прошёл: " + workflow.Output);
    Require(Results(Path.Combine(h.Root, "Result")).Length == 1, "Настоящий процесс не сохранил результат.");
}

static (int Code, string Output) RunProcess(string workingDirectory, string input, params string[] arguments)
{
    // Тест запускается из bin/<Configuration>/net10.0; определяем соседнюю сборку приложения.
    string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
    string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string app = Path.Combine(repository, "src", "RadarPlacement.Console", "bin", configuration, "net10.0", "RadarPlacement.Console.dll");
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardInputEncoding = new UTF8Encoding(false),
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    start.ArgumentList.Add(app);
    foreach (string argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить приложение.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    process.StandardInput.Write(input);
    process.StandardInput.Close();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    try { process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult(); }
    catch (OperationCanceledException)
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
        throw new InvalidOperationException("Консоль не завершилась за 30 секунд.");
    }
    return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
}

internal sealed class Harness : IDisposable
{
    private readonly string _base = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TestArtifacts"));
    internal string Root { get; }
    internal HookWriter Output { get; } = new();
    internal string Text => Output.ToString();
    internal OperationCancellation Cancellation { get; } = new();
    internal ConsoleApplication App { get; }
    internal Harness(string input, IConsoleTerminal? terminal = null) : this(_ => input, terminal) { }
    internal Harness(Func<string, string> createInput, IConsoleTerminal? terminal = null)
    {
        Root = Path.Combine(_base, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        App = new(new StringReader(createInput(Root)), Output, Root,
            Path.Combine(AppContext.BaseDirectory, "Fixtures"), Cancellation, terminal);
    }
    internal int Run(params string[] args) => App.RunAsync(args).GetAwaiter().GetResult();
    public void Dispose()
    {
        Cancellation.Dispose();
        Output.Dispose();
        string resolved = Path.GetFullPath(Root);
        if (!resolved.StartsWith(_base + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Путь очистки вышел за пределы тестового каталога.");
        Directory.Delete(resolved, recursive: true);
    }
}

internal sealed class HookWriter : StringWriter
{
    internal Action<string>? OnWrite { get; set; }
    public override void Write(string? value) { base.Write(value); if (value is not null) OnWrite?.Invoke(value); }
    public override void WriteLine(string? value)
    {
        // Не вызываем base.WriteLine(value), который может повторно обратиться к Write
        // и вызвать обработчик дважды. Сигнал наблюдения соответствует ровно одной строке.
        base.Write(value);
        base.Write(Environment.NewLine);
        if (value is not null) OnWrite?.Invoke(value);
    }
}
