namespace NetCraft.Commands.Context;

//ParsedArgument maps to vanilla com.mojang.brigadier.context.ParsedArgument
//Holds a StringRange marking the parsed fragment and the parse result
//Split into a non-generic base and a generic subclass, mirroring Java's ParsedArgument<S,?> wildcard
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

//ParsedArgument<S,T> strongly typed subclass holding the concrete parse result, checked by CommandContext.getArgument against the Class
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
