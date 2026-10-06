using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//DynamicCommandExceptionType single-argument dynamic exception type, maps to vanilla DynamicCommandExceptionType
//Produces a Message from an argument via a Func and creates a CommandSyntaxException
public sealed class DynamicCommandExceptionType : ICommandExceptionType
{
    private readonly Func<object, IMessage> _function;

    public DynamicCommandExceptionType(Func<object, IMessage> function)
    {
        _function = function;
    }

    public CommandSyntaxException Create(object arg)
        => new(this, _function(arg));

    public CommandSyntaxException CreateWithContext(IImmutableStringReader reader, object arg)
        => new(this, _function(arg), reader.String, reader.Cursor);
}
