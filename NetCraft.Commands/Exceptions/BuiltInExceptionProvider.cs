namespace NetCraft.Commands.Exceptions;

//IBuiltInExceptionProvider built-in exception factory interface, maps to vanilla BuiltInExceptionProvider
//Defines accessors for every standard exception type used during parsing and dispatch
//The CommandSyntaxException.BuiltInExceptions field holds a default BuiltInExceptions instance of this interface
public interface IBuiltInExceptionProvider
{
    Dynamic2CommandExceptionType DoubleTooLow();
    Dynamic2CommandExceptionType DoubleTooHigh();
    Dynamic2CommandExceptionType FloatTooLow();
    Dynamic2CommandExceptionType FloatTooHigh();
    Dynamic2CommandExceptionType IntegerTooLow();
    Dynamic2CommandExceptionType IntegerTooHigh();
    Dynamic2CommandExceptionType LongTooLow();
    Dynamic2CommandExceptionType LongTooHigh();
    DynamicCommandExceptionType LiteralIncorrect();
    SimpleCommandExceptionType ReaderExpectedStartOfQuote();
    SimpleCommandExceptionType ReaderExpectedEndOfQuote();
    DynamicCommandExceptionType ReaderInvalidEscape();
    DynamicCommandExceptionType ReaderInvalidBool();
    DynamicCommandExceptionType ReaderInvalidInt();
    SimpleCommandExceptionType ReaderExpectedInt();
    DynamicCommandExceptionType ReaderInvalidLong();
    SimpleCommandExceptionType ReaderExpectedLong();
    DynamicCommandExceptionType ReaderInvalidDouble();
    SimpleCommandExceptionType ReaderExpectedDouble();
    DynamicCommandExceptionType ReaderInvalidFloat();
    SimpleCommandExceptionType ReaderExpectedFloat();
    SimpleCommandExceptionType ReaderExpectedBool();
    DynamicCommandExceptionType ReaderExpectedSymbol();
    SimpleCommandExceptionType DispatcherUnknownCommand();
    SimpleCommandExceptionType DispatcherUnknownArgument();
    SimpleCommandExceptionType DispatcherExpectedArgumentSeparator();
    DynamicCommandExceptionType DispatcherParseException();
}
