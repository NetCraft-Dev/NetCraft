using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Protocol;

namespace NetCraft.Network.Protocol.Handshake;

//ServerHandshakePacketListenerImpl 服务端握手监听器实现
//处理 ClientIntentionPacket 按 Intention 路由到 Status/Login 阶段
//不合法 intention 断连对应原版 ServerHandshakePacketListenerImpl
public sealed class ServerHandshakePacketListenerImpl : ServerHandshakePacketListener
{
    private readonly Connection _connection;
    private readonly ServerHandshakeContext _context;

    public ServerHandshakePacketListenerImpl(Connection connection, ServerHandshakeContext context)
    {
        _connection = connection;
        _context = context;
    }

    //HandleIntention 路由客户端意图到对应协议阶段
    public void HandleIntention(ClientIntentionPacket packet)
    {
        Log.Debug($"HandleIntention entry intention={packet.Intention} protocol={packet.ProtocolVersion}");
        switch (packet.Intention)
        {
            case ClientIntent.Status:
                _context.TransitionToStatus(_connection);
                break;
            case ClientIntent.Login:
                _context.TransitionToLogin(_connection);
                break;
            case ClientIntent.Transfer:
                Log.Warning("Transfer intention is not supported yet, disconnecting");
                _connection.Disconnect("Transfer not supported");
                break;
            default:
                Log.Warning($"Unknown intention={packet.Intention}, disconnecting");
                _connection.Disconnect("Unknown intention");
                break;
        }
        //Log.Debug("HandleIntention 出口");
    }

    public void OnDisconnect(string reason)
    {
        Log.Debug($"handshake phase disconnect reason={reason}");
    }
}

//ServerHandshakeContext 握手阶段上下文
//由 DedicatedServer 实现封装路由到 Status/Login 监听器的逻辑
//解耦监听器与 DedicatedServer 避免循环依赖
public interface ServerHandshakeContext
{
    //TransitionToStatus 切换连接到 Status 阶段挂 ServerStatusPacketListenerImpl
    void TransitionToStatus(Connection connection);

    //TransitionToLogin 切换连接到 Login 阶段挂 ServerLoginPacketListenerImpl
    void TransitionToLogin(Connection connection);
}
