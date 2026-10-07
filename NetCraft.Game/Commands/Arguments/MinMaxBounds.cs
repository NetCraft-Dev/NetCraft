using System.Globalization;
using NetCraft.Codec;
using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//MinMaxBounds numeric range, maps to vanilla net.minecraft.advancements.predicates.MinMaxBounds
//Parsed from [min..max] syntax; a single value is min=max; fully empty is invalid; shared by the selector distance/level/rotation options
public static class MinMaxBounds
{
    public static readonly SimpleCommandExceptionType ErrorEmpty =
        new(new TranslatableMessage("argument.range.empty"));
    public static readonly SimpleCommandExceptionType ErrorSwapped =
        new(new TranslatableMessage("argument.range.swapped"));

    //'.' followed by '.' means the range separator and stops reading; a lone '.' is a decimal point
    private static bool IsAllowedInputChar(StringReader reader)
    {
        var c = reader.Peek();
        if (c is >= '0' and <= '9' or '-') return true;
        if (c == '.') return !(reader.CanRead(2) && reader.Peek(1) == '.');
        return false;
    }

    //ReadNumber reads a numeric text segment; a failed conversion throws the matching parse exception; an empty string returns null
    private static T? ReadNumber<T>(StringReader reader, Func<string, T> converter,
        DynamicCommandExceptionType parseError) where T : struct
    {
        var start = reader.Cursor;
        while (reader.CanRead() && IsAllowedInputChar(reader))
            reader.Skip();
        var number = reader.String[start..reader.Cursor];
        if (number.Length == 0) return null;
        try
        {
            return converter(number);
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            throw parseError.CreateWithContext(reader, number);
        }
    }

    //ReadBounds reads the range into min/max; without a '..' separator max=min; an internal exception rolls back the cursor then rethrows at the start position
    private static (T? Min, T? Max) ReadBounds<T>(StringReader reader, Func<string, T> converter,
        DynamicCommandExceptionType parseError) where T : struct
    {
        if (!reader.CanRead())
            throw ErrorEmpty.CreateWithContext(reader);
        var start = reader.Cursor;
        try
        {
            var min = ReadNumber(reader, converter, parseError);
            T? max;
            if (reader.CanRead(2) && reader.Peek() == '.' && reader.Peek(1) == '.')
            {
                reader.Skip();
                reader.Skip();
                max = ReadNumber(reader, converter, parseError);
            }
            else
            {
                max = min;
            }
            if (min is null && max is null)
                throw ErrorEmpty.CreateWithContext(reader);
            return (min, max);
        }
        catch (CommandSyntaxException e)
        {
            reader.SetCursor(start);
            throw new CommandSyntaxException(e.Type, e.RawMessage, e.Input, start);
        }
    }

    //Doubles double-precision range; the squared value is precomputed for squared-distance matching
    public sealed class Doubles
    {
        public static readonly Doubles Any = new(null, null);

        //SingleCodec single float form, both bounds equal, maps to the float branch of vanilla Doubles.CODEC
        private static readonly Codec<Doubles> SingleCodec = new DoubleExactlyCodec();

        //MinMaxCodec compound tag form; a missing side means unbounded
        private static readonly Codec<Doubles> MinMaxCodec = RecordCodecBuilder.Of2(
            Codecs.Double.OptionalFieldOf("min").ForGetter((Doubles range) => ToOptional(range.Min)),
            Codecs.Double.OptionalFieldOf("max").ForGetter((Doubles range) => ToOptional(range.Max)),
            (min, max) => new Doubles(min.IsPresent ? min.Get() : null, max.IsPresent ? max.Get() : null));

        //CODEC persistence codec, tries a single float then min/max, maps to vanilla Doubles.CODEC
        public static readonly Codec<Doubles> CODEC = Codecs.WithAlternative(SingleCodec, MinMaxCodec);

        private static Optional<double> ToOptional(double? value)
            => value is { } present ? Optional<double>.Of(present) : Optional<double>.Empty();

        public Doubles(double? min, double? max)
        {
            Min = min;
            Max = max;
            MinSqr = min * min;
            MaxSqr = max * max;
        }

        public double? Min { get; }
        public double? Max { get; }
        private double? MinSqr { get; }
        private double? MaxSqr { get; }

        public bool IsAny => Min is null && Max is null;

        public bool Matches(double value)
            => (Min is null || Min <= value) && (Max is null || Max >= value);

        public bool MatchesSqr(double valueSqr)
            => (MinSqr is null || MinSqr <= valueSqr) && (MaxSqr is null || MaxSqr >= valueSqr);

        public static Doubles FromReader(StringReader reader)
        {
            var start = reader.Cursor;
            var (min, max) = ReadBounds(reader,
                s => double.Parse(s, CultureInfo.InvariantCulture),
                CommandSyntaxException.BuiltInExceptions.ReaderInvalidDouble());
            if (min > max)
            {
                reader.SetCursor(start);
                throw ErrorSwapped.CreateWithContext(reader);
            }
            return new Doubles(min, max);
        }
    }

