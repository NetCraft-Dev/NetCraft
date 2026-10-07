using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//WorldCoordinate single-axis coordinate segment, maps to vanilla WorldCoordinate
//~ prefix means relative to the current value; blank means offset 0; absolute values may add 0.5 for center correction
public sealed record WorldCoordinate(bool Relative, double Value)
{
    //Get resolves the absolute value from a base value; relative segments add
    public double Get(double original) => Relative ? Value + original : Value;

    public bool IsRelative => Relative;

    //ParseDouble parses a single coordinate segment; ^ throws a mixed-type error; when center is true and an absolute value has no decimal point, adds 0.5 to align to the block center
    public static WorldCoordinate ParseDouble(StringReader reader, bool center)
    {
        if (reader.CanRead() && reader.Peek() == '^')
            throw Vec3Argument.ErrorMixedType.CreateWithContext(reader);
        if (!reader.CanRead())
            throw ErrorExpectedDouble.CreateWithContext(reader);
        var relative = IsRelativePrefix(reader);
        var start = reader.Cursor;
        var value = !reader.CanRead() || reader.Peek() == ' ' ? 0.0 : reader.ReadDouble();
        var number = reader.String[start..reader.Cursor];
        if (relative && number.Length == 0) return new WorldCoordinate(true, 0.0);
        if (!number.Contains('.') && !relative && center) value += 0.5;
        return new WorldCoordinate(relative, value);
    }

    //ParseInt parses an integer coordinate segment; relative segments allow decimals, absolute segments accept integers only
    public static WorldCoordinate ParseInt(StringReader reader)
    {
        if (reader.CanRead() && reader.Peek() == '^')
            throw Vec3Argument.ErrorMixedType.CreateWithContext(reader);
        if (!reader.CanRead())
            throw ErrorExpectedInt.CreateWithContext(reader);
        var relative = IsRelativePrefix(reader);
        double value;
        if (reader.CanRead() && reader.Peek() != ' ')
            value = relative ? reader.ReadDouble() : reader.ReadInt();
        else
            value = 0.0;
        return new WorldCoordinate(relative, value);
    }

    //IsRelativePrefix consumes an optional ~ prefix and returns whether it is relative
    private static bool IsRelativePrefix(StringReader reader)
    {
        if (reader.Peek() != '~') return false;
        reader.Skip();
        return true;
    }

    public static readonly SimpleCommandExceptionType ErrorExpectedDouble =
        new(new TranslatableMessage("argument.pos.missing.double"));

    public static readonly SimpleCommandExceptionType ErrorExpectedInt =
        new(new TranslatableMessage("argument.pos.missing.int"));
}
