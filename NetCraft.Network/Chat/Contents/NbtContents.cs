using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//NBT contents, maps to vanilla net.minecraft.network.chat.contents.NbtContents
//NbtPath is the path, Interpreting whether to interpret as a component, Compiling the path compilation result, Separator the separator, DataSource a data source placeholder
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
