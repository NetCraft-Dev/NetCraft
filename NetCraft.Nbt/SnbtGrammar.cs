using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NetCraft.Codec;
using NetCraft.Util;
using NetCraft.Util.Parsing.Packrat;
using NetCraft.Util.Parsing.Packrat.Commands;

namespace NetCraft.Nbt;

//SNBT grammar, mirroring vanilla net.minecraft.nbt.SnbtGrammar
//Registers every parse rule through createParser and builds a Grammar<Tag> for TagParser
public static partial class SnbtGrammar
{
    internal static readonly DynamicCommandExceptionType ErrorNumberParseFailureType =
        new(msg => $"Number parse failure: {msg}");

    internal static readonly DynamicCommandExceptionType ErrorExpectedHexEscapeType =
        new(length => $"Expected hex escape of length {length}");

    internal static readonly DynamicCommandExceptionType ErrorInvalidCodepointType =
        new(codepoint => $"Invalid codepoint U+{codepoint:X8}");

    internal static readonly DynamicCommandExceptionType ErrorNoSuchOperationType =
        new(op => $"No such operation {op}");

    internal static readonly DelayedException<CommandSyntaxException> ErrorNumberParseFailure =
        DelayedExceptionFactories.Create(ErrorNumberParseFailureType, "unknown");

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedHexEscape =
        DelayedExceptionFactories.Create(ErrorExpectedHexEscapeType, "0");

    internal static readonly DelayedException<CommandSyntaxException> ErrorInvalidCodepoint =
        DelayedExceptionFactories.Create(ErrorInvalidCodepointType, "U+00000000");

