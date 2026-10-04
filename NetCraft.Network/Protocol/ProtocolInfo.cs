namespace NetCraft.Network.Protocol;

//ProtocolInfo 协议信息对应原版 net.minecraft.network.protocol.ProtocolInfo
//绑定上下文后的协议信息包含编解码器和打包信息
//THandler 是包处理器类型
//继承 INonGenericProtocol 让 Connection 可以不依赖 THandler 存当前协议
public interface ProtocolInfo<THandler> : INonGenericProtocol
{
    //Id 协议枚举（new 隐藏 INonGenericProtocol.Id 用泛型协议的 Id）
    new ConnectionProtocol Id { get; }

    //Flow 包方向
    PacketFlow Flow { get; }

    //Codec 包编解码器
    StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Codec { get; }

    //BundlerInfo 包打包信息
    BundlerInfo<THandler> BundlerInfo { get; }

    //显式实现 INonGenericProtocol.FlowDirection 从 PacketFlow 转换
    FlowDirection INonGenericProtocol.FlowDirection
        => Flow == PacketFlow.Clientbound ? FlowDirection.Clientbound : FlowDirection.Serverbound;

    //显式实现 INonGenericProtocol.DecodePacket 委托给 Codec.Decode
    object? INonGenericProtocol.DecodePacket(RegistryFriendlyByteBuf buf) => Codec.Decode(buf);

    //显式实现 INonGenericProtocol.EncodePacket 委托给 Codec.Encode
    //common 包静态类型是 Packet<父监听器> 不能强转 Packet<THandler> 优先走 object 桥接路径
    void INonGenericProtocol.EncodePacket(RegistryFriendlyByteBuf buf, object packet)
    {
        if (Codec is IObjectEncodable encodable)
        {
            encodable.EncodeObject(buf, packet);
            return;
        }
        Codec.Encode(buf, (Packet<THandler>)packet);
    }

    //显式实现 INonGenericProtocol.PacketIdFor 取包 Type.Id
    int INonGenericProtocol.PacketIdFor(object packet)
        => packet is IPacket p ? p.PacketTypeId : ((Packet<THandler>)packet).Type.Id;

    //Details 协议静态详情按方向列出所有包
    public interface Details
    {
        //Id 协议枚举
        ConnectionProtocol Id { get; }

        //Flow 方向
        PacketFlow Flow { get; }

        //ListPackets 遍历所有包类型及其网络 ID
        void ListPackets(PacketVisitor output);

        //PacketVisitor 包访问者
        public interface PacketVisitor
        {
            void Accept(PacketType<THandler> type, int networkId);
        }
    }

    //DetailsProvider 提供 Details
    public interface DetailsProvider
    {
        Details Details { get; }
    }
}