    //Ints integer range; the squared value uses long to avoid overflow
    public sealed class Ints
    {
        public static readonly Ints Any = new(null, null);

        //SingleCodec single integer form, both bounds equal, maps to the integer branch of vanilla Ints.CODEC
        private static readonly Codec<Ints> SingleCodec = new IntExactlyCodec();

        //MinMaxCodec compound tag form; a missing side means unbounded
        private static readonly Codec<Ints> MinMaxCodec = RecordCodecBuilder.Of2(
            Codecs.Int.OptionalFieldOf("min").ForGetter((Ints range) => ToOptional(range.Min)),
            Codecs.Int.OptionalFieldOf("max").ForGetter((Ints range) => ToOptional(range.Max)),
            (min, max) => new Ints(min.IsPresent ? min.Get() : null, max.IsPresent ? max.Get() : null));

        //CODEC persistence codec, tries a single integer then min/max, maps to vanilla Ints.CODEC
        public static readonly Codec<Ints> CODEC = Codecs.WithAlternative(SingleCodec, MinMaxCodec);

        private static Optional<int> ToOptional(int? value)
            => value is { } present ? Optional<int>.Of(present) : Optional<int>.Empty();

        public Ints(int? min, int? max)
        {
            Min = min;
            Max = max;
            MinSqr = min is null ? null : (long)min * min;
            MaxSqr = max is null ? null : (long)max * max;
        }

        public int? Min { get; }
        public int? Max { get; }
        private long? MinSqr { get; }
        private long? MaxSqr { get; }

        public bool IsAny => Min is null && Max is null;

        public bool Matches(int value)
            => (Min is null || Min <= value) && (Max is null || Max >= value);

        public bool MatchesSqr(long valueSqr)
            => (MinSqr is null || MinSqr <= valueSqr) && (MaxSqr is null || MaxSqr >= valueSqr);

        public static Ints FromReader(StringReader reader)
        {
            var start = reader.Cursor;
            var (min, max) = ReadBounds(reader,
                s => int.Parse(s, CultureInfo.InvariantCulture),
                CommandSyntaxException.BuiltInExceptions.ReaderInvalidInt());
            if (min > max)
            {
                reader.SetCursor(start);
                throw ErrorSwapped.CreateWithContext(reader);
            }
            return new Ints(min, max);
        }
    }

    //FloatDegrees angle range; degrees wrap and there is no swapped validation
    public sealed class FloatDegrees
    {
        public static readonly FloatDegrees Any = new(null, null);

        public FloatDegrees(float? min, float? max)
        {
            Min = min;
            Max = max;
        }

        public float? Min { get; }
        public float? Max { get; }

        public bool IsAny => Min is null && Max is null;

        public static FloatDegrees FromReader(StringReader reader)
        {
            var (min, max) = ReadBounds(reader,
                s => float.Parse(s, CultureInfo.InvariantCulture),
                CommandSyntaxException.BuiltInExceptions.ReaderInvalidFloat());
            return new FloatDegrees(min, max);
        }
    }
}

//IntExactlyCodec integer range codec for the single-integer form
//Parsing always yields equal bounds; encoding only holds when the bounds are equal, otherwise the next candidate in the chain is used, maps to the integer branch of vanilla Ints.CODEC
internal sealed class IntExactlyCodec : ScalarCodec<MinMaxBounds.Ints>
{
    public override DataResult<MinMaxBounds.Ints> Parse<U>(DynamicOps<U> ops, U input)
        => Codecs.Int.Parse(ops, input).Map(value => new MinMaxBounds.Ints(value, value));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, MinMaxBounds.Ints value)
        => value.Min is { } min && min == value.Max
            ? Codecs.Int.EncodeStart(ops, min)
            : DataResult<U>.Error(() => "range is not a single value, cannot encode as an integer");
}

//DoubleExactlyCodec double range codec for the single-float form
//Parsing always yields equal bounds; encoding only holds when the bounds are equal, otherwise the next candidate in the chain is used, maps to the float branch of vanilla Doubles.CODEC
internal sealed class DoubleExactlyCodec : ScalarCodec<MinMaxBounds.Doubles>
{
    public override DataResult<MinMaxBounds.Doubles> Parse<U>(DynamicOps<U> ops, U input)
        => Codecs.Double.Parse(ops, input).Map(value => new MinMaxBounds.Doubles(value, value));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, MinMaxBounds.Doubles value)
        => value.Min is { } min && min == value.Max
            ? Codecs.Double.EncodeStart(ops, min)
            : DataResult<U>.Error(() => "range is not a single value, cannot encode as a float");
}
