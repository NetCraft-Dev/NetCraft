using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;
using UtilSyntaxException = NetCraft.Util.Parsing.Packrat.Commands.CommandSyntaxException;

namespace NetCraft.Game.Commands.Arguments;

//NbtTagArgument 任意 NBT 标签参数对应原版 net.minecraft.commands.arguments.NbtTagArgument
//命令里写成 0 0.0 {} {foo=bar} 这类 SNBT 片段 与只认复合标签的 CompoundTagArgument 互补
//注册在网络 id 22(nbt_tag) 客户端按同 id 用原版解析器切词
public sealed class NbtTagArgument : ArgumentType<Tag>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "0", "0.0", "{}", "{foo=bar}" };

    private static readonly NbtTagArgument Instance = new();

    public static NbtTagArgument NbtTag() => Instance;

    public Tag Parse(StringReader reader)
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
            throw ErrorInvalidTag.CreateWithContext(reader, e.RawMessage);
        }
        reader.SetCursor(nbtReader.Cursor);
        return tag;
    }

    //ErrorInvalidTag 标签语法错误 对应原版 ERROR_INVALID_TYPE
    private static readonly DynamicCommandExceptionType ErrorInvalidTag =
        new(arg => new LiteralMessage($"NBT 标签不合法: {arg}"));

    //GetNbtTag 取解析出的标签
    public static Tag GetNbtTag(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Tag>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
