using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Client.Network.Protocol.Login;

//ClientLoginPacketListenerImpl client login listener implementation
//Simplified version of vanilla ClientLoginPacketListenerImpl, skipping encryption
//HandleLoginFinished sends LoginAcknowledged, switches to Configuration, and hands off to configListener to advance
public sealed class ClientLoginPacketListenerImpl : ClientLoginPacketListener
{
    private readonly Connection _connection;
    private readonly ClientConfigurationPacketListenerImpl _configurationListener;

    public ClientLoginPacketListenerImpl(Connection connection, ClientConfigurationPacketListenerImpl configurationListener)
    {
        _connection = connection;
        _configurationListener = configurationListener;
    }

    //HandleLoginFinished on login completion sends LoginAcknowledged and switches to the Configuration protocol
    //S4 aligns with vanilla flow: wait for the server's SelectKnownPacks/FinishConfiguration, and configListener advances to Play on receipt
    public void HandleLoginFinished(ClientboundLoginFinishedPacket packet)
    {
        Log.Info($"Login finished profile={packet.GameProfile.Name}, switching to Configuration phase");
        _connection.Send(ServerboundLoginAcknowledgedPacket.Instance);
        _connection.SetupInboundProtocol(ConfigurationProtocols.Clientbound, _configurationListener);
        _connection.SetupOutboundProtocol(ConfigurationProtocols.Serverbound);
    }

    //HandleHello server hello; not called since there is no encryption flow
    public void HandleHello(ClientboundHelloPacket packet) { }

    //HandleDisconnect rejected during the login phase
    public void HandleDisconnect(ClientboundLoginDisconnectPacket packet)
        => Log.Warning($"Login rejected {packet.Reason}");

    //HandleCompression compression negotiation; the simplified version does not enable compression, empty implementation
    public void HandleCompression(ClientboundLoginCompressionPacket packet) { }

    //HandleCustomQuery custom query, empty implementation
    public void HandleCustomQuery(ClientboundCustomQueryPacket packet) { }

    public void OnDisconnect(string reason)
        => Log.Info($"login phase disconnect reason={reason}");
}
