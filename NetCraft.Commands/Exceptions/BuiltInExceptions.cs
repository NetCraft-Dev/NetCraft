using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//BuiltInExceptions 内建异常工厂对应原版BuiltInExceptions
//实现IBuiltInExceptionProvider按字段缓存所有标准异常类型实例
public sealed class BuiltInExceptions : IBuiltInExceptionProvider
{
    private static readonly Dynamic2CommandExceptionType DOUBLE_TOO_SMALL =
        new((found, min) => new LiteralMessage("Double must not be less than " + min + ", found " + found));
    private static readonly Dynamic2CommandExceptionType DOUBLE_TOO_BIG =
        new((found, max) => new LiteralMessage("Double must not be more than " + max + ", found " + found));
    private static readonly Dynamic2CommandExceptionType FLOAT_TOO_SMALL =
        new((found, min) => new LiteralMessage("Float must not be less than " + min + ", found " + found));
    private static readonly Dynamic2CommandExceptionType FLOAT_TOO_BIG =
        new((found, max) => new LiteralMessage("Float must not be more than " + max + ", found " + found));
    private static readonly Dynamic2CommandExceptionType INTEGER_TOO_SMALL =
        new((found, min) => new LiteralMessage("Integer must not be less than " + min + ", found " + found));
    private static readonly Dynamic2CommandExceptionType INTEGER_TOO_BIG =
        new((found, max) => new LiteralMessage("Integer must not be more than " + max + ", found " + found));
    private static readonly Dynamic2CommandExceptionType LONG_TOO_SMALL =
        new((found, min) => new LiteralMessage("Long must not be less than " + min + ", found " + found));
    private static readonly Dynamic2CommandExceptionType LONG_TOO_BIG =
        new((found, max) => new LiteralMessage("Long must not be more than " + max + ", found " + found));
    private static readonly DynamicCommandExceptionType LITERAL_INCORRECT =
        new(expected => new LiteralMessage("Expected literal " + expected));
    private static readonly SimpleCommandExceptionType READER_EXPECTED_START_OF_QUOTE =
        new(new LiteralMessage("Expected quote to start a string"));
    private static readonly SimpleCommandExceptionType READER_EXPECTED_END_OF_QUOTE =
        new(new LiteralMessage("Unclosed quoted string"));
    private static readonly DynamicCommandExceptionType READER_INVALID_ESCAPE =
        new(character => new LiteralMessage("Invalid escape sequence '" + character + "' in quoted string"));
    private static readonly DynamicCommandExceptionType READER_INVALID_BOOL =
        new(value => new LiteralMessage("Invalid bool, expected true or false but found '" + value + "'"));
    private static readonly DynamicCommandExceptionType READER_INVALID_INT =
        new(value => new LiteralMessage("Invalid integer '" + value + "'"));
    private static readonly SimpleCommandExceptionType READER_EXPECTED_INT =
        new(new LiteralMessage("Expected integer"));
    private static readonly DynamicCommandExceptionType READER_INVALID_LONG =
        new(value => new LiteralMessage("Invalid long '" + value + "'"));
    private static readonly SimpleCommandExceptionType READER_EXPECTED_LONG =
        new(new LiteralMessage("Expected long"));
    private static readonly DynamicCommandExceptionType READER_INVALID_DOUBLE =
        new(value => new LiteralMessage("Invalid double '" + value + "'"));
    private static readonly SimpleCommandExceptionType READER_EXPECTED_DOUBLE =
        new(new LiteralMessage("Expected double"));
    private static readonly DynamicCommandExceptionType READER_INVALID_FLOAT =
        new(value => new LiteralMessage("Invalid float '" + value + "'"));
    private static readonly SimpleCommandExceptionType READER_EXPECTED_FLOAT =
        new(new LiteralMessage("Expected float"));
    private static readonly SimpleCommandExceptionType READER_EXPECTED_BOOL =
        new(new LiteralMessage("Expected bool"));
    private static readonly DynamicCommandExceptionType READER_EXPECTED_SYMBOL =
        new(symbol => new LiteralMessage("Expected '" + symbol + "'"));
    private static readonly SimpleCommandExceptionType DISPATCHER_UNKNOWN_COMMAND =
        new(new LiteralMessage("Unknown command"));
    private static readonly SimpleCommandExceptionType DISPATCHER_UNKNOWN_ARGUMENT =
        new(new LiteralMessage("Incorrect argument for command"));
    private static readonly SimpleCommandExceptionType DISPATCHER_EXPECTED_ARGUMENT_SEPARATOR =
        new(new LiteralMessage("Expected whitespace to end one argument, but found trailing data"));
    private static readonly DynamicCommandExceptionType DISPATCHER_PARSE_EXCEPTION =
        new(message => new LiteralMessage("Could not parse command: " + message));

