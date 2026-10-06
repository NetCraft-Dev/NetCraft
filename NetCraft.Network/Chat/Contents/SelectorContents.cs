using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//Selector contents, maps to vanilla net.minecraft.network.chat.contents.SelectorContents
//Pattern is the entity selector string, parsed at runtime by EntitySelector
public sealed class SelectorContents : ComponentContents
{
    public string Pattern { get; }
    public string? Separator { get; }

    public SelectorContents(string pattern, string? separator)
    {
        Pattern = pattern;
        Separator = separator;
    }

    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    public override string ToString() => $"selector{{{Pattern}}}";
}
