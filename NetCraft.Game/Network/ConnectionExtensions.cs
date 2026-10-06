using NetCraft.Config;
using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Network.Protocol.Configuration;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Handshake;
using NetCraft.Network.Protocol.Login;
using NetCraft.Network.Protocol.Status;

namespace NetCraft.Game.Network;

//ConnectionExtensions extension methods for connection business packets
//Extracted business packet dependency logic here from Network/Connection.cs
//The kernel Connection keeps only the generic framework handshake/status/login protocols; registration is done by the Game layer
public static class ConnectionExtensions
{
    //SetListenerForServerboundHandshake sets the server initial handshake listener
    //Aligns with vanilla Connection.setListenerForServerboundHandshake
    public static void SetListenerForServerboundHandshake(this Connection connection, PacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("A non-server connection cannot set a handshake listener");
        if (listener.Flow != FlowDirection.Serverbound)
            throw new InvalidOperationException("The handshake listener direction must be Serverbound");
        if (listener.Protocol != ConnectionProtocol.Handshake)
            throw new InvalidOperationException("The handshake listener protocol must be Handshake");
        connection.SetInitialInboundProtocolInternal(listener, HandshakeProtocols.Serverbound);
    }

    //SetListenerForServerboundStatus sets the server status phase listener
    public static void SetListenerForServerboundStatus(this Connection connection, ServerStatusPacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("A non-server connection cannot set a status listener");
        if (listener.Protocol != ConnectionProtocol.Status)
            throw new InvalidOperationException("The status listener protocol must be Status");
        connection.SetupInboundProtocol(StatusProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(StatusProtocols.Clientbound);
    }

    //SetListenerForServerboundLogin sets the server login phase listener
    public static void SetListenerForServerboundLogin(this Connection connection, ServerLoginPacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("A non-server connection cannot set a login listener");
        if (listener.Protocol != ConnectionProtocol.Login)
            throw new InvalidOperationException("The login listener protocol must be Login");
        connection.SetupInboundProtocol(LoginProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(LoginProtocols.Clientbound);
    }

    //SetListenerForServerboundConfiguration sets the server configuration phase listener
    public static void SetListenerForServerboundConfiguration(this Connection connection, ServerConfigurationPacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("A non-server connection cannot set a configuration listener");
        if (listener.Protocol != ConnectionProtocol.Configuration)
            throw new InvalidOperationException("The configuration listener protocol must be Configuration");
        connection.SetupInboundProtocol(ConfigurationProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(ConfigurationProtocols.Clientbound);
    }

    //SetListenerForServerboundGame sets the server play phase listener
    //listener.Protocol explicitly returns Play because the inherited default Protocol is Configuration
    public static void SetListenerForServerboundGame(this Connection connection, ServerGamePacketListener listener)
    {
        if (connection.Receiving != PacketFlow.Serverbound)
            throw new InvalidOperationException("A non-server connection cannot set a play listener");
        if (listener.Protocol != ConnectionProtocol.Play)
            throw new InvalidOperationException("The play listener protocol must be Play");
        connection.SetupInboundProtocol(GameProtocols.Serverbound, listener);
        connection.SetupOutboundProtocol(GameProtocols.Clientbound);
    }

    //InitiateServerboundStatusConnection initiates a status query connection from the client
    //Aligns with vanilla Connection.initiateServerboundStatusConnection
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

    //InitiateServerboundLoginConnection initiates a login connection from the client
    //Aligns with vanilla initiateServerboundPlayConnection; the vanilla name is kept, but this is really Login
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

    //InitiateServerboundConnection generic client initiation flow
    //Aligns with vanilla initiateServerboundConnection
    //After sending ClientIntention, switch the outbound protocol to the target protocol
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
            throw new InvalidOperationException("Inbound and outbound protocols do not match");
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
