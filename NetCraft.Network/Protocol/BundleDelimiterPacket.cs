namespace NetCraft.Network.Protocol;

//BundleDelimiterPacket 包分隔符基类对应原版 net.minecraft.network.protocol.BundleDelimiterPacket
//标识 bundle 起止边界包实际由 pipeline 处理不应到达 Handle
public abstract class BundleDelimiterPacket<THandler> : Packet<THandler>
{
    //Type 子类提供具体包类型标识
    public abstract PacketType<THandler> Type { get; }

    //Handle 抛异常分隔符包不应被处理器接收
    public void Handle(THandler handler)
        => throw new InvalidOperationException("分隔符包应由 pipeline 处理不应到达 Handle");
}
