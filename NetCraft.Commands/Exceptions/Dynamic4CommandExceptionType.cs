using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//Dynamic4CommandExceptionType 四参动态异常类型对应原版Dynamic4CommandExceptionType
//按四个参数通过Func生成Message创建CommandSyntaxException
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
