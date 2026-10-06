using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//Dynamic3CommandExceptionType three-argument dynamic exception type, maps to vanilla Dynamic3CommandExceptionType
//Produces a Message from three arguments via a Func and creates a CommandSyntaxException
public sealed class Dynamic3CommandExceptionType : ICommandExceptionType
{
    private readonly Func<object, object, object, IMessage> _function;

    public Dynamic3CommandExceptionType(Func<object, object, object, IMessage> function)
    {
        _function = function;
    }

    public CommandSyntaxException Create(object a, object b, object c)
        => new(this, _function(a, b, c));

    public CommandSyntaxException CreateWithContext(IImmutableStringReader reader, object a, object b, object c)
        => new(this, _function(a, b, c), reader.String, reader.Cursor);
}
