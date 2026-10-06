namespace NetCraft.Util.Parsing.Packrat;

//Error collector, maps to vanilla net.minecraft.util.parsing.packrat.ErrorCollector
//store records the failure position, suggestions and reason, finish marks the parse end position
public interface ErrorCollector<S>
{
    void Store(int cursor, SuggestionSupplier<S>? suggestions, object? reason);

    void Finish(int finalCursor);

    void Store(int cursor, object? reason)
        => Store(cursor, SuggestionSuppliers.Empty<S>(), reason);
}

//Nop empty implementation discarding all errors
public sealed class NopErrorCollector<S> : ErrorCollector<S>
{
    public static NopErrorCollector<S> Instance { get; } = new();

    private NopErrorCollector() { }

    public void Store(int cursor, SuggestionSupplier<S>? suggestions, object? reason) { }

    public void Finish(int finalCursor) { }
}

//LongestOnly keeps only errors at the furthest parse position
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

    //growByHalf maps to vanilla Util.growByHalf, grows by half
    private static int UtilGrowByHalf(int current, int needed)
        => Math.Max(current + (current >> 1), needed);

    private sealed class MutableErrorEntry
    {
        public SuggestionSupplier<S>? Suggestions = SuggestionSuppliers.Empty<S>();
        public object? Reason = "empty";
    }
}
