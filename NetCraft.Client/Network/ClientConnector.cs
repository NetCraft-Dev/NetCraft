using System.Net.Sockets;
using NetCraft.Game.Client.Level;
using NetCraft.Network.Protocol.Configuration;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Login;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Game.Network;

//ClientConnector client TCP connector, maps to vanilla Connection.connectToServer
//A background thread establishes the TcpClient; on success it builds a Connection with PacketFlow.Clientbound and starts the read loop
//Goes through InitiateServerboundLoginConnection to send Intention (Hello/Ack/FinishConfiguration are advanced by the listener)
//Connection result is reported via the onConnected/onFailed callbacks; callbacks fire on the read thread or connect thread, so mind thread safety
public static class ClientConnector
{
    //Connect starts a connection: host server address, port, playerName client player name
    //player/onJoinWorld/level are passed through to ClientGamePacketListenerImpl
    //onConnected connection-established callback; emits the Connection for MinecraftClient to hold and tick
    //onFailed connection-failure callback carrying the exception message
    public static void Connect(string host, int port,
        ClientLevel? level, World.Entity.Player player, System.Action? onJoinWorld,
        System.Action<Connection> onConnected, System.Action<string> onFailed,
        string playerName = "NetCraftPlayer")
    {
        var thread = new Thread(() => ConnectCore(host, port, level, player, playerName, onJoinWorld, onConnected, onFailed))
        {
            IsBackground = true,
            Name = "ClientConnector"
        };
        thread.Start();
    }

    //ConnectCore main connect flow: builds the listener chain Login->Configuration->Game, then starts login
    private static void ConnectCore(string host, int port,
        ClientLevel? level, World.Entity.Player player, string playerName,
        System.Action? onJoinWorld, System.Action<Connection> onConnected, System.Action<string> onFailed)
    {
        Connection? connection = null;
        try
        {
            var tcp = new TcpClient();
            tcp.Connect(host, port);
            var stream = tcp.GetStream();
            connection = new Connection(stream, stream, PacketFlow.Clientbound);
            connection.SetTransport(tcp);
            var gameListener = new ClientGamePacketListenerImpl(connection, level, player, onJoinWorld);
            var configListener = new ClientConfigurationPacketListenerImpl(connection, gameListener);
            var loginListener = new ClientLoginPacketListenerImpl(connection, configListener);
            connection.InitiateServerboundLoginConnection(host, port, loginListener);
            //After Intention, send LoginStart, aligning with vanilla initiateServerboundPlayConnection flow
            connection.Send(new ServerboundHelloPacket(playerName, Guid.NewGuid()));
            connection.StartReadLoop();
            Log.Info($"Connected to server {host}:{port}");
            onConnected(connection);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to connect to server {host}:{port} {e.Message}");
            connection?.Dispose();
            onFailed(e.Message);
        }
    }
}
