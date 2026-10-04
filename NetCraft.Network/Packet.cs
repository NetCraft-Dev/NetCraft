namespace NetCraft.Network;

//IPacket 非泛型包接口
//C# 泛型不变体下 Packet<父监听器> 不能转 Packet<子监听器> common 包桥接发送时需按 ID 派发
public interface IPacket
{
    //PacketTypeId 包类型的网络 ID
    //不叫 TypeId 是因为包自带的业务字段常叫 TypeId(方块实体类型/实体类型)
    //同名属性会隐式实现接口成员并盖掉默认实现 网络 ID 会被当成业务字段发出去
    int PacketTypeId { get; }
}

//Packet 协议包接口对应原版 net.minecraft.network.protocol.Packet
//THandler 是协议处理器类型提供 Handle 方法接收处理器
public interface Packet<THandler> : IPacket
{
    //PacketType 包类型标识用于注册和编码
    PacketType<THandler> Type { get; }

    //IPacket.PacketTypeId 显式实现从泛型 Type 取网络 ID
    int IPacket.PacketTypeId => Type.Id;

    //Handle 调用处理器的对应方法
    void Handle(THandler handler);

    //IsSkippable 是否可跳过默认 false 对齐原版 isSkippable
    //解码失败时若可跳过则丢弃包继续否则抛异常
    bool IsSkippable => false;

    //IsTerminal 是否终止包默认 false 对齐原版 isTerminal
    //终止包处理后关闭连接如 LoginDisconnectPacket
    bool IsTerminal => false;
}
