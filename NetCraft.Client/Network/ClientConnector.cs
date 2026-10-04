using System.Net.Sockets;
using NetCraft.Game.Client.Level;
using NetCraft.Network.Protocol.Configuration;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Login;
using NetCraft.Logging;
using NetCraft.Network;

namespace NetCraft.Game.Network;

//ClientConnector 客户端 TCP 连接器对应原版 Connection.connectToServer
//后台线程建立 TcpClient 连接成功后构造 PacketFlow.Clientbound 的 Connection 启动读循环
//走 InitiateServerboundLoginConnection 发 Intention(Hello/Ack/FinishConfiguration 由监听器推进)
//连接结果通过 onConnected/onFailed 回调通知调用方回调在读线程或连接线程触发注意线程安全
public static class ClientConnector
{
    //Connect 发起连接 host 服务器地址 port 端口 playerName 客户端玩家名
    //player/onJoinWorld/level 透传给 ClientGamePacketListenerImpl
    //onConnected 连接建立回调传出 Connection 供 MinecraftClient 持有 tick
    //onFailed 连接失败回调带异常信息
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

    //ConnectCore 连接主流程构造监听器链 Login->Configuration->Game 后发起登录
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
            //Intention 后发 LoginStart 对齐原版 initiateServerboundPlayConnection 流程
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
