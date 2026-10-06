using System.Net;
using System.Net.Sockets;
using NetCraft.Logging;
using NetCraft.Network.Protocol;

namespace NetCraft.Network;

//ConnectionAcceptor is the server-side TCP listen acceptor
//Replaces vanilla netty ServerBootstrap with a TcpListener and a background thread looping on AcceptTcpClient
//Each client builds a Connection and calls back OnNewConnection, where DedicatedServer installs the initial handshake listener
//Fault isolation: a single failed accept does not exit the loop but logs Log.Error and continues
public sealed class ConnectionAcceptor : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Action<Connection> _onNewConnection;
    private readonly Thread _acceptThread;
    private readonly CancellationTokenSource _cts = new();
    private bool _running;
    private bool _disposed;

    //The ConnectionAcceptor constructor listens on the given address and port
    //addr is the listen address; IPAddress.Any means 0.0.0.0
    //port is the listen port
    //onNewConnection is the new-connection callback, where DedicatedServer installs the initial listener
    public ConnectionAcceptor(IPAddress addr, int port, Action<Connection> onNewConnection)
    {
        _listener = new TcpListener(addr, port);
        _onNewConnection = onNewConnection;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "ConnectionAcceptor" };
    }

    //Start starts the listen thread
    public void Start()
    {
        if (_running) return;
        _listener.Start();
        _running = true;
        _acceptThread.Start();
    }

    //Stop stops listening and waits for the thread to exit
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _acceptThread.Join(2000); } catch { }
    }

    //AcceptLoop is the background thread looping to accept new connections
    //Each TcpClient builds a Connection with a bidirectional NetworkStream and calls back OnNewConnection
    //Fault isolation: a single failure does not exit the loop
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
                //NoDelay disables Nagle to cut small-packet latency, aligns with vanilla TCP_NODELAY
                client.NoDelay = true;
                var stream = client.GetStream();
                //The server inbound direction is Serverbound, since all packets from clients are serverbound
                var conn = new Connection(stream, stream, PacketFlow.Serverbound);
                //Records the client IP for the IP ban check; IPv4-mapped addresses are normalized back to IPv4 text
                conn.RemoteAddress = NormalizeAddress(client);
                //Transport takes the TcpClient, closed on Dispose to make the read loop exit
                conn.SetTransport(client);
                //Installs the initial handshake listener before starting the read loop to avoid the read loop spinning with no listener
                _onNewConnection(conn);
                //The background read loop blocks on Receive and enqueues packets, which PacketProcessor handles from DedicatedServer.Tick
                conn.StartReadLoop();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to create Connection {e.Message}");
                try { client.Dispose(); } catch { }
            }
        }
    }

    //NormalizeAddress gets the client IP text; under dual-stack listening IPv4 clients appear as mapped addresses and are normalized
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
