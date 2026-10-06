using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//Object contents placeholder, maps to vanilla net.minecraft.network.chat.contents.ObjectContents
//Carries an arbitrary object at runtime for custom rendering; currently a stub, to be extended by the business layer
public sealed class ObjectContents : ComponentContents
{
    public object? Value { get; }

    public ObjectContents(object? value)
    {
        Value = value;
    }

    public MapCodec<ComponentContents> Codec() => throw new NotImplementedException();

    public override string ToString() => $"object{{{Value}}}";
}
