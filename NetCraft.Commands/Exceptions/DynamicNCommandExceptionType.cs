using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//DynamicNCommandExceptionType 变参动态异常类型对应原版DynamicNCommandExceptionType
//按任意数量参数通过Func生成Message创建CommandSyntaxException
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
