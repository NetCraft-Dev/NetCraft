using System.Globalization;
using System.Text;
using NetCraft.Codec;
using NetCraft.Util;
using NetCraft.Util.Parsing.Packrat;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Nbt;

//Number base, mirroring vanilla SnbtGrammar.Base
internal enum Base
{
    Binary,
    Decimal,
    Hex
}

//Sign prefix, mirroring vanilla SnbtGrammar.SignedPrefix
internal enum SignedPrefix
{
    Signed,
    Unsigned
}

//Type suffix, mirroring vanilla SnbtGrammar.TypeSuffix
//Vanilla Java TypeSuffix is an enum reference type; C# uses a sealed class because a value type cannot express failure as null in the packrat framework
internal sealed class TypeSuffix
{
    public static readonly TypeSuffix Float = new("Float");
    public static readonly TypeSuffix Double = new("Double");
    public static readonly TypeSuffix Byte = new("Byte");
    public static readonly TypeSuffix Short = new("Short");
    public static readonly TypeSuffix Int = new("Int");
    public static readonly TypeSuffix Long = new("Long");

    public string Name { get; }
    private TypeSuffix(string name) { Name = name; }
    public override string ToString() => Name;
}

//Sign, mirroring vanilla SnbtGrammar.Sign
//Vanilla Java Sign is an enum reference type; C# uses a sealed class because a value type cannot express failure as null in the packrat framework
internal sealed class Sign
{
    public static readonly Sign Plus = new("Plus");
    public static readonly Sign Minus = new("Minus");

    public string Name { get; }
    private Sign(string name) { Name = name; }
    public override string ToString() => Name;
}

//Integer suffix, mirroring vanilla SnbtGrammar.IntegerSuffix
internal sealed class IntegerSuffix
{
    public SignedPrefix? Signed { get; }
    public TypeSuffix? Type { get; }
    public static IntegerSuffix Empty { get; } = new(null, null);

    public IntegerSuffix(SignedPrefix? signed, TypeSuffix? type)
    {
        Signed = signed;
        Type = type;
    }
}

//Integer literal, mirroring vanilla SnbtGrammar.IntegerLiteral
internal sealed class IntegerLiteral
{
    public Sign Sign { get; }
    public Base Base { get; }
    public string Digits { get; }
    public IntegerSuffix Suffix { get; }

    public IntegerLiteral(Sign sign, Base @base, string digits, IntegerSuffix suffix)
    {
        Sign = sign;
        Base = @base;
        Digits = digits;
        Suffix = suffix;
    }

    //signedOrDefault infers the sign prefix: binary and hex default to unsigned, decimal to signed
    private SignedPrefix SignedOrDefault()
    {
        if (Suffix.Signed is { } signed) return signed;
        return Base == global::NetCraft.Nbt.Base.Decimal ? SignedPrefix.Signed : SignedPrefix.Unsigned;
    }

    //cleanupDigits concatenates the sign with the underscore-free digit string
    private string CleanupDigits(Sign sign)
    {
        var needsUnderscoreRemoval = SnbtGrammar.NeedsUnderscoreRemoval(Digits);
        if (sign == Sign.Minus || needsUnderscoreRemoval)
        {
            var result = new StringBuilder();
            if (sign == Sign.Minus) result.Append('-');
            SnbtGrammar.CleanAndAppend(result, Digits, needsUnderscoreRemoval);
            return result.ToString();
        }
        return Digits;
    }

    //create builds a value of type T through ops and returns default on failure
    public T? Create<T>(DynamicOps<T> ops, ParseState<CommandStringReader> state)
        => Create(ops, Suffix.Type ?? TypeSuffix.Int, state);

