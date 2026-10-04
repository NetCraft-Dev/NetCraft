using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Nbt;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//CompoundTagArgument SNBT 复合标签参数对应原版 CompoundTagArgument
//命令里写成 {...} 供 summon 这类命令传实体的初始 NBT
//注册在网络 id 21(nbt_compound_tag) 客户端按同 id 用原版解析器切词 语法与表现一致
public sealed class CompoundTagArgument : ArgumentType<CompoundTag>
{
    //Instance 无附加参数的单例 与其它参数类型的工厂写法一致
    public static readonly CompoundTagArgument Instance = new();

    public static CompoundTagArgument CompoundTag() => Instance;

    public CompoundTag Parse(StringReader reader)
    {
        //SNBT 解析器有自己的游标 借它解析后再把位置写回命令的 reader
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        var tag = TagParser<Tag>.ParseCompoundAsArgument(nbtReader);
        reader.SetCursor(nbtReader.Cursor);
        return tag;
    }

    //GetCompoundTag 取解析出的复合标签
    public static CompoundTag GetCompoundTag(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<CompoundTag>(name);
}
