using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//DynamicNCommandExceptionType variadic dynamic exception type, maps to vanilla DynamicNCommandExceptionType
//Produces a Message from any number of arguments via a Func and creates a CommandSyntaxException
public sealed class DynamicNCommandExceptionType : ICommandExceptionType
{
    private readonly Func<object[], IMessage> _function;

    public DynamicNCommandExceptionType(Func<object[], IMessage> function)
    {
        _function = function;
    }

    public CommandSyntaxException Create(params object[] args)
        => new(this, _function(args));

    public CommandSyntaxException CreateWithContext(IImmutableStringReader reader, params object[] args)
        => new(this, _function(args), reader.String, reader.Cursor);
}
