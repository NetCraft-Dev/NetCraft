using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//Keybind contents, maps to vanilla net.minecraft.network.chat.contents.KeybindContents
//Name is the key name, translated at runtime into localized text by KeybindMapping
public sealed record KeybindContents(string Name) : ComponentContents
{
    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    public override string ToString() => $"keybind{{{Name}}}";
}
