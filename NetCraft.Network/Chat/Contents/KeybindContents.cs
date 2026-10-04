using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//按键绑定内容对应原版net.minecraft.network.chat.contents.KeybindContents
//Name 按键名运行时由 KeybindMapping 翻译为本地化文本
public sealed record KeybindContents(string Name) : ComponentContents
{
    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    public override string ToString() => $"keybind{{{Name}}}";
}
