using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//NBT内容对应原版net.minecraft.network.chat.contents.NbtContents
//NbtPath 路径 Interpreting 是否作为组件解释 Compiling 路径编译结果 Separator 分隔符 DataSource 数据源占位
public sealed class NbtContents : ComponentContents
{
    public string NbtPath { get; }
    public bool Interpreting { get; }
    public bool Compiling { get; }
    public string? Separator { get; }
    public object? DataSource { get; }

    public NbtContents(string nbtPath, bool interpreting, bool compiling, string? separator, object? dataSource)
    {
        NbtPath = nbtPath;
        Interpreting = interpreting;
        Compiling = compiling;
        Separator = separator;
        DataSource = dataSource;
    }

    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    public override string ToString() => $"nbt{{{NbtPath}}}";
}
