using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;
using UtilSyntaxException = NetCraft.Util.Parsing.Packrat.Commands.CommandSyntaxException;

namespace NetCraft.Game.Commands.Arguments;

//ComponentArgument 文本组件参数对应原版 net.minecraft.commands.arguments.ComponentArgument
//命令里写 SNBT 片段 "文本" {text:"文本"} [""] 解析后转成聊天组件
//注册在网络 id 18(component) 客户端按同 id 用原版解析器切词
public sealed class ComponentArgument : ArgumentType<Component>
{
    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "\"hello world\"", "'hello world'", "\"\"", "{text:\"hello world\"}", "[\"\"]" };

    private static readonly ComponentArgument Instance = new();

    public static ComponentArgument TextComponent() => Instance;

    public Component Parse(StringReader reader)
    {
        //SNBT 解析器有自己的游标 借它解析后再把位置写回命令的 reader
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        Tag tag;
        try
        {
            tag = TagParser<Tag>.ParseTagAsArgument(nbtReader);
        }
        catch (UtilSyntaxException e)
        {
            throw ErrorInvalidComponent.CreateWithContext(reader, e.RawMessage);
        }
        reader.SetCursor(nbtReader.Cursor);
        return ComponentSerialization.FromTag(tag);
    }

    //ErrorInvalidComponent 组件语法错误 对应原版 ERROR_INVALID_COMPONENT
    private static readonly DynamicCommandExceptionType ErrorInvalidComponent =
        new(arg => new LiteralMessage($"文本组件不合法: {arg}"));

    //GetRawComponent 取解析出的组件 对应原版 getRawComponent
    //原版还有解析选择器占位的 getResolvedComponent NC 组件体系暂无选择器内容所以不提供
    public static Component GetRawComponent(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Component>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
