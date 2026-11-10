using System.Numerics;

namespace RadarPlacement.Core.Optimization;

/// <summary>
/// Точное решение конечной задачи максимального покрытия методом ветвей и границ.
/// Кандидат — битовая маска видимых ячеек. Объединение масок — побитовое ИЛИ.
/// Благодаря одинаковой площади ячеек максимизация числа единиц равносильна максимизации площади.
/// </summary>
internal sealed class MaximumCoverageSearch
{
    private readonly ulong[][] _masks;
    private readonly int[] _originalIndices;
    private readonly ulong[][] _suffixUnion;
    private readonly ulong[][] _workingUnion;
    private readonly int _selectionSize;
    private readonly int _observableCount;
    private readonly CancellationToken _cancellationToken;
    private readonly Action<long, int>? _report;
    private readonly int[] _currentSelection;
    private int[] _bestSelection;
    private int _bestCount;
    private long _visited;
    private long _pruned;

    internal MaximumCoverageSearch(ulong[][] masks, int selectionSize, int observableCount,
        CancellationToken cancellationToken, Action<long, int>? report = null)
    {
        _cancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        _selectionSize = selectionSize;
        _observableCount = observableCount;
        _report = report;

        // Хорошие кандидаты в начале обычно позволяют быстрее получить сильную нижнюю оценку.
        // Исходный индекс служит вторым ключом: одинаковый ввод даёт одинаковый результат.
        var counts = new int[masks.Length];
        for (int i = 0; i < masks.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            counts[i] = Count(masks[i]);
        }
        _originalIndices = Enumerable.Range(0, masks.Length)
            .OrderByDescending(i => counts[i]).ThenBy(i => i).ToArray();
        _masks = _originalIndices.Select(i => masks[i]).ToArray();
        _currentSelection = new int[selectionSize];
        _bestSelection = new int[selectionSize];
        int words = masks[0].Length;
        _workingUnion = Enumerable.Range(0, selectionSize + 1).Select(_ => new ulong[words]).ToArray();

        // suffix[i] объединяет ВСЕ маски с номерами >= i.
        // Даже неограниченное число оставшихся радаров не может покрыть больше этой маски.
        _suffixUnion = new ulong[masks.Length + 1][];
        _suffixUnion[^1] = new ulong[words];
        for (int i = masks.Length - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _suffixUnion[i] = new ulong[words];
            UnionInto(_suffixUnion[i + 1], _masks[i], _suffixUnion[i]);
        }
    }

    internal SearchResult Run()
    {
        BuildGreedyLowerBound();
        _report?.Invoke(0, _bestCount);
        _cancellationToken.ThrowIfCancellationRequested();

        // Если уже покрыты все наблюдаемые ячейки, более хорошее решение невозможно.
        // Это доказанный максимум, даже если его удалось получить начальным жадным выбором.
        if (_bestCount < _observableCount)
        {
            Array.Clear(_workingUnion[0]);
            Search(start: 0, depth: 0, coveredCount: 0);
        }
        _cancellationToken.ThrowIfCancellationRequested();
        var original = _bestSelection.Select(index => _originalIndices[index]).Order().ToArray();
        return new SearchResult(original, _bestCount, _visited, _pruned);
    }

    // Жадный выбор служит только стартовым допустимым решением — нижней оценкой.
    // Сам по себе он не подтверждает оптимальность: после него выполняется точный поиск.
    private void BuildGreedyLowerBound()
    {
        var used = new bool[_masks.Length];
        var union = _workingUnion[0];
        for (int slot = 0; slot < _selectionSize; slot++)
        {
            int best = -1;
            int largestGain = -1;
            for (int i = 0; i < _masks.Length; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (used[i]) continue;
                int gain = CountNew(_masks[i], union);
                if (gain <= largestGain) continue;
                best = i;
                largestGain = gain;
            }
            used[best] = true;
            _bestSelection[slot] = best;
            _bestCount += largestGain;
            UnionInto(union, _masks[best], union);
        }
    }

    private void Search(int start, int depth, int coveredCount)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _visited++;
        if ((_visited & 1023) == 0) _report?.Invoke(_visited, _bestCount);
        if (depth == _selectionSize)
        {
            if (coveredCount > _bestCount)
            {
                _bestCount = coveredCount;
                Array.Copy(_currentSelection, _bestSelection, _selectionSize);
                _report?.Invoke(_visited, _bestCount);
            }
            return;
        }

        int slots = _selectionSize - depth;
        var union = _workingUnion[depth];
        int upperByUnion = coveredCount + CountNew(_suffixUnion[start], union);
        if (upperByUnion <= _bestCount)
        {
            _pruned++;
            return;
        }

        // Вторая верхняя оценка учитывает число свободных радаров: суммируем slots
        // наибольших индивидуальных добавок к ТЕКУЩЕМУ покрытию. Эти добавки могут
        // перекрываться между собой, поэтому сумма завышает результат, но никогда не занижает.
        Span<int> largestGains = stackalloc int[10];
        largestGains.Clear();
        for (int i = start; i < _masks.Length; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            int gain = CountNew(_masks[i], union);
            for (int j = 0; j < slots; j++)
                if (gain > largestGains[j])
                    (largestGains[j], gain) = (gain, largestGains[j]);
        }
        long upperByGains = coveredCount;
        for (int i = 0; i < slots; i++) upperByGains += largestGains[i];
        if (upperByGains <= _bestCount)
        {
            _pruned++;
            return;
        }

        // Возрастающие индексы перечисляют каждое сочетание ровно один раз.
        // Последняя допустимая первая позиция оставляет достаточно кандидатов для остальных слотов.
        for (int i = start; i <= _masks.Length - slots; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_bestCount == _observableCount) return;
            _currentSelection[depth] = i;
            var nextUnion = _workingUnion[depth + 1];
            UnionInto(union, _masks[i], nextUnion);
            int nextCount = coveredCount + CountNew(_masks[i], union);
            Search(i + 1, depth + 1, nextCount);
        }
    }

    internal static int Count(ulong[] mask)
    {
        int count = 0;
        foreach (ulong word in mask) count += BitOperations.PopCount(word);
        return count;
    }

    private static int CountNew(ulong[] candidate, ulong[] current)
    {
        int count = 0;
        for (int i = 0; i < candidate.Length; i++)
            count += BitOperations.PopCount(candidate[i] & ~current[i]);
        return count;
    }

    internal static void UnionInto(ulong[] first, ulong[] second, ulong[] destination)
    {
        for (int i = 0; i < first.Length; i++) destination[i] = first[i] | second[i];
    }
}

internal sealed record SearchResult(int[] Indices, int CoveredCount, long VisitedNodes, long PrunedNodes);
