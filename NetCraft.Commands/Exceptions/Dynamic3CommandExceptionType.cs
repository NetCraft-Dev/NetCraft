using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//Dynamic3CommandExceptionType 三参动态异常类型对应原版Dynamic3CommandExceptionType
//按三个参数通过Func生成Message创建CommandSyntaxException
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
