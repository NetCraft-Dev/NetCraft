using NetCraft.Registry;

namespace NetCraft.Network;

//IPacketType 非泛型包类型接口
//PacketTypeRegistry 按 (Protocol, Direction, Id) 索引不依赖 THandler 用此接口
public interface IPacketType
{
    //Id 简化版网络 ID 由注册顺序分配
    int Id { get; }
    //Protocol 所属协议
    ConnectionProtocol Protocol { get; }
    //Direction 方向与 FlowDirection 一致
    FlowDirection Direction { get; }
}

//PacketType 包类型对应原版 net.minecraft.network.protocol.PacketType
//注册到 PacketTypeRegistry 每种协议包有唯一 id
//同时支持简化版 int Id（Connection.cs 用）和原版 Identifier（ProtocolInfoBuilder 用）
public sealed class PacketType<THandler> : IPacketType
{
    //Id 简化版网络 ID 由注册顺序分配
    public int Id { get; }
    //Protocol 所属协议
    public ConnectionProtocol Protocol { get; }
    //Direction 方向与 FlowDirection 一致
    public FlowDirection Direction { get; }
    //Codec 包编解码器
    public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>? Codec { get; }
    //Identifier 原版标识符对齐原版 PacketType.id
    //默认 null 不要求原版体系时可不设置
    public Identifier? Identifier { get; set; }

    public PacketType(int id, ConnectionProtocol protocol, FlowDirection direction, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>? codec)
    {
        Id = id;
        Protocol = protocol;
        Direction = direction;
        Codec = codec;
    }

    //WithIdentifier 设置原版 Identifier 返回 this 便于链式
    public PacketType<THandler> WithIdentifier(Identifier identifier)
    {
        Identifier = identifier;
        return this;
    }

    public override string ToString() =>
        Identifier.HasValue ? $"{Protocol.Id()}/{Direction}/{Identifier.Value}" : $"PacketType[{Protocol}/{Direction} #{Id}]";
}

//FlowDirection 包方向对应原版 PacketFlow
//Clientbound 服务端发往客户端
//Serverbound 客户端发往服务端
public enum FlowDirection
{
    Clientbound,
    Serverbound
}

//FlowDirectionExtensions FlowDirection 扩展方法对齐原版 PacketFlow.id/getOpposite
public static class FlowDirectionExtensions
{
    //Id 返回方向小写名字符串
    public static string Id(this FlowDirection direction) => direction switch
    {
        FlowDirection.Clientbound => "clientbound",
        FlowDirection.Serverbound => "serverbound",
        _ => direction.ToString().ToLowerInvariant(),
    };

    //GetOpposite 返回反方向
    public static FlowDirection GetOpposite(this FlowDirection direction) =>
        direction == FlowDirection.Clientbound ? FlowDirection.Serverbound : FlowDirection.Clientbound;

    //ToPacketFlow 转为 Protocol.PacketFlow
    public static Protocol.PacketFlow ToPacketFlow(this FlowDirection direction) =>
        direction == FlowDirection.Clientbound ? Protocol.PacketFlow.Clientbound : Protocol.PacketFlow.Serverbound;
}
