using System.Net;
using System.Net.Sockets;
using NetCraft.Logging;
using NetCraft.Game.Server;

namespace NetCraft.Game.Server.Rcon.Thread;

//RconThread RCON 监听线程对应原版 net.minecraft.server.rcon.thread.RconThread
//收下 TCP 连接后每个客户端一条 RconClient 线程 协议见 RconClient
public class RconThread : GenericThread
{
    private readonly Socket _socket;
    private readonly string _rconPassword;
    private readonly List<RconClient> _clients = [];
    private readonly DedicatedServer _serverInterface;

    private RconThread(DedicatedServer serverInterface, Socket socket, string rconPassword)
        : base("RCON Listener")
    {
        _serverInterface = serverInterface;
        _socket = socket;
        _rconPassword = rconPassword;
    }

    //Create 按配置建监听端口与密码没配好时返回 null 表示禁用 对应原版 create
    public static RconThread? Create(DedicatedServer serverInterface)
    {
        var settings = serverInterface.Settings;
        var serverIp = settings.ServerIp;
        if (serverIp.Length == 0) serverIp = "0.0.0.0";
        var port = settings.RconPort;
        if (port <= 0 || port > 65535)
        {
            Log.Warning($"Invalid rcon port {port} found in server.properties, rcon disabled!");
            return null;
        }
        var password = settings.RconPassword;
        if (password.Length == 0)
        {
            Log.Warning("No rcon password set in server.properties, rcon disabled!");
            return null;
        }
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(ResolveAddress(serverIp), port));
            socket.Listen(0);
            var result = new RconThread(serverInterface, socket, password);
            if (!result.Start()) return null;
            Log.Info($"RCON running on {serverIp}:{port}");
            return result;
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to initialise RCON on {serverIp}:{port} {e}");
            return null;
        }
    }

    //ResolveAddress 解析绑定地址 支持 IP 字面量与主机名
    private static IPAddress ResolveAddress(string serverIp)
        => IPAddress.TryParse(serverIp, out var parsed)
            ? parsed
            : Dns.GetHostAddresses(serverIp).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
              ?? IPAddress.Any;

    //ClearClients 回收已退出的客户端线程
    private void ClearClients() => _clients.RemoveAll(client => !client.IsRunning());

    protected override void Run()
    {
        try
        {
            while (Running)
            {
                try
                {
                    //五百毫秒等不到连接就回头清一次客户端列表 对应原版 500ms 的 accept 超时
                    if (!_socket.Poll(500_000, SelectMode.SelectRead))
                    {
                        ClearClients();
                        continue;
                    }
                    var client = _socket.Accept();
                    var rconClient = new RconClient(_serverInterface, _rconPassword, client);
                    rconClient.Start();
                    _clients.Add(rconClient);
                    ClearClients();
                }
                catch (SocketException)
                {
                    ClearClients();
                }
                catch (Exception e)
                {
                    if (Running) Log.Info($"IO exception: {e}");
                }
            }
        }
        finally
        {
            CloseSocket(_socket);
        }
    }

    public override void Stop()
    {
        Running = false;
        CloseSocket(_socket);
        base.Stop();
        foreach (var rconClient in _clients)
            if (rconClient.IsRunning()) rconClient.Stop();
        _clients.Clear();
    }

    private static void CloseSocket(Socket socket)
    {
        try
        {
            socket.Close();
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to close socket {e}");
        }
    }
}
