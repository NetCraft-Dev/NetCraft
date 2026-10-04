using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//命令语法异常对应原版com.mojang.brigadier.exceptions.CommandSyntaxException
//解析失败时携带cursor位置抛出
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

//简单异常类型工厂对应原版SimpleCommandExceptionType
//持有固定消息createWithContext在指定reader位置创建异常
public sealed class SimpleCommandExceptionType
{
    private readonly string _message;

    public SimpleCommandExceptionType(string message) => _message = message;

    public CommandSyntaxException CreateWithContext(CommandStringReader reader)
        => new(_message, reader.Cursor);
}

//动态异常类型工厂对应原版DynamicCommandExceptionType
//根据参数生成消息createWithContext在指定reader位置创建异常
public sealed class DynamicCommandExceptionType
{
    private readonly Func<object?, string> _messageFactory;

    public DynamicCommandExceptionType(Func<object?, string> messageFactory)
        => _messageFactory = messageFactory;

    public CommandSyntaxException CreateWithContext(CommandStringReader reader, object? arg)
        => new(_messageFactory(arg), reader.Cursor);
}
