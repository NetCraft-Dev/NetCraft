using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//SimpleCommandExceptionType 简单异常类型对应原版SimpleCommandExceptionType
//持有固定Message创建无参数或带reader上下文的CommandSyntaxException
public sealed class SimpleCommandExceptionType : ICommandExceptionType
{
    private readonly IMessage _message;

    public SimpleCommandExceptionType(IMessage message)
    {
        _message = message;
    }

    public CommandSyntaxException Create()
        => new(this, _message);

    public CommandSyntaxException CreateWithContext(IImmutableStringReader reader)
        => new(this, _message, reader.String, reader.Cursor);

    public override string ToString() => _message.GetString();
}
