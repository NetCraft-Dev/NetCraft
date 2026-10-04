namespace NetCraft.Util.Parsing.Packrat;

//错误收集器对应原版net.minecraft.util.parsing.packrat.ErrorCollector
//store记录失败位置建议和原因finish标记解析结束位置
public interface ErrorCollector<S>
{
    void Store(int cursor, SuggestionSupplier<S>? suggestions, object? reason);

    void Finish(int finalCursor);

    void Store(int cursor, object? reason)
        => Store(cursor, SuggestionSuppliers.Empty<S>(), reason);
}

//Nop空实现丢弃所有错误
public sealed class NopErrorCollector<S> : ErrorCollector<S>
{
    public static NopErrorCollector<S> Instance { get; } = new();

    private NopErrorCollector() { }

    public void Store(int cursor, SuggestionSupplier<S>? suggestions, object? reason) { }

    public void Finish(int finalCursor) { }
}

//LongestOnly只保留最长解析位置的错误
public sealed class LongestOnlyErrorCollector<S> : ErrorCollector<S>
{
    private int _nextErrorEntry;
    private MutableErrorEntry[] _entries = new MutableErrorEntry[16];
    private int _lastCursor = -1;

    private void DiscardErrorsFromShorterParse(int cursor)
    {
        if (cursor > _lastCursor)
        {
            _lastCursor = cursor;
            _nextErrorEntry = 0;
        }
    }

    public void Finish(int finalCursor)
        => DiscardErrorsFromShorterParse(finalCursor);

    public void Store(int cursor, SuggestionSupplier<S>? suggestions, object? reason)
    {
        DiscardErrorsFromShorterParse(cursor);
        if (cursor == _lastCursor)
        {
            AddErrorEntry(suggestions, reason);
        }
    }

    private void AddErrorEntry(SuggestionSupplier<S>? suggestions, object? reason)
    {
        var currentSize = _entries.Length;
        if (_nextErrorEntry >= currentSize)
        {
            var newSize = UtilGrowByHalf(currentSize, _nextErrorEntry + 1);
            var newEntries = new MutableErrorEntry[newSize];
            Array.Copy(_entries, newEntries, currentSize);
            _entries = newEntries;
        }
        var entryIndex = _nextErrorEntry++;
        var entry = _entries[entryIndex];
        if (entry is null)
        {
            entry = new MutableErrorEntry();
            _entries[entryIndex] = entry;
        }
        entry.Suggestions = suggestions;
        entry.Reason = reason;
    }

    public List<ErrorEntry<S>> Entries()
    {
        var errorCount = _nextErrorEntry;
        if (errorCount == 0) return new();
        var result = new List<ErrorEntry<S>>(errorCount);
        for (var i = 0; i < errorCount; i++)
        {
            var entry = _entries[i];
            result.Add(new ErrorEntry<S>(_lastCursor, entry.Suggestions, entry.Reason));
        }
        return result;
    }

    public int Cursor() => _lastCursor;

    //growByHalf对应原版Util.growByHalf按一半增长
    private static int UtilGrowByHalf(int current, int needed)
        => Math.Max(current + (current >> 1), needed);

    private sealed class MutableErrorEntry
    {
        public SuggestionSupplier<S>? Suggestions = SuggestionSuppliers.Empty<S>();
        public object? Reason = "empty";
    }
}
