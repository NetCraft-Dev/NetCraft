using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Login;

//ClientLoginPacketListenerImpl 客户端 login 监听器实现
//对应原版 ClientLoginPacketListenerImpl 简化版跳过加密
//HandleLoginFinished 发 LoginAcknowledged 切 Configuration 交 configListener 推进
public sealed class ClientLoginPacketListenerImpl : ClientLoginPacketListener
{
    private readonly Connection _connection;
    private readonly ClientConfigurationPacketListenerImpl _configurationListener;

    public ClientLoginPacketListenerImpl(Connection connection, ClientConfigurationPacketListenerImpl configurationListener)
    {
        _connection = connection;
        _configurationListener = configurationListener;
    }

    //HandleLoginFinished 收到登录完成发 LoginAcknowledged 切 Configuration 协议
    //S4 对齐原版流程等待服务端 SelectKnownPacks/FinishConfiguration 由 configListener 收包推进 Play
    public void HandleLoginFinished(ClientboundLoginFinishedPacket packet)
    {
        Log.Info($"Login finished profile={packet.GameProfile.Name}, switching to Configuration phase");
        _connection.Send(ServerboundLoginAcknowledgedPacket.Instance);
        _connection.SetupInboundProtocol(ConfigurationProtocols.Clientbound, _configurationListener);
        _connection.SetupOutboundProtocol(ConfigurationProtocols.Serverbound);
    }

    //HandleHello 服务器 hello 无加密流程不调用
    public void HandleHello(ClientboundHelloPacket packet) { }

    //HandleDisconnect 登录阶段被拒
    public void HandleDisconnect(ClientboundLoginDisconnectPacket packet)
        => Log.Warning($"Login rejected {packet.Reason}");

    //HandleCompression 压缩协商简化版不启用压缩空实现
    public void HandleCompression(ClientboundLoginCompressionPacket packet) { }

    //HandleCustomQuery 自定义查询空实现
    public void HandleCustomQuery(ClientboundCustomQueryPacket packet) { }

    public void OnDisconnect(string reason)
        => Log.Info($"login phase disconnect reason={reason}");
}
