using NetCraft.Network.Protocol.Ping;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Status;

//ServerStatusPacketListenerImpl, the server status listener implementation
//Responds to ServerboundStatusRequestPacket with the ServerStatus JSON
//Ping requests are left unimplemented because StatusProtocols does not register the ping packet and that path is unreachable
public sealed class ServerStatusPacketListenerImpl : ServerStatusPacketListener
{
    private readonly Connection _connection;
    private readonly ServerStatus _status;

    public ServerStatusPacketListenerImpl(Connection connection, ServerStatus status)
    {
        _connection = connection;
        _status = status;
    }

    //HandleStatusRequest replies with ClientboundStatusResponsePacket carrying the ServerStatus JSON
    public void HandleStatusRequest(ServerboundStatusRequestPacket packet)
    {
        //Log.Debug("HandleStatusRequest entry");
        try
        {
            _connection.Send(new ClientboundStatusResponsePacket(_status));
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to send status response {e.Message}");
        }
        //Log.Debug("HandleStatusRequest exit");
    }

    //HandlePingRequest replies with ClientboundPongResponsePacket, echoing time so the client can compute latency
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
        //Log.Debug("HandlePingRequest exit");
    }

    public void OnDisconnect(string reason)
    {
        Log.Info($"status phase disconnect reason={reason}");
    }
}
