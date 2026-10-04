using NetCraft.Network.Protocol.Ping;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Status;

//ServerStatusPacketListenerImpl 服务端 status 监听器实现
//响应 ServerboundStatusRequestPacket 返回 ServerStatus JSON
//ping 请求暂空实现因 StatusProtocols 不注册 ping 包流程跑不到
public sealed class ServerStatusPacketListenerImpl : ServerStatusPacketListener
{
    private readonly Connection _connection;
    private readonly ServerStatus _status;

    public ServerStatusPacketListenerImpl(Connection connection, ServerStatus status)
    {
        _connection = connection;
        _status = status;
    }

    //HandleStatusRequest 回 ClientboundStatusResponsePacket 含 ServerStatus JSON
    public void HandleStatusRequest(ServerboundStatusRequestPacket packet)
    {
        //Log.Debug("HandleStatusRequest 入口");
        try
        {
            _connection.Send(new ClientboundStatusResponsePacket(_status));
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to send status response {e.Message}");
        }
        //Log.Debug("HandleStatusRequest 出口");
    }

    //HandlePingRequest 回 ClientboundPongResponsePacket 回传 time 供客户端算延迟
    public void HandlePingRequest(ServerboundPingRequestPacket packet)
    {
        Log.Debug($"HandlePingRequest entry time={packet.Time}");
        try
        {
            _connection.Send(new ClientboundPongResponsePacket(packet.Time));
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to send pong response {e.Message}");
        }
        //Log.Debug("HandlePingRequest 出口");
    }

    public void OnDisconnect(string reason)
    {
        Log.Info($"status phase disconnect reason={reason}");
    }
}
