namespace NetCraft.Commands.Exceptions;

//IBuiltInExceptionProvider 内建异常工厂接口对应原版BuiltInExceptionProvider
//定义解析与调度过程中所有标准异常类型的获取方法
//CommandSyntaxException.BuiltInExceptions字段持有此接口的默认BuiltInExceptions实例
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
