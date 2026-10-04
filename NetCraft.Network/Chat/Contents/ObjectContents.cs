using NetCraft.Codec;

namespace NetCraft.Network.Chat.Contents;

//对象内容占位对应原版net.minecraft.network.chat.contents.ObjectContents
//运行时承载任意对象供自定义渲染使用当前为 stub 待业务扩展
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
