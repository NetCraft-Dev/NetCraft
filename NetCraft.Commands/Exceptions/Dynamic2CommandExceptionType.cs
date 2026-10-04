using NetCraft.Commands;

namespace NetCraft.Commands.Exceptions;

//Dynamic2CommandExceptionType 双参动态异常类型对应原版Dynamic2CommandExceptionType
//按两个参数通过Func生成Message创建CommandSyntaxException
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
