namespace RadarPlacement.ConsoleApp;

/// <summary>
/// Минимальная граница между диалогом и настоящим окном консоли.
/// В проверках терминал заменяется объектом, который хранит кадры и выдаёт нажатия клавиш.
/// Перенаправленные потоки вообще не используют этот интерфейс.
/// </summary>
internal interface IConsoleTerminal
{
    (int Width, int Height) Size { get; }
    void Draw(ConsoleFrame frame);
    ConsoleKeyInfo? ReadKey();
}

/// <summary>Готовый экран и необязательное положение курсора ввода; координаты отсчитываются от окна.</summary>
internal sealed record ConsoleFrame(string[] Lines, int? CursorColumn = null, int? CursorRow = null);

/// <summary>
/// Перерисовывает только изменившиеся строки в пределах видимого окна Windows.
/// Последние столбец и строка оставлены свободными, чтобы вывод не прокручивал окно.
/// Очистка нужна только при первом кадре или изменении размеров, а не при каждом отчёте ядра.
/// </summary>
internal sealed class ConsoleTerminal : IConsoleTerminal
{
    private string[] _previous = [];
    private (int Width, int Height) _previousSize;
    private (int? Column, int? Row) _previousCursor;

    public (int Width, int Height) Size =>
        (Math.Max(1, System.Console.WindowWidth - 1), Math.Max(1, System.Console.WindowHeight - 1));

    public void Draw(ConsoleFrame frame)
    {
        try
        {
            var size = Size;
            var cursor = (frame.CursorColumn, frame.CursorRow);
            // Проверка размера выполняется и в ожидании ввода. Не отправляем в терминал
            // повторные команды скрытия/показа курсора, если весь кадр остался прежним.
            if (_previousSize == size && _previousCursor == cursor && _previous.SequenceEqual(frame.Lines)) return;
            System.Console.CursorVisible = false;
            if (_previousSize != size)
            {
                System.Console.Clear();
                _previous = [];
                _previousSize = size;
            }
            int count = Math.Min(size.Height, Math.Max(_previous.Length, frame.Lines.Length));
            for (int row = 0; row < count; row++)
            {
                string line = row < frame.Lines.Length ? frame.Lines[row] : "";
                line = line[..Math.Min(line.Length, size.Width)];
                if (row < _previous.Length && line == _previous[row]) continue;
                System.Console.SetCursorPosition(0, System.Console.WindowTop + row);
                System.Console.Write(line.PadRight(size.Width));
            }
            _previous = frame.Lines;
            _previousCursor = cursor;
            if (frame.CursorColumn is int column && frame.CursorRow is int cursorRow)
            {
                System.Console.SetCursorPosition(Math.Clamp(column, 0, size.Width - 1),
                    System.Console.WindowTop + Math.Clamp(cursorRow, 0, size.Height - 1));
                System.Console.CursorVisible = true;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // Окно могли уменьшить между чтением размера и перемещением курсора.
            // Следующий кадр полностью восстановит содержимое в новых границах.
            _previousSize = default;
        }
    }

    public ConsoleKeyInfo? ReadKey()
    {
        if (System.Console.KeyAvailable) return System.Console.ReadKey(intercept: true);
        // Короткое ожидание позволяет обновлять размер окна даже без ввода пользователя.
        // Оно не относится к вычислительному ядру и не удерживает блокировку отрисовки.
        Thread.Sleep(100);
        return null;
    }

    /// <summary>Оставляет курсор ниже интерфейса для приглашения командной оболочки после выхода.</summary>
    internal void RestoreCursor()
    {
        System.Console.CursorVisible = true;
        System.Console.SetCursorPosition(0, System.Console.WindowTop + System.Console.WindowHeight - 1);
    }
}
