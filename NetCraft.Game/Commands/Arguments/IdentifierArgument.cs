using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Registry;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//IdentifierArgument 标识符参数对应原版 IdentifierArgument
//贪婪读标识符合法字符再按namespace:path规则解析
public sealed class IdentifierArgument : ArgumentType<Identifier>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "012" };

    public static readonly SimpleCommandExceptionType ErrorInvalid =
        new(new TranslatableMessage("argument.id.invalid"));

    public static IdentifierArgument Id() => new();

    public Identifier Parse(StringReader reader) => ReadIdentifier(reader);

    //ReadIdentifier 贪婪读标识符字符并解析 非法时回滚游标抛异常
    public static Identifier ReadIdentifier(StringReader reader)
    {
        var start = reader.Cursor;
        while (reader.CanRead() && Identifier.IsAllowedInIdentifier(reader.Peek()))
            reader.Skip();
        var raw = reader.String[start..reader.Cursor];
        try
        {
            return Identifier.Parse(raw);
        }
        catch (IdentifierException)
        {
            reader.SetCursor(start);
            throw ErrorInvalid.CreateWithContext(reader);
        }
    }

    //GetId 取解析结果
    public static Identifier GetId(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Identifier>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
