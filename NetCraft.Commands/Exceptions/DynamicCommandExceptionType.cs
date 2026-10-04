using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//DynamicCommandExceptionType 单参动态异常类型对应原版DynamicCommandExceptionType
//按参数通过Func生成Message创建CommandSyntaxException
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
