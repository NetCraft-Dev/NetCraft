using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Arguments;

//FloatArgumentType 单精度参数类型对应原版com.mojang.brigadier.arguments.FloatArgumentType
//限定min/max范围并解析float值越界抛FloatTooLow/High
public sealed class FloatArgumentType : ArgumentType<float>
{
    private static readonly IReadOnlyList<string> _examples = new[] { "0", "1.2", ".5", "-1", "-.5", "-1234.56" };

    private readonly float _minimum;
    private readonly float _maximum;

    private FloatArgumentType(float minimum, float maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    public static FloatArgumentType FloatArg() => FloatArg(-float.MaxValue);

    public static FloatArgumentType FloatArg(float min) => FloatArg(min, float.MaxValue);

    public static FloatArgumentType FloatArg(float min, float max) => new(min, max);

    public static float GetFloat<S>(CommandContext<S> context, string name)
    {
        return context.GetArgument<float>(name);
    }

    public float Minimum => _minimum;
    public float Maximum => _maximum;

    public float Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var result = reader.ReadFloat();
        if (result < _minimum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.FloatTooLow().CreateWithContext(reader, result, _minimum);
        }
        if (result > _maximum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.FloatTooHigh().CreateWithContext(reader, result, _maximum);
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
        if (obj is not FloatArgumentType that)
        {
            return false;
        }
        return _maximum == that._maximum && _minimum == that._minimum;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (int)(31 * _minimum + _maximum);
        }
    }

    public override string ToString()
    {
        if (_minimum == -float.MaxValue && _maximum == float.MaxValue)
        {
            return "float()";
        }
        if (_maximum == float.MaxValue)
        {
            return "float(" + _minimum + ")";
        }
        return "float(" + _minimum + ", " + _maximum + ")";
    }
}