    public T? Create<T>(DynamicOps<T> ops, TypeSuffix typeSuffix, ParseState<CommandStringReader> state)
    {
        var isSigned = SignedOrDefault() == SignedPrefix.Signed;
        if (!isSigned && Sign == Sign.Minus)
        {
            state.ErrorCollector.Store(state.Mark(), SnbtGrammar.ErrorExpectedNonNegativeNumber);
            return default;
        }
        var digits = CleanupDigits(Sign);
        var radix = Base switch
        {
            global::NetCraft.Nbt.Base.Binary => 2,
            global::NetCraft.Nbt.Base.Decimal => 10,
            global::NetCraft.Nbt.Base.Hex => 16,
            _ => throw new InvalidOperationException()
        };
        try
        {
            if (isSigned)
            {
                if (typeSuffix == TypeSuffix.Byte) return ops.CreateByte((byte)Convert.ToSByte(digits, radix));
                if (typeSuffix == TypeSuffix.Short) return ops.CreateShort(Convert.ToInt16(digits, radix));
                if (typeSuffix == TypeSuffix.Int) return ops.CreateInt(Convert.ToInt32(digits, radix));
                if (typeSuffix == TypeSuffix.Long) return ops.CreateLong(Convert.ToInt64(digits, radix));
                return FailInteger<T>(state);
            }
            if (typeSuffix == TypeSuffix.Byte) return ops.CreateByte(Convert.ToByte(digits, radix));
            if (typeSuffix == TypeSuffix.Short) return ops.CreateShort((short)Convert.ToUInt16(digits, radix));
            if (typeSuffix == TypeSuffix.Int) return ops.CreateInt((int)Convert.ToUInt32(digits, radix));
            if (typeSuffix == TypeSuffix.Long) return ops.CreateLong((long)Convert.ToUInt64(digits, radix));
            return FailInteger<T>(state);
        }
        catch (FormatException e)
        {
            state.ErrorCollector.Store(state.Mark(), SnbtGrammar.CreateNumberParseError(e.Message));
            return default;
        }
    }

    private static T? FailInteger<T>(ParseState<CommandStringReader> state)
    {
        state.ErrorCollector.Store(state.Mark(), SnbtGrammar.ErrorExpectedIntegerType);
        return default;
    }
}

//Signed value, mirroring vanilla SnbtGrammar.Signed
internal sealed class Signed<T>
{
    public Sign Sign { get; }
    public T Value { get; }

    public Signed(Sign sign, T value)
    {
        Sign = sign;
        Value = value;
    }
}

//Map entry, carried by Map.Entry<String,T> in vanilla SnbtGrammar
//Uses a class rather than KeyValuePair<string,T> because a struct cannot express failure as null in the packrat framework
internal sealed class MapEntry<T>
{
    public string Key { get; }
    public T Value { get; }

    public MapEntry(string key, T value)
    {
        Key = key;
        Value = value;
    }
}

//Array prefix, mirroring vanilla SnbtGrammar.ArrayPrefix
internal abstract class ArrayPrefix
{
    public static readonly ArrayPrefix Byte = new ByteArrayPrefix();
    public static readonly ArrayPrefix Int = new IntArrayPrefix();
    public static readonly ArrayPrefix Long = new LongArrayPrefix();

    private readonly TypeSuffix _defaultType;
    private readonly HashSet<TypeSuffix> _additionalTypes;

    protected ArrayPrefix(TypeSuffix defaultType, params TypeSuffix[] additionalTypes)
    {
        _defaultType = defaultType;
        _additionalTypes = new HashSet<TypeSuffix>(additionalTypes);
    }

    //Vanilla returns T and null on error; matching Java type erasure, C# uses default!
    public abstract T Create<T>(DynamicOps<T> ops);
    public abstract T Create<T>(DynamicOps<T> ops, List<IntegerLiteral> entries, ParseState<CommandStringReader> state);

    public bool IsAllowed(TypeSuffix type) => type == _defaultType || _additionalTypes.Contains(type);

    protected long? BuildNumber(IntegerLiteral entry, ParseState<CommandStringReader> state)
    {
        var actualType = ComputeType(entry.Suffix);
        if (actualType is null)
        {
            state.ErrorCollector.Store(state.Mark(), SnbtGrammar.ErrorInvalidArrayElementType);
            return null;
        }
        return ExtractLong(entry, actualType!, state);
    }

    private TypeSuffix? ComputeType(IntegerSuffix value)
    {
        if (value.Type is null) return _defaultType;
        return IsAllowed(value.Type!) ? value.Type : null;
    }

