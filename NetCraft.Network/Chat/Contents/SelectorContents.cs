using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//选择器内容对应原版net.minecraft.network.chat.contents.SelectorContents
//Pattern 实体选择器字符串运行时由 EntitySelector 解析
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
