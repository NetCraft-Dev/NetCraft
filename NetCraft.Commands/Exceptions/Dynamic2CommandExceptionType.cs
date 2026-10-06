using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//Dynamic2CommandExceptionType two-argument dynamic exception type, maps to vanilla Dynamic2CommandExceptionType
//Produces a Message from two arguments via a Func and creates a CommandSyntaxException
public sealed class Dynamic2CommandExceptionType : ICommandExceptionType
{
    private readonly Func<object, object, IMessage> _function;

    public Dynamic2CommandExceptionType(Func<object, object, IMessage> function)
    {
        _function = function;
    }

    public CommandSyntaxException Create(object a, object b)
        => new(this, _function(a, b));

    public CommandSyntaxException CreateWithContext(IImmutableStringReader reader, object a, object b)
        => new(this, _function(a, b), reader.String, reader.Cursor);
}
