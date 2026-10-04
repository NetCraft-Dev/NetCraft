namespace NetCraft.Commands.Context;

//ParsedArgument 已解析参数对应原版com.mojang.brigadier.context.ParsedArgument
//持StringRange标记解析片段范围与解析结果
//拆非泛型基类与泛型派生对应Java ParsedArgument<S,?>通配
public abstract class ParsedArgument<S>
{
    public StringRange Range { get; }

    protected ParsedArgument(StringRange range)
    {
        Range = range;
    }

    public abstract object? GetResult();

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o)) return true;
        if (o is not ParsedArgument<S> that) return false;
        return Range.Equals(that.Range) && Equals(GetResult(), that.GetResult());
    }

    public override int GetHashCode() => HashCode.Combine(Range, GetResult());
}

//ParsedArgument<S,T> 强类型派生持具体解析结果供CommandContext.getArgument按Class校验
public sealed class ParsedArgument<S, T> : ParsedArgument<S>
{
    private readonly T _result;

    public ParsedArgument(int start, int end, T result)
        : base(StringRange.Between(start, end))
    {
        _result = result;
    }

    public T Result => _result;

    public override object? GetResult() => _result;
}
