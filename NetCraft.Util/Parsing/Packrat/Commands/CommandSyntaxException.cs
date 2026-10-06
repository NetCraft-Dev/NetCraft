using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//Command syntax exception, maps to vanilla com.mojang.brigadier.exceptions.CommandSyntaxException
//Thrown on parse failure carrying the cursor position
public sealed class CommandSyntaxException : Exception
{
    public string RawMessage { get; }
    public int Cursor { get; }

    public CommandSyntaxException(string message, int cursor)
        : base(message)
    {
        RawMessage = message;
        Cursor = cursor;
    }

    public override string Message => RawMessage;
}

//Simple exception type factory, maps to vanilla SimpleCommandExceptionType
//Holds a fixed message, createWithContext creates the exception at the given reader position
public sealed class SimpleCommandExceptionType
{
    private readonly string _message;

    public SimpleCommandExceptionType(string message) => _message = message;

    public CommandSyntaxException CreateWithContext(CommandStringReader reader)
        => new(_message, reader.Cursor);
}

//Dynamic exception type factory, maps to vanilla DynamicCommandExceptionType
//Generates a message from arguments, createWithContext creates the exception at the given reader position
public sealed class DynamicCommandExceptionType
{
    private readonly Func<object?, string> _messageFactory;

    public DynamicCommandExceptionType(Func<object?, string> messageFactory)
        => _messageFactory = messageFactory;

    public CommandSyntaxException CreateWithContext(CommandStringReader reader, object? arg)
        => new(_messageFactory(arg), reader.Cursor);
}
