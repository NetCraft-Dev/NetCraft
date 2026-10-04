using NetCraft.Config;
using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Network.Protocol.Configuration;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Handshake;
using NetCraft.Network.Protocol.Login;
using NetCraft.Network.Protocol.Status;

namespace NetCraft.Game.Network;

//ConnectionExtensions Connection 业务包相关扩展方法
//从 Network/Connection.cs 抽离业务包依赖逻辑至此
//内核 Connection 只保留通用框架握手/状态/登录协议注册由 Game 层做
public static class ConnectionExtensions
{
    //SetListenerForServerboundHandshake 设置服务端初始握手监听器
    //对齐原版 Connection.setListenerForServerboundHandshake
    public static void SetListenerForServerboundHandshake(this Connection connection, PacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("非服务端连接不能设置握手监听器");
        if (listener.Flow != FlowDirection.Serverbound)
            throw new InvalidOperationException("握手监听器方向必须为 Serverbound");
        if (listener.Protocol != ConnectionProtocol.Handshake)
            throw new InvalidOperationException("握手监听器协议必须为 Handshake");
        connection.SetInitialInboundProtocolInternal(listener, HandshakeProtocols.Serverbound);
    }

    //SetListenerForServerboundStatus 设置服务端 status 阶段监听器
    public static void SetListenerForServerboundStatus(this Connection connection, ServerStatusPacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("非服务端连接不能设置 status 监听器");
        if (listener.Protocol != ConnectionProtocol.Status)
            throw new InvalidOperationException("status 监听器协议必须为 Status");
        connection.SetupInboundProtocol(StatusProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(StatusProtocols.Clientbound);
    }

    //SetListenerForServerboundLogin 设置服务端 login 阶段监听器
    public static void SetListenerForServerboundLogin(this Connection connection, ServerLoginPacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("非服务端连接不能设置 login 监听器");
        if (listener.Protocol != ConnectionProtocol.Login)
            throw new InvalidOperationException("login 监听器协议必须为 Login");
        connection.SetupInboundProtocol(LoginProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(LoginProtocols.Clientbound);
    }

    //SetListenerForServerboundConfiguration 设置服务端 configuration 阶段监听器
    public static void SetListenerForServerboundConfiguration(this Connection connection, ServerConfigurationPacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("非服务端连接不能设置 configuration 监听器");
        if (listener.Protocol != ConnectionProtocol.Configuration)
            throw new InvalidOperationException("configuration 监听器协议必须为 Configuration");
        connection.SetupInboundProtocol(ConfigurationProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(ConfigurationProtocols.Clientbound);
    }

    //SetListenerForServerboundGame 设置服务端 play 阶段监听器
    //listener.Protocol 显式返回 Play 因继承链默认 Protocol 是 Configuration
    public static void SetListenerForServerboundGame(this Connection connection, ServerGamePacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("非服务端连接不能设置 play 监听器");
        if (listener.Protocol != ConnectionProtocol.Play)
            throw new InvalidOperationException("play 监听器协议必须为 Play");
        connection.SetupInboundProtocol(GameProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(GameProtocols.Clientbound);
    }

    //InitiateServerboundStatusConnection 客户端发起状态查询连接
    //对齐原版 Connection.initiateServerboundStatusConnection
    public static void InitiateServerboundStatusConnection(
        this Connection connection,
        string hostName, int port,
        ClientStatusPacketListener listener,
        int protocolVersion = SharedConstants.ProtocolVersion)
    {
        InitiateServerboundConnection(
            connection,
            hostName, port,
            StatusProtocols.Serverbound,
            StatusProtocols.Clientbound,
            listener,
            ClientIntent.Status,
            protocolVersion);
    }

    //InitiateServerboundLoginConnection 客户端发起登录连接
    //对齐原版 initiateServerboundPlayConnection 命名沿用原版 Play 实为 Login
    public static void InitiateServerboundLoginConnection(
        this Connection connection,
        string hostName, int port,
        ClientLoginPacketListener listener,
        int protocolVersion = SharedConstants.ProtocolVersion)
    {
        InitiateServerboundConnection(
            connection,
            hostName, port,
            LoginProtocols.Serverbound,
            LoginProtocols.Clientbound,
            listener,
            ClientIntent.Login,
            protocolVersion);
    }

    //InitiateServerboundConnection 通用客户端发起流程
    //对齐原版 initiateServerboundConnection
    //发送 ClientIntention 后切换出站协议到目标协议
    public static void InitiateServerboundConnection<S, C>(
        this Connection connection,
        string hostName, int port,
        ProtocolInfo<S> outbound,
        ProtocolInfo<C> inbound,
        C listener,
        ClientIntent intent,
        int protocolVersion)
        where S : class, PacketListener
        where C : class, PacketListener
    {
        if (outbound.Id != inbound.Id)
            throw new InvalidOperationException("出入站协议不匹配");
        connection.SetDisconnectListenerInternal(listener);
        connection.RunOnceConnected(conn =>
        {
            conn.SetupInboundProtocol(inbound, listener);
            conn.SetupOutboundProtocol(HandshakeProtocols.Serverbound);
            var intention = new ClientIntentionPacket(protocolVersion, hostName, port, intent);
            conn.Send(intention);
            conn.SetupOutboundProtocol(outbound);
        });
    }
}