    //ExtractLong converts an IntegerLiteral to long according to type for use as an array element
    private static long? ExtractLong(IntegerLiteral entry, TypeSuffix type, ParseState<CommandStringReader> state)
    {
        var isSigned = entry.Suffix.Signed is { } signed
            ? signed == SignedPrefix.Signed
            : entry.Base == global::NetCraft.Nbt.Base.Decimal;
        if (!isSigned && entry.Sign == Sign.Minus)
        {
            state.ErrorCollector.Store(state.Mark(), SnbtGrammar.ErrorExpectedNonNegativeNumber);
            return null;
        }
        var digits = entry.Sign == Sign.Minus
            ? "-" + entry.Digits.Replace("_", "")
            : entry.Digits.Replace("_", "");
        var radix = entry.Base switch
        {
            global::NetCraft.Nbt.Base.Binary => 2,
            global::NetCraft.Nbt.Base.Decimal => 10,
            global::NetCraft.Nbt.Base.Hex => 16,
            _ => throw new InvalidOperationException()
        };
        try
        {
            if (type == TypeSuffix.Byte) return isSigned ? Convert.ToSByte(digits, radix) : (byte)Convert.ToByte(digits, radix);
            if (type == TypeSuffix.Short) return isSigned ? Convert.ToInt16(digits, radix) : (short)Convert.ToUInt16(digits, radix);
            if (type == TypeSuffix.Int) return isSigned ? Convert.ToInt32(digits, radix) : (int)Convert.ToUInt32(digits, radix);
            if (type == TypeSuffix.Long) return isSigned ? Convert.ToInt64(digits, radix) : (long)Convert.ToUInt64(digits, radix);
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed class ByteArrayPrefix : ArrayPrefix
    {
        public ByteArrayPrefix() : base(TypeSuffix.Byte) { }

        public override T Create<T>(DynamicOps<T> ops)
            => ops.CreateByteList(Array.Empty<byte>());

        public override T Create<T>(DynamicOps<T> ops, List<IntegerLiteral> entries, ParseState<CommandStringReader> state)
        {
            var bytes = new byte[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var n = BuildNumber(entries[i], state);
                if (!n.HasValue) return default!;
                bytes[i] = (byte)n.Value;
            }
            return ops.CreateByteList(bytes);
        }
    }

    private sealed class IntArrayPrefix : ArrayPrefix
    {
        public IntArrayPrefix() : base(TypeSuffix.Int, TypeSuffix.Byte, TypeSuffix.Short) { }

        public override T Create<T>(DynamicOps<T> ops)
            => ops.CreateIntList(Array.Empty<int>());

        public override T Create<T>(DynamicOps<T> ops, List<IntegerLiteral> entries, ParseState<CommandStringReader> state)
        {
            var ints = new int[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var n = BuildNumber(entries[i], state);
                if (!n.HasValue) return default!;
                ints[i] = (int)n.Value;
            }
            return ops.CreateIntList(ints);
        }
    }

    private sealed class LongArrayPrefix : ArrayPrefix
    {
        public LongArrayPrefix() : base(TypeSuffix.Long, TypeSuffix.Byte, TypeSuffix.Short, TypeSuffix.Int) { }

        public override T Create<T>(DynamicOps<T> ops)
            => ops.CreateLongList(Array.Empty<long>());

        public override T Create<T>(DynamicOps<T> ops, List<IntegerLiteral> entries, ParseState<CommandStringReader> state)
        {
            var longs = new long[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var n = BuildNumber(entries[i], state);
                if (!n.HasValue) return default!;
                longs[i] = n.Value;
            }
            return ops.CreateLongList(longs);
        }
    }
}

//SimpleHexLiteralParseRule matches fixed-length hex, mirroring the same-named vanilla inner class
internal sealed class SimpleHexLiteralParseRule : GreedyPredicateParseRule
{
    public SimpleHexLiteralParseRule(int size)
        : base(size, size, DelayedExceptionFactories.Create(SnbtGrammar.ErrorExpectedHexEscapeType, size.ToString()))
    {
    }

    protected override bool IsAccepted(char c) => c is >= '0' and <= '9'
        or >= 'A' and <= 'F' or >= 'a' and <= 'f';
}
