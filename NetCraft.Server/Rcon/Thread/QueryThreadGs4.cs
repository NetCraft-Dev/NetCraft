using System.Net;
using System.Net.Sockets;
using System.Text;
using NetCraft.Logging;
using NetCraft.Game.Server;
using NetCraft.Util.Random;

namespace NetCraft.Game.Server.Rcon.Thread;

//QueryThreadGs4 GS4 查询协议线程对应原版 net.minecraft.server.rcon.thread.QueryThreadGs4
//UDP 监听 handshake(0x09) 发挑战数 status(0x00) 回基础信息或规则表
//挑战数 30 秒清理一次 规则响应缓存 5 秒
public class QueryThreadGs4 : GenericThread
{
    private const string GameType = "SMP";
    private const string GameId = "MINECRAFT";
    private const long ChallengeCheckIntervalMillis = 30000;
    private const long ResponseCacheTimeMillis = 5000;

    private long _lastChallengeCheck;
    private readonly int _port;
    private readonly int _serverPort;
    private readonly int _maxPlayers;
    private readonly string _serverName;
    private readonly string _worldName;
    private Socket? _socket;
    private readonly byte[] _buffer = new byte[PktUtils.MaxPacketSize];
    private string _hostIp;
    private string _serverIp;
    private readonly Dictionary<IPEndPoint, RequestChallenge> _validChallenges = [];
    private readonly NetworkDataOutputStream _rulesResponse;
    private long _lastRulesResponse;
    private readonly DedicatedServer _serverInterface;

    private QueryThreadGs4(DedicatedServer serverInterface, int port)
        : base("Query Listener")
    {
        _serverInterface = serverInterface;
        _port = port;
        _serverIp = serverInterface.Settings.ServerIp;
        _serverPort = serverInterface.Settings.ServerPort;
        _serverName = serverInterface.Settings.Motd;
        _maxPlayers = serverInterface.Settings.MaxPlayers;
        _worldName = serverInterface.Settings.LevelName;
        _lastRulesResponse = 0;
        _hostIp = "0.0.0.0";
        if (_serverIp.Length == 0 || _hostIp == _serverIp)
        {
            _serverIp = "0.0.0.0";
            try
            {
                _hostIp = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? _hostIp;
            }
            catch (Exception)
            {
                Log.Warning("Unable to determine local host IP, please set server-ip in server.properties");
            }
        }
        else
        {
            _hostIp = _serverIp;
        }
        _rulesResponse = new NetworkDataOutputStream(PktUtils.MaxPacketSize);
    }

    //Create 按配置建线程端口没配好返回 null 对应原版 create
    public static QueryThreadGs4? Create(DedicatedServer serverInterface)
    {
        var port = serverInterface.Settings.QueryPort;
        if (port <= 0 || port > 65535)
        {
            Log.Warning($"Invalid query port {port} found in server.properties (queries disabled)");
            return null;
        }
        var result = new QueryThreadGs4(serverInterface, port);
        if (!result.Start()) return null;
        return result;
    }

    private void SendTo(byte[] data, IPEndPoint src)
        => _socket!.SendTo(data, 0, data.Length, SocketFlags.None, src);

    //ProcessPacket 处理一个 UDP 包返回是否有效
    private bool ProcessPacket(byte[] buf, int len, IPEndPoint socketAddress)
    {
        if (3 > len || buf[0] != 0xFE || buf[1] != 0xFD)
        {
            Log.Debug($"Invalid packet [{socketAddress}]");
            return false;
        }
        switch (buf[2])
        {
            case 9:
                SendChallenge(buf, socketAddress);
                Log.Debug($"Challenge [{socketAddress}]");
                return true;
            case 0:
            {
                if (!ValidChallenge(socketAddress, buf, len))
                {
                    Log.Debug($"Invalid challenge [{socketAddress}]");
                    return false;
                }
                if (len == 15)
                {
                    SendTo(BuildRuleResponse(socketAddress), socketAddress);
                    Log.Debug($"Rules [{socketAddress}]");
                    break;
                }
                var dos = new NetworkDataOutputStream(PktUtils.MaxPacketSize);
                dos.Write(0);
                dos.WriteBytes(GetIdentBytes(socketAddress));
                dos.WriteString(_serverName);
                dos.WriteString(GameType);
                dos.WriteString(_worldName);
                dos.WriteString(_serverInterface.PlayerList.Players.Count.ToString());
                dos.WriteString(_maxPlayers.ToString());
                dos.WriteShort((short)_serverPort);
                dos.WriteString(_hostIp);
                SendTo(dos.ToByteArray(), socketAddress);
                Log.Debug($"Status [{socketAddress}]");
                break;
            }
        }
        return true;
    }

