using System.Globalization;

namespace RadarPlacement.ConsoleApp;

/// <summary>
/// Диалог с двумя способами вывода: сменяемые экраны в терминале и обычные строки
/// при перенаправлении потоков. Числа, пути и правила ввода одинаковы в обоих режимах.
/// Построение кадров находится в соседней части класса ConsoleUi.Screen.cs.
/// </summary>
internal sealed partial class ConsoleUi(TextReader input, TextWriter output, string workingDirectory,
    IConsoleTerminal? terminal = null, Func<IReadOnlyList<string>>? header = null)
{
    private readonly object _writeLock = new();
    internal static CultureInfo Russian { get; } = CultureInfo.GetCultureInfo("ru-RU");

    internal void Line(string text = "")
    {
        lock (_writeLock)
        {
            if (terminal is null) output.WriteLine(text);
            else { _body.Add(Safe(text)); Draw(tail: true); }
        }
    }

    internal string Read(string prompt)
    {
        if (terminal is not null) return ReadScreen(prompt);
        lock (_writeLock) { output.Write(prompt); output.Flush(); }
        // EOF — обычное завершение, в том числе посередине подменю. Повторять запрос
        // при закрытом потоке бессмысленно: это создало бы бесконечный цикл.
        return input.ReadLine()?.Trim() ?? throw new EndOfInputException();
    }

    internal string? ReadPath(string prompt, string? defaultPath = null)
    {
        string suffix = defaultPath is null ? " [Enter — назад]: " : $" [{Safe(defaultPath)}; 0 — назад]: ";
        string text = Read(prompt + suffix);
        if (text == "0") return null;
        if (text.Length == 0)
        {
            if (defaultPath is null) return null;
            text = defaultPath;
        }
        // Путь, скопированный из Проводника, часто окружён кавычками.
        // Он передаётся файловому API, а не командной оболочке.
        if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
            text = text[1..^1];
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Путь не должен быть пустым.");
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(text), workingDirectory);
    }

    internal bool Confirm(string prompt) => Read(prompt + " [д/Н]: ").ToLowerInvariant() is "д" or "да" or "y" or "yes";

    internal long ReadInteger(string prompt, long current, long min, long max)
    {
        while (true)
        {
            string value = Read($"{prompt} [{current}; Enter — оставить]: ");
            if (value.Length == 0) return current;
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) && number >= min && number <= max)
                return number;
            Line($"Введите целое число от {min} до {max}.");
        }
    }

    internal double ReadNumber(string prompt, double current, double min, double max)
    {
        while (true)
        {
            string value = Read($"{prompt} [{current.ToString("G", Russian)}; Enter — оставить]: ");
            if (value.Length == 0) return current;
            // Разрешаем точку и десятичную запятую, но не разделители групп разрядов.
            if (double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                && double.IsFinite(number) && number >= min && number <= max)
                return number;
            Line(min == double.Epsilon && max == double.MaxValue
                ? "Введите положительное конечное число. Допустимы точка и запятая."
                : $"Введите конечное число от {min.ToString("G", Russian)} до {max.ToString("G", Russian)}. Допустимы точка и запятая.");
        }
    }

    internal static string Number(double value) => value.ToString("N2", Russian);
    internal static string Safe(string text) => string.Concat(text.Select(c => char.IsControl(c) ? ' ' : c));
}

internal sealed class EndOfInputException : Exception;
