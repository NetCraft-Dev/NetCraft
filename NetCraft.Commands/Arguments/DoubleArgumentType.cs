using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;

namespace NetCraft.Commands.Arguments;

//DoubleArgumentType 双精度参数类型对应原版com.mojang.brigadier.arguments.DoubleArgumentType
//限定min/max范围并解析double值越界抛DoubleTooLow/High
public sealed class DoubleArgumentType : ArgumentType<double>
{
    private static readonly IReadOnlyList<string> _examples = new[] { "0", "1.2", ".5", "-1", "-.5", "-1234.56" };

    private readonly double _minimum;
    private readonly double _maximum;

    private DoubleArgumentType(double minimum, double maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    public static DoubleArgumentType DoubleArg() => DoubleArg(-double.MaxValue);

    public static DoubleArgumentType DoubleArg(double min) => DoubleArg(min, double.MaxValue);

    public static DoubleArgumentType DoubleArg(double min, double max) => new(min, max);

    public static double GetDouble<S>(CommandContext<S> context, string name)
    {
        return context.GetArgument<double>(name);
    }

    public double Minimum => _minimum;
    public double Maximum => _maximum;

    public double Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var result = reader.ReadDouble();
        if (result < _minimum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.DoubleTooLow().CreateWithContext(reader, result, _minimum);
        }
        if (result > _maximum)
        {
            reader.SetCursor(start);
            throw CommandSyntaxException.BuiltInExceptions.DoubleTooHigh().CreateWithContext(reader, result, _maximum);
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
        if (obj is not DoubleArgumentType that)
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
        if (_minimum == -double.MaxValue && _maximum == double.MaxValue)
        {
            return "double()";
        }
        if (_maximum == double.MaxValue)
        {
            return "double(" + _minimum + ")";
        }
        return "double(" + _minimum + ", " + _maximum + ")";
    }
}
