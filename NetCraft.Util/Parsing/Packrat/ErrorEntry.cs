namespace NetCraft.Util.Parsing.Packrat;

//Error entry, maps to vanilla net.minecraft.util.parsing.packrat.ErrorEntry
//Records the parse failure position, suggestions and reason for the error collector to aggregate
public sealed class ErrorEntry<S>
{
    public int Cursor { get; }
    public SuggestionSupplier<S>? Suggestions { get; }
    public object? Reason { get; }

    public ErrorEntry(int cursor, SuggestionSupplier<S>? suggestions, object? reason)
    {
        Cursor = cursor;
        Suggestions = suggestions;
        Reason = reason;
    }

    public override string ToString() => $"ErrorEntry[{Cursor}, {Reason}]";
}