    internal static readonly DelayedException<CommandSyntaxException> ErrorNoSuchOperation =
        DelayedExceptionFactories.Create(ErrorNoSuchOperationType, "unknown");

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedIntegerType =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected integer type"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedFloatType =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected float type"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedNonNegativeNumber =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected non-negative number"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorInvalidCharacterName =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Invalid character name"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorInvalidArrayElementType =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Invalid array element type"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorInvalidUnquotedStart =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Invalid unquoted start"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedUnquotedString =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected unquoted string"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorInvalidStringContents =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Invalid string contents"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedBinaryNumeral =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected binary numeral"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorUnderscoreNotAllowed =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Underscore not allowed"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedDecimalNumeral =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected decimal numeral"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorExpectedHexNumeral =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Expected hex numeral"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorEmptyKey =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Empty key"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorLeadingZeroNotAllowed =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Leading zero not allowed"));

    internal static readonly DelayedException<CommandSyntaxException> ErrorInfinityNotAllowed =
        DelayedExceptionFactories.Create(new SimpleCommandExceptionType("Infinity not allowed"));

    //BINARY_NUMERAL is a binary digit string accepting 0, 1 and underscores
    internal static readonly NumberRunParseRule BinaryNumeral = new BinaryNumeralRule();

    //DECIMAL_NUMERAL is a decimal digit string accepting 0-9 and underscores
    internal static readonly NumberRunParseRule DecimalNumeral = new DecimalNumeralRule();

    //HEX_NUMERAL is a hexadecimal digit string accepting 0-9A-Fa-f and underscores
    internal static readonly NumberRunParseRule HexNumeral = new HexNumeralRule();

    //PLAIN_STRING_CHUNK is a plain string chunk that accepts neither quotes nor backslashes
    internal static readonly GreedyPredicateParseRule PlainStringChunk = new PlainStringChunkRule();

    //NUMBER_LOOKEAHEAD is a lookahead for number start characters
    internal static readonly Term<CommandStringReader> NumberLookahead =
        new NumberLookaheadTerm();

    internal static readonly Regex UnicodeName = new("[-a-zA-Z0-9 ]+", RegexOptions.Compiled);

    //CreateNumberParseError wraps a FormatException message into a DelayedException
    internal static DelayedException<CommandSyntaxException> CreateNumberParseError(string message)
        => DelayedExceptionFactories.Create(ErrorNumberParseFailureType, message);

    //needsUnderscoreRemoval checks whether the string contains underscores
    internal static bool NeedsUnderscoreRemoval(string contents) => contents.IndexOf('_') != -1;

    //cleanAndAppend removes underscores when needed and appends to output
    internal static void CleanAndAppend(StringBuilder output, string contents)
        => CleanAndAppend(output, contents, NeedsUnderscoreRemoval(contents));

    internal static void CleanAndAppend(StringBuilder output, string contents, bool needsUnderscoreRemoval)
    {
        if (needsUnderscoreRemoval)
        {
            foreach (var c in contents)
            {
                if (c != '_') output.Append(c);
            }
            return;
        }
        output.Append(contents);
    }

    //canStartNumber reports whether a character can start a numeric literal
    internal static bool CanStartNumber(char c) => c is '+' or '-' or '.' or (>= '0' and <= '9');

    //isAllowedToStartUnquotedString allows an unquoted string only for non-digit start characters
    internal static bool IsAllowedToStartUnquotedString(char c) => !CanStartNumber(c);

    //parseUnsignedShort parses a string into an unsigned short using Convert with the given radix
    internal static short ParseUnsignedShort(string s, int radix)
    {
        var value = Convert.ToInt32(s, radix);
        if ((value >> 16) != 0) throw new FormatException("out of range: " + value);
        return (short)value;
    }

    //escapeControlCharacters turns control characters into SNBT escape sequences
    public static string? EscapeControlCharacters(char c) => c switch
    {
        '\b' => "b",
        '\t' => "t",
        '\n' => "n",
        '\f' => "f",
        '\r' => "r",
        _ when c < ' ' => "x" + ((byte)c).ToString("X2"),
        _ => null
    };

    //joinList joins a string list with an empty string, matching vanilla LocalTime.ROOT_LOCALE
    internal static string JoinList(List<string> list)
    {
        if (list.Count == 0) return string.Empty;
        if (list.Count == 1) return list[0];
        return string.Join(string.Empty, list);
    }

    //createFloat joins the parts of a float literal into a string and converts to float or double based on typeSuffix
    internal static T? CreateFloat<T>(DynamicOps<T> ops, Sign sign, string? whole, string? fraction,
        Signed<string>? exponent, TypeSuffix? typeSuffix, ParseState<CommandStringReader> state)
    {
        var sb = new StringBuilder();
        if (sign == Sign.Minus) sb.Append('-');
        if (whole is not null) CleanAndAppend(sb, whole);
        if (fraction is not null)
        {
            sb.Append('.');
            CleanAndAppend(sb, fraction);
        }
        if (exponent is not null)
        {
            sb.Append('e');
            if (exponent.Sign == Sign.Minus) sb.Append('-');
            CleanAndAppend(sb, exponent.Value);
        }
        var str = sb.ToString();
        try
        {
            if (typeSuffix == TypeSuffix.Float) return ConvertFloat(ops, state, str);
            if (typeSuffix == TypeSuffix.Double) return ConvertDouble(ops, state, str);
            state.ErrorCollector.Store(state.Mark(), ErrorExpectedFloatType);
            return default;
        }
        catch (FormatException e)
        {
            state.ErrorCollector.Store(state.Mark(), CreateNumberParseError(e.Message));
            return default;
        }
    }

    //convertFloat parses to float, checks that it is not infinite and calls ops.CreateFloat
    internal static T? ConvertFloat<T>(DynamicOps<T> ops, ParseState<CommandStringReader> state, string str)
    {
        var f = float.Parse(str, CultureInfo.InvariantCulture);
        if (!float.IsFinite(f))
        {
            state.ErrorCollector.Store(state.Mark(), ErrorInfinityNotAllowed);
            return default;
        }
        return ops.CreateFloat(f);
    }

    //convertDouble parses to double, checks that it is not infinite and calls ops.CreateDouble
    internal static T? ConvertDouble<T>(DynamicOps<T> ops, ParseState<CommandStringReader> state, string str)
    {
        var d = double.Parse(str, CultureInfo.InvariantCulture);
        if (!double.IsFinite(d))
        {
            state.ErrorCollector.Store(state.Mark(), ErrorInfinityNotAllowed);
            return default;
        }
        return ops.CreateDouble(d);
    }

    private sealed class BinaryNumeralRule : NumberRunParseRule
    {
        public BinaryNumeralRule() : base(ErrorExpectedBinaryNumeral, ErrorUnderscoreNotAllowed) { }

        protected override bool IsAccepted(char c) => c == '0' || c == '1' || c == '_';
    }

    private sealed class DecimalNumeralRule : NumberRunParseRule
    {
        public DecimalNumeralRule() : base(ErrorExpectedDecimalNumeral, ErrorUnderscoreNotAllowed) { }

        protected override bool IsAccepted(char c) => c is (>= '0' and <= '9') or '_';
    }

    private sealed class HexNumeralRule : NumberRunParseRule
    {
        public HexNumeralRule() : base(ErrorExpectedHexNumeral, ErrorUnderscoreNotAllowed) { }

        protected override bool IsAccepted(char c) => c is (>= '0' and <= '9')
            or (>= 'A' and <= 'F') or (>= 'a' and <= 'f') or '_';
    }

    private sealed class PlainStringChunkRule : GreedyPredicateParseRule
    {
        public PlainStringChunkRule() : base(1, ErrorInvalidStringContents) { }

        protected override bool IsAccepted(char c) => c != '"' && c != '\'' && c != '\\';
    }

    private sealed class NumberLookaheadTerm : StringReaderTerms.TerminalCharacters
    {
        public NumberLookaheadTerm() : base(Array.Empty<char>()) { }

        protected override bool IsAccepted(char value) => CanStartNumber(value);
    }
}
