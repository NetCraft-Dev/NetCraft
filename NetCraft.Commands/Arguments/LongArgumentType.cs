using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Arguments;

//LongArgumentType 长整型参数类型对应原版com.mojang.brigadier.arguments.LongArgumentType
//限定min/max范围并解析long值越界抛LongTooLow/High
public sealed class LongArgumentType : ArgumentType<long>
{
    private static readonly IReadOnlyList<string> _examples = new[] { "0", "123", "-123" };

    private readonly long _minimum;
    private readonly long _maximum;

    private LongArgumentType(long minimum, long maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    public static LongArgumentType LongArg() => LongArg(long.MinValue);

    public static LongArgumentType LongArg(long min) => LongArg(min, long.MaxValue);

    public static LongArgumentType LongArg(long min, long max) => new(min, max);

    public static long GetLong<S>(CommandContext<S> context, string name)
    {
        return context.GetArgument<long>(name);
    }

    public long Minimum => _minimum;
    public long Maximum => _maximum;

    public long Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var result = reader.ReadLong();
        if (result < _minimum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.LongTooLow().CreateWithContext(reader, result, _minimum);
        }
        if (result > _maximum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.LongTooHigh().CreateWithContext(reader, result, _maximum);
        }
        return result;
    }

    public IReadOnlyList<string> Examples => _examples;

    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (obj is not LongArgumentType that)
        {
            return false;
        }
        return _maximum == that._maximum && _minimum == that._minimum;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return 31 * _minimum.GetHashCode() + _maximum.GetHashCode();
        }
    }

    public override string ToString()
    {
        if (_minimum == long.MinValue && _maximum == long.MaxValue)
        {
            return "longArg()";
        }
        if (_maximum == long.MaxValue)
        {
            return "longArg(" + _minimum + ")";
        }
        return "longArg(" + _minimum + ", " + _maximum + ")";
    }
}
