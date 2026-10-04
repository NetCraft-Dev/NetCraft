using System.Net;
using System.Net.Sockets;
using NetCraft.Logging;
using NetCraft.Network.Protocol;

namespace NetCraft.Network;

//ConnectionAcceptor 服务端 TCP 监听接受器
//替代原版 netty ServerBootstrap 用 TcpListener 后台线程循环 AcceptTcpClient
//每个客户端构造 Connection 回调 OnNewConnection 由 DedicatedServer 装初始握手监听器
//异常隔离单个 accept 失败不退出循环记 Log.Error 后继续
public sealed class ConnectionAcceptor : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Action<Connection> _onNewConnection;
    private readonly Thread _acceptThread;
    private readonly CancellationTokenSource _cts = new();
    private bool _running;
    private bool _disposed;

    //ConnectionAcceptor 构造监听指定地址端口
    //addr 监听地址 IPAddress.Any 表示 0.0.0.0
    //port 监听端口
    //onNewConnection 新连接回调由 DedicatedServer 装初始监听器
    public ConnectionAcceptor(IPAddress addr, int port, Action<Connection> onNewConnection)
    {
        _listener = new TcpListener(addr, port);
        _onNewConnection = onNewConnection;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "ConnectionAcceptor" };
    }

    //Start 启动监听线程
    public void Start()
    {
        if (_running) return;
        _listener.Start();
        _running = true;
        _acceptThread.Start();
    }

    //Stop 停止监听并等待线程退出
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _acceptThread.Join(2000); } catch { }
    }

    //AcceptLoop 后台线程循环接受新连接
    //每个 TcpClient 构造 Connection 用 NetworkStream 双向流回调 OnNewConnection
    //异常隔离单次失败不退出循环
    private void AcceptLoop()
    {
        while (_running && !_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch (SocketException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                Log.Error($"AcceptTcpClient failed {e.Message}");
                continue;
            }
            try
            {
                //NoDelay 禁用 Nagle 减少小包延迟对齐原版 TCP_NODELAY
                client.NoDelay = true;
                var stream = client.GetStream();
                //服务端 inbound 是 Serverbound 方向客户端发来的包都是 serverbound
                var conn = new Connection(stream, stream, PacketFlow.Serverbound);
                //记录客户端 IP 供 IP 封禁检查 IPv4 映射地址统一还原成 IPv4 文本
                conn.RemoteAddress = NormalizeAddress(client);
                //Transport 传入 TcpClient 供 Dispose 时关闭触发读循环退出
                conn.SetTransport(client);
                //先装初始握手监听器再启动读循环避免读循环无监听器空转
                _onNewConnection(conn);
                //后台读循环阻塞 Receive 包入队 PacketProcessor 由 DedicatedServer.Tick 处理
                conn.StartReadLoop();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to create Connection {e.Message}");
                try { client.Dispose(); } catch { }
            }
        }
    }

    //NormalizeAddress 取客户端 IP 文本 双栈监听下 IPv4 客户端是映射地址统一还原
    private static string? NormalizeAddress(TcpClient client)
    {
        if (client.Client.RemoteEndPoint is not IPEndPoint endpoint) return null;
        var address = endpoint.Address;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts.Dispose();
    }
}
