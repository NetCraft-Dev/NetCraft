using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//SimpleCommandExceptionType maps to vanilla SimpleCommandExceptionType
//Holds a fixed Message and creates a CommandSyntaxException with no arguments or with reader context
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