    public Dynamic2CommandExceptionType DoubleTooLow() => DOUBLE_TOO_SMALL;
    public Dynamic2CommandExceptionType DoubleTooHigh() => DOUBLE_TOO_BIG;
    public Dynamic2CommandExceptionType FloatTooLow() => FLOAT_TOO_SMALL;
    public Dynamic2CommandExceptionType FloatTooHigh() => FLOAT_TOO_BIG;
    public Dynamic2CommandExceptionType IntegerTooLow() => INTEGER_TOO_SMALL;
    public Dynamic2CommandExceptionType IntegerTooHigh() => INTEGER_TOO_BIG;
    public Dynamic2CommandExceptionType LongTooLow() => LONG_TOO_SMALL;
    public Dynamic2CommandExceptionType LongTooHigh() => LONG_TOO_BIG;
    public DynamicCommandExceptionType LiteralIncorrect() => LITERAL_INCORRECT;
    public SimpleCommandExceptionType ReaderExpectedStartOfQuote() => READER_EXPECTED_START_OF_QUOTE;
    public SimpleCommandExceptionType ReaderExpectedEndOfQuote() => READER_EXPECTED_END_OF_QUOTE;
    public DynamicCommandExceptionType ReaderInvalidEscape() => READER_INVALID_ESCAPE;
    public DynamicCommandExceptionType ReaderInvalidBool() => READER_INVALID_BOOL;
    public DynamicCommandExceptionType ReaderInvalidInt() => READER_INVALID_INT;
    public SimpleCommandExceptionType ReaderExpectedInt() => READER_EXPECTED_INT;
    public DynamicCommandExceptionType ReaderInvalidLong() => READER_INVALID_LONG;
    public SimpleCommandExceptionType ReaderExpectedLong() => READER_EXPECTED_LONG;
    public DynamicCommandExceptionType ReaderInvalidDouble() => READER_INVALID_DOUBLE;
    public SimpleCommandExceptionType ReaderExpectedDouble() => READER_EXPECTED_DOUBLE;
    public DynamicCommandExceptionType ReaderInvalidFloat() => READER_INVALID_FLOAT;
    public SimpleCommandExceptionType ReaderExpectedFloat() => READER_EXPECTED_FLOAT;
    public SimpleCommandExceptionType ReaderExpectedBool() => READER_EXPECTED_BOOL;
    public DynamicCommandExceptionType ReaderExpectedSymbol() => READER_EXPECTED_SYMBOL;
    public SimpleCommandExceptionType DispatcherUnknownCommand() => DISPATCHER_UNKNOWN_COMMAND;
    public SimpleCommandExceptionType DispatcherUnknownArgument() => DISPATCHER_UNKNOWN_ARGUMENT;
    public SimpleCommandExceptionType DispatcherExpectedArgumentSeparator() => DISPATCHER_EXPECTED_ARGUMENT_SEPARATOR;
    public DynamicCommandExceptionType DispatcherParseException() => DISPATCHER_PARSE_EXCEPTION;
}
