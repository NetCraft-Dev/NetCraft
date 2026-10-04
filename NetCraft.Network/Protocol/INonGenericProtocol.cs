namespace NetCraft.Network.Protocol;

//INonGenericProtocol 非泛型协议接口
//Connection 存当前 inbound/outbound 协议时不依赖 THandler 用 object 装箱传递 Packet
//ProtocolInfo<THandler> 继承此接口用显式实现提供非泛型访问入口
public interface INonGenericProtocol
{
    //Id 协议枚举
    ConnectionProtocol Id { get; }

    //FlowDirection 包方向
    FlowDirection FlowDirection { get; }

    //DecodePacket 从缓冲区解码一个包返回 Packet<THandler> 装箱为 object
    object? DecodePacket(RegistryFriendlyByteBuf buf);

    //EncodePacket 编码一个包接受 Packet<THandler> 装箱为 object
    void EncodePacket(RegistryFriendlyByteBuf buf, object packet);

    //PacketIdFor 获取包的网络 ID 写入到缓冲区前缀
    int PacketIdFor(object packet);
}
