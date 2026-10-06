using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//Dynamic4CommandExceptionType four-argument dynamic exception type, maps to vanilla Dynamic4CommandExceptionType
//Produces a Message from four arguments via a Func and creates a CommandSyntaxException
public sealed class Dynamic4CommandExceptionType : ICommandExceptionType
{
    private readonly Func<object, object, object, object, IMessage> _function;

    public Dynamic4CommandExceptionType(Func<object, object, object, object, IMessage> function)
    {
        _function = function;
    }

    public CommandSyntaxException Create(object a, object b, object c, object d)
        => new(this, _function(a, b, c, d));

    public CommandSyntaxException CreateWithContext(IImmutableStringReader reader, object a, object b, object c, object d)
        => new(this, _function(a, b, c, d), reader.String, reader.Cursor);
}
