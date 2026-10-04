using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Arguments;

//IntegerArgumentType 整数参数类型对应原版com.mojang.brigadier.arguments.IntegerArgumentType
//限定min/max范围并解析int值越界抛IntegerTooLow/High
public sealed class IntegerArgumentType : ArgumentType<int>
{
    private static readonly IReadOnlyList<string> _examples = new[] { "0", "123", "-123" };

    private readonly int _minimum;
    private readonly int _maximum;

    private IntegerArgumentType(int minimum, int maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    public static IntegerArgumentType Integer() => Integer(int.MinValue);

    public static IntegerArgumentType Integer(int min) => Integer(min, int.MaxValue);

    public static IntegerArgumentType Integer(int min, int max) => new(min, max);

    public static int GetInteger<S>(CommandContext<S> context, string name)
    {
        return context.GetArgument<int>(name);
    }

    public int Minimum => _minimum;
    public int Maximum => _maximum;

    public int Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var result = reader.ReadInt();
        if (result < _minimum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.IntegerTooLow().CreateWithContext(reader, result, _minimum);
        }
        if (result > _maximum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.IntegerTooHigh().CreateWithContext(reader, result, _maximum);
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
        if (obj is not IntegerArgumentType that)
        {
            return false;
        }
        return _maximum == that._maximum && _minimum == that._minimum;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return 31 * _minimum + _maximum;
        }
    }

    public override string ToString()
    {
        if (_minimum == int.MinValue && _maximum == int.MaxValue)
        {
            return "integer()";
        }
        if (_maximum == int.MaxValue)
        {
            return "integer(" + _minimum + ")";
        }
        return "integer(" + _minimum + ", " + _maximum + ")";
    }
}
