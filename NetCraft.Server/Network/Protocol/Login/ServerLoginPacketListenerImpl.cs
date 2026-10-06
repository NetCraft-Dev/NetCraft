using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Network.Protocol.Login;

//ServerLoginPacketListenerImpl, the server login listener implementation
//A simplified version that skips encryption, sends LoginFinished right after Hello, then waits for the client LoginAcknowledged to switch to Configuration
//Passes through when online mode is off, Mojang authentication is not supported yet when it is on
public sealed class ServerLoginPacketListenerImpl : ServerLoginPacketListener
{
    private readonly Connection _connection;
    private readonly ServerLoginContext _context;
    private GameProfile? _profile;

    public ServerLoginPacketListenerImpl(Connection connection, ServerLoginContext context)
    {
        _connection = connection;
        _context = context;
    }

    //HandleHello stores the GameProfile after the client hello and sends LoginFinished directly, skipping encryption
    public void HandleHello(ServerboundHelloPacket packet)
    {
        Log.Debug($"HandleHello entry name={packet.Name} profileId={packet.ProfileId}");
        _profile = new GameProfile(packet.ProfileId, packet.Name);
        try
        {
            _connection.Send(new ClientboundLoginFinishedPacket(_profile, Guid.NewGuid()));
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to send LoginFinished {e.Message}");
            _connection.Disconnect("login failed");
        }
        //Log.Debug("HandleHello exit");
    }

    //HandleKey, the encryption packet is not called this round since encryption is skipped, left unimplemented
    public void HandleKey(ServerboundKeyPacket packet)
    {
        Log.Debug("HandleKey entry, encryption skipped");
    }

    //HandleCustomQueryPacket, custom query packet, left unimplemented
    public void HandleCustomQueryPacket(ServerboundCustomQueryAnswerPacket packet)
    {
        //Log.Debug("HandleCustomQueryPacket entry");
    }

    //HandleLoginAcknowledgement switches to the Configuration phase after the client confirms login completion
    public void HandleLoginAcknowledgement(ServerboundLoginAcknowledgedPacket packet)
    {
        //Log.Debug("HandleLoginAcknowledgement entry");
        if (_profile is null)
        {
            Log.Warning("LoginAcknowledged received before Hello, disconnecting");
            _connection.Disconnect("login state error");
            return;
        }
        _context.TransitionToConfiguration(_connection, _profile);
        //Log.Debug("HandleLoginAcknowledgement exit");
    }

    public void OnDisconnect(string reason)
    {
        Log.Info($"login phase disconnect reason={reason} profile={_profile?.Name ?? "none"}");
    }
}

//ServerLoginContext, login phase context, implemented by DedicatedServer and wrapping the switch to Configuration logic
public interface ServerLoginContext
{
    //TransitionToConfiguration switches the connection to the Configuration phase and attaches ServerConfigurationPacketListenerImpl
    //profile is the player profile already verified through Hello
    void TransitionToConfiguration(Connection connection, GameProfile profile);
}