    //BuildRuleResponse 规则表响应 5 秒内复用缓存只改头部挑战号 对应原版 buildRuleResponse
    private byte[] BuildRuleResponse(IPEndPoint socketAddress)
    {
        var now = Environment.TickCount64;
        if (now < _lastRulesResponse + ResponseCacheTimeMillis)
        {
            var data = _rulesResponse.ToByteArray();
            var ident = GetIdentBytes(socketAddress);
            data[1] = ident[0];
            data[2] = ident[1];
            data[3] = ident[2];
            data[4] = ident[3];
            return data;
        }
        _lastRulesResponse = now;
        _rulesResponse.Reset();
        _rulesResponse.Write(0);
        _rulesResponse.WriteBytes(GetIdentBytes(socketAddress));
        _rulesResponse.WriteString("splitnum");
        _rulesResponse.Write(128);
        _rulesResponse.Write(0);
        _rulesResponse.WriteString("hostname");
        _rulesResponse.WriteString(_serverName);
        _rulesResponse.WriteString("gametype");
        _rulesResponse.WriteString(GameType);
        _rulesResponse.WriteString("game_id");
        _rulesResponse.WriteString(GameId);
        _rulesResponse.WriteString("version");
        _rulesResponse.WriteString(_serverInterface.ServerStatus.Version!.Name);
        _rulesResponse.WriteString("plugins");
        _rulesResponse.WriteString("");
        _rulesResponse.WriteString("map");
        _rulesResponse.WriteString(_worldName);
        _rulesResponse.WriteString("numplayers");
        _rulesResponse.WriteString(_serverInterface.PlayerList.Players.Count.ToString());
        _rulesResponse.WriteString("maxplayers");
        _rulesResponse.WriteString(_maxPlayers.ToString());
        _rulesResponse.WriteString("hostport");
        _rulesResponse.WriteString(_serverPort.ToString());
        _rulesResponse.WriteString("hostip");
        _rulesResponse.WriteString(_hostIp);
        _rulesResponse.Write(0);
        _rulesResponse.Write(1);
        _rulesResponse.WriteString("player_");
        _rulesResponse.Write(0);
        foreach (var player in _serverInterface.PlayerList.Players)
            _rulesResponse.WriteString(player.Profile.Name);
        _rulesResponse.Write(0);
        return _rulesResponse.ToByteArray();
    }

    private byte[] GetIdentBytes(IPEndPoint src)
        => _validChallenges.TryGetValue(src, out var challenge)
            ? challenge.IdentBytes
            : throw new InvalidOperationException($"No challenge for {src}");

    private bool ValidChallenge(IPEndPoint socketAddress, byte[] data, int length)
    {
        if (!_validChallenges.ContainsKey(socketAddress)) return false;
        return _validChallenges[socketAddress].Challenge
            == PktUtils.IntFromNetworkByteArray(data, 7, length);
    }

    private void SendChallenge(byte[] buf, IPEndPoint socketAddress)
    {
        var challenge = new RequestChallenge(buf);
        _validChallenges[socketAddress] = challenge;
        SendTo(challenge.ChallengeBytes, socketAddress);
    }

    //PruneChallenges 过期挑战清理 30 秒一次
    private void PruneChallenges()
    {
        if (!Running) return;
        var now = Environment.TickCount64;
        if (now < _lastChallengeCheck + ChallengeCheckIntervalMillis) return;
        _lastChallengeCheck = now;
        foreach (var stale in _validChallenges.Values.Where(challenge => challenge.IsBefore(now)).ToList())
            _validChallenges.Remove(FirstKeyOf(stale));
    }

    //FirstKeyOf 按值反查键 清理时用
    private IPEndPoint FirstKeyOf(RequestChallenge challenge)
        => _validChallenges.First(pair => ReferenceEquals(pair.Value, challenge)).Key;

    protected override void Run()
    {
        Log.Info($"Query running on {_serverIp}:{_port}");
        _lastChallengeCheck = Environment.TickCount64;
        var remoteEndPoint = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
        try
        {
            while (Running)
            {
                try
                {
                    int len;
                    try
                    {
                        len = _socket!.ReceiveFrom(_buffer, _buffer.Length, SocketFlags.None, ref remoteEndPoint);
                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
                    {
                        PruneChallenges();
                        continue;
                    }
                    catch (SocketException e) when (e.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused)
                    {
                        //java 的 PortUnreachableException 在 Windows 上映射 WSAECONNRESET 在 POSIX 上映射 ECONNREFUSED
                        continue;
                    }
                    PruneChallenges();
                    ProcessPacket(_buffer, len, (IPEndPoint)remoteEndPoint);
                }
                catch (Exception e)
                {
                    RecoverSocketError(e);
                }
            }
        }
        finally
        {
            Log.Debug($"closeSocket: {_serverIp}:{_port}");
            _socket?.Close();
        }
    }

    public override bool Start()
    {
        if (Running) return true;
        if (!InitSocket()) return false;
        return base.Start();
    }

    //RecoverSocketError 崩掉的 UDP socket 重建一次 重建失败就停
    private void RecoverSocketError(Exception e)
    {
        if (!Running) return;
        Log.Warning($"Unexpected exception {e}");
        if (!InitSocket())
        {
            Log.Error("Failed to recover from exception, shutting down!");
            Running = false;
        }
    }

    private bool InitSocket()
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.TryParse(_serverIp, out var ip) ? ip : IPAddress.Any, _port));
            _socket.ReceiveTimeout = 500;
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"Unable to initialise query system on {_serverIp}:{_port} {e}");
            return false;
        }
    }

    //RequestChallenge 一次握手发出的挑战对应原版 RequestChallenge
    //ident 是请求里带的四字节 挑战数是随机数 编码为 tab+ident+challenge+0
    private sealed class RequestChallenge
    {
        private readonly long _time = Environment.TickCount64;

        public RequestChallenge(byte[] buf)
        {
            IdentBytes = [buf[3], buf[4], buf[5], buf[6]];
            Ident = Encoding.UTF8.GetString(IdentBytes);
            //NC 没有 java 的 ThreadLocal 随机源 挑战数各自现造一个等价
            Challenge = RandomSource.Create().NextInt(0x1000000);
            ChallengeBytes = Encoding.UTF8.GetBytes($"\t{Ident}{Challenge}\0");
        }

        public bool IsBefore(long time) => _time < time;

        public int Challenge { get; }

        public byte[] IdentBytes { get; }

        public string Ident { get; }

        public byte[] ChallengeBytes { get; }
    }
}
