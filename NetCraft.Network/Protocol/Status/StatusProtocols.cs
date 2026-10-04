using NetCraft.Network.Protocol.Ping;

namespace NetCraft.Network.Protocol.Status;

//StatusProtocols status 协议对应原版 net.minecraft.network.protocol.status.StatusProtocols
//原版在同一协议注册 status 请求/响应与 ping 请求/pong 响应
//ping 包声明成 Packet<ServerPingPacketListener>/Packet<ClientPongPacketListener> 对齐原版 Packet<? super T> 逆变
//Status 协议用 AddPacketCommon 桥接到 ServerStatusPacketListener/ClientStatusPacketListener
public static class StatusProtocols
{
    //ServerboundTemplate SERVERBOUND status 协议模板含 status 请求与 ping 请求
    public static readonly SimpleUnboundProtocol<ServerStatusPacketListener> ServerboundTemplate =
        new ProtocolInfoBuilder<ServerStatusPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Serverbound)
            .AddPacket(StatusPacketTypes.ServerboundStatusRequest, ServerboundStatusRequestPacket.StreamCodec)
            .AddPacketCommon(PingPacketTypes.ServerboundPingRequest, ServerboundPingRequestPacket.StreamCodec)
            .BuildUnbound();

    //Serverbound 绑定后的 SERVERBOUND ProtocolInfo
    public static readonly ProtocolInfo<ServerStatusPacketListener> Serverbound =
        ServerboundTemplate.Bind();

    //ClientboundTemplate CLIENTBOUND status 协议模板含 status 响应与 pong 响应
    public static readonly SimpleUnboundProtocol<ClientStatusPacketListener> ClientboundTemplate =
        new ProtocolInfoBuilder<ClientStatusPacketListener>(
            ConnectionProtocol.Status, FlowDirection.Clientbound)
            .AddPacket(StatusPacketTypes.ClientboundStatusResponse, ClientboundStatusResponsePacket.StreamCodec)
            .AddPacketCommon(PingPacketTypes.ClientboundPongResponse, ClientboundPongResponsePacket.StreamCodec)
            .BuildUnbound();

    //Clientbound 绑定后的 CLIENTBOUND ProtocolInfo
    public static readonly ProtocolInfo<ClientStatusPacketListener> Clientbound =
        ClientboundTemplate.Bind();
}
