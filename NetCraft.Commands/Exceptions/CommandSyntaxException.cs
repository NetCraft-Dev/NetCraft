using System.Text;
using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//CommandSyntaxException 命令语法异常对应原版com.mojang.brigadier.exceptions.CommandSyntaxException
//携带type与message可选携带input与cursor用于上下文定位
//静态字段BuiltInExceptions可运行时替换EnableCommandStackTraces控制栈追踪
public sealed class CommandSyntaxException : Exception
{
    public const int ContextAmount = 10;
    public static bool EnableCommandStackTraces = true;
    public static IBuiltInExceptionProvider BuiltInExceptions { get; set; } = new BuiltInExceptions();

    private readonly ICommandExceptionType _type;
    private readonly IMessage _message;
    private readonly string? _input;
    private readonly int _cursor;

    public CommandSyntaxException(ICommandExceptionType type, IMessage message)
        : base(message.GetString(), null)
    {
        _type = type;
        _message = message;
        _input = null;
        _cursor = -1;
    }

    public CommandSyntaxException(ICommandExceptionType type, IMessage message, string? input, int cursor)
        : base(message.GetString(), null)
    {
        _type = type;
        _message = message;
        _input = input;
        _cursor = cursor;
    }

    public override string Message
    {
        get
        {
            var message = _message.GetString();
            var context = GetContext();
            if (context != null)
            {
                message += " at position " + _cursor + ": " + context;
            }
            return message;
        }
    }

    public IMessage RawMessage => _message;

    public string? GetContext()
    {
        if (_input == null || _cursor < 0)
        {
            return null;
        }
        var builder = new StringBuilder();
        var cursor = Math.Min(_input.Length, _cursor);
        if (cursor > ContextAmount)
        {
            builder.Append("...");
        }
        builder.Append(_input.Substring(Math.Max(0, cursor - ContextAmount), cursor - Math.Max(0, cursor - ContextAmount)));
        builder.Append("<--[HERE]");
        return builder.ToString();
    }

    public ICommandExceptionType Type => _type;
    public string? Input => _input;
    public int Cursor => _cursor;
}
