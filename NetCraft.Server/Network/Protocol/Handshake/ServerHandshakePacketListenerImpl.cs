using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Protocol;

namespace NetCraft.Network.Protocol.Handshake;

//ServerHandshakePacketListenerImpl, the server handshake listener implementation
//Handles ClientIntentionPacket and routes to the Status/Login phase by Intention
//An invalid intention disconnects, maps to vanilla ServerHandshakePacketListenerImpl
public sealed class ServerHandshakePacketListenerImpl : ServerHandshakePacketListener
{
    private readonly Connection _connection;
    private readonly ServerHandshakeContext _context;

    public ServerHandshakePacketListenerImpl(Connection connection, ServerHandshakeContext context)
    {
        _connection = connection;
        _context = context;
    }

    //HandleIntention routes the client intention to the matching protocol phase
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
        //Log.Debug("HandleIntention exit");
    }

    public void OnDisconnect(string reason)
    {
        Log.Debug($"handshake phase disconnect reason={reason}");
    }
}

//ServerHandshakeContext, handshake phase context
//Implemented by DedicatedServer, wraps the routing to the Status/Login listeners
//Decouples the listener from DedicatedServer to avoid a circular dependency
public interface ServerHandshakeContext
{
    //TransitionToStatus switches the connection to the Status phase and attaches ServerStatusPacketListenerImpl
    void TransitionToStatus(Connection connection);

    //TransitionToLogin switches the connection to the Login phase and attaches ServerLoginPacketListenerImpl
    void TransitionToLogin(Connection connection);
}
