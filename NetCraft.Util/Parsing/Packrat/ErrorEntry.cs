namespace NetCraft.Util.Parsing.Packrat;

//错误条目对应原版net.minecraft.util.parsing.packrat.ErrorEntry
//记录解析失败位置建议值和原因供错误收集器聚合
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
