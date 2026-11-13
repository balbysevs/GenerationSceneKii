using System.Text;

namespace RadarPlacement.ConsoleApp;

internal sealed partial class ConsoleUi
{
    private readonly List<string> _body = [];
    private string _title = "RadarPlacement";
    private string? _progress;

    /// <summary>Начинает новый экран. Заголовок состояния берётся из сеанса при каждой отрисовке.</summary>
    internal void BeginScreen(string title)
    {
        lock (_writeLock)
        {
            _title = title;
            _body.Clear();
            _progress = null;
            if (terminal is not null) Draw();
            else
            {
                output.WriteLine();
                foreach (string line in header?.Invoke() ?? []) output.WriteLine(Safe(line));
                output.WriteLine("── " + Safe(title) + " ──");
            }
        }
    }

    /// <summary>Заменяет строку прогресса; в текстовом журнале сохраняет прежние отчёты.</summary>
    internal void Progress(string text)
    {
        lock (_writeLock)
        {
            if (terminal is null) { output.WriteLine(text); return; }
            _progress = Safe(text);
            Draw(tail: true);
        }
    }

    /// <summary>
    /// Не даёт следующему меню мгновенно стереть результат или ошибку.
    /// Дополнительное нажатие требуется только в настоящем оконном терминале.
    /// </summary>
    internal void Pause()
    {
        if (terminal is not null) ReadScreen("Enter — продолжить", pause: true);
    }

    /// <summary>
    /// Ввод редактируется в одной строке: длинный путь сдвигается по горизонтали,
    /// не вытесняя сводку за верхнюю границу окна. PgUp/PgDn листают содержимое экрана.
    /// Отрисовка синхронная; фоновые отчёты не остаются в очереди после завершения расчёта.
    /// </summary>
    private string ReadScreen(string prompt, bool pause = false)
    {
        var value = new StringBuilder();
        int cursor = 0, page = 0;
        while (true)
        {
            int pages;
            lock (_writeLock) pages = Draw(prompt, value.ToString(), cursor, page, pause: pause);
            page = Math.Clamp(page, 0, pages - 1);
            if (terminal!.ReadKey() is not { } key) continue;
            switch (key.Key)
            {
                case ConsoleKey.PageUp: page = Math.Max(0, page - 1); break;
                case ConsoleKey.PageDown: page = Math.Min(pages - 1, page + 1); break;
                case ConsoleKey.Enter: return value.ToString().Trim();
                case ConsoleKey.Z when key.Modifiers.HasFlag(ConsoleModifiers.Control): throw new EndOfInputException();
                case ConsoleKey.LeftArrow: cursor = Math.Max(0, cursor - 1); break;
                case ConsoleKey.RightArrow: cursor = Math.Min(value.Length, cursor + 1); break;
                case ConsoleKey.Home: cursor = 0; break;
                case ConsoleKey.End: cursor = value.Length; break;
                case ConsoleKey.Backspace when !pause && cursor > 0: value.Remove(--cursor, 1); break;
                case ConsoleKey.Delete when !pause && cursor < value.Length: value.Remove(cursor, 1); break;
                case ConsoleKey.Escape when !pause: value.Clear(); cursor = 0; break;
                default:
                    if (!pause && !char.IsControl(key.KeyChar)) value.Insert(cursor++, key.KeyChar);
                    break;
            }
        }
    }

    /// <summary>
    /// Делит тело на страницы по текущему размеру окна. Сводка, подсказка и ввод
    /// не участвуют в прокрутке. Длинные строки тела переносятся, строки сводки сокращаются.
    /// Во время работы виден конец журнала; после операции весь журнал можно пролистать.
    /// </summary>
    private int Draw(string? prompt = null, string value = "", int cursor = 0,
        int page = 0, bool tail = false, bool pause = false)
    {
        if (terminal is null) return 1;
        var size = terminal.Size;
        int width = Math.Max(1, size.Width), height = Math.Max(1, size.Height);
        var lines = new List<string>();
        // В очень низком окне оставляем хотя бы строку содержания и строку ввода.
        lines.AddRange((header?.Invoke() ?? []).Take(Math.Max(0, height - 5)).Select(s => Clip(s, width)));
        if (lines.Count < height - 2) lines.Add(Clip("── " + _title + " ──", width));
        int footerRows = prompt is null ? 1 : Math.Min(3, Math.Max(1, height - lines.Count - 1));
        int capacity = Math.Max(1, height - lines.Count - footerRows);
        var content = _body.SelectMany(s => Wrap(s, width)).ToList();
        if (_progress is not null) content.AddRange(Wrap(_progress, width));
        int pages = Math.Max(1, (content.Count + capacity - 1) / capacity);
        int start = tail ? Math.Max(0, content.Count - capacity) : Math.Clamp(page, 0, pages - 1) * capacity;
        lines.AddRange(content.Skip(start).Take(capacity));
        while (lines.Count < height - footerRows) lines.Add("");
        string navigation = pages > 1 ? $"PgUp/PgDn — страницы {Math.Clamp(page + 1, 1, pages)}/{pages}" : "";
        if (prompt is null)
        {
            lines.Add(_progress is null ? "" : Clip("Ctrl+C — отмена операции", width));
            terminal.Draw(new(lines.Take(height).ToArray()));
        }
        else
        {
            if (footerRows >= 3) lines.Add(Clip(navigation, width));
            if (footerRows >= 2) lines.Add(Clip(prompt, width));
            // Позиция курсора должна оставаться внутри строки, в том числе при вставке длинного пути.
            int offset = Math.Max(0, cursor - Math.Max(0, width - 3));
            string inputLine = pause ? (footerRows == 1 ? prompt : "") : "> " + value[offset..];
            int inputRow = lines.Count;
            lines.Add(Clip(inputLine, width));
            terminal.Draw(new(lines.Take(height).ToArray(), pause ? null : Math.Min(width - 1, 2 + cursor - offset),
                pause ? null : Math.Min(height - 1, inputRow)));
        }
        return pages;
    }

    private static string Clip(string text, int width)
    {
        text = Safe(text);
        return text.Length <= width ? text : text[..Math.Max(0, width - 1)] + "…";
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        if (text.Length == 0) { yield return ""; yield break; }
        for (int index = 0; index < text.Length; index += width)
            yield return text.Substring(index, Math.Min(width, text.Length - index));
    }
}
