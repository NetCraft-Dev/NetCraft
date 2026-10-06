using System.Net.Sockets;
using System.Text;
using NetCraft.Logging;
using NetCraft.Game.Server;

namespace NetCraft.Game.Server.Rcon.Thread;

//RconClient 单个 RCON 客户端的会话线程对应原版 net.minecraft.server.rcon.thread.RconClient
//先鉴权(SERVERDATA_AUTH) 后每次一条命令(SERVERDATA_EXECCOMMAND) 响应按 4096 字符切分
//一次 TCP 读必须正好含一个整包 包长对不上直接断连 与原版一致
public class RconClient : GenericThread
{
    private const int ServerdataAuth = 3;
    private const int ServerdataExeccommand = 2;
    private const int ServerdataResponseValue = 0;
    private const int ServerdataAuthResponse = 2;
    private const int ServerdataAuthFailure = -1;

    private bool _authed;
    private readonly Socket _client;
    private readonly byte[] _buf = new byte[PktUtils.MaxPacketSize];
    private readonly string _rconPassword;
    private readonly DedicatedServer _serverInterface;

    public RconClient(DedicatedServer serverInterface, string rconPassword, Socket socket)
        : base("RCON Client " + (socket.RemoteEndPoint?.ToString() ?? "unknown"))
    {
        _serverInterface = serverInterface;
        _client = socket;
        try
        {
            _client.ReceiveTimeout = 0;
        }
        catch (Exception)
        {
            Running = false;
        }
        _rconPassword = rconPassword;
    }

    protected override void Run()
    {
        try
        {
            while (Running)
            {
                int read;
                using (var stream = new BufferedStream(new NetworkStream(_client, ownsSocket: false)))
                {
                    read = stream.Read(_buf, 0, PktUtils.MaxPacketSize);
                }
                if (10 > read) return;
                var offset = 0;
                var pktsize = PktUtils.IntFromByteArray(_buf, 0, read);
                if (pktsize != read - 4) return;
                var requestid = PktUtils.IntFromByteArray(_buf, offset += 4, read);
                var cmd = PktUtils.IntFromByteArray(_buf, offset += 4);
                offset += 4;
                switch (cmd)
                {
                    case ServerdataAuth:
                    {
                        var password = PktUtils.StringFromByteArray(_buf, offset, read);
                        if (password.Length != 0 && password == _rconPassword)
                        {
                            _authed = true;
                            Send(requestid, ServerdataAuthResponse, "");
                        }
                        else
                        {
                            _authed = false;
                            SendAuthFailure();
                        }
                        break;
                    }
                    case ServerdataExeccommand:
                    {
                        if (_authed)
                        {
                            var command = PktUtils.StringFromByteArray(_buf, offset, read);
                            try
                            {
                                SendCmdResponse(requestid, _serverInterface.RunRconCommand(command));
                            }
                            catch (Exception e)
                            {
                                SendCmdResponse(requestid, $"Error executing: {command} ({e.Message})");
                            }
                        }
                        else
                        {
                            SendAuthFailure();
                        }
                        break;
                    }
                    default:
                        SendCmdResponse(requestid, $"Unknown request {cmd:x}");
                        break;
                }
            }
        }
        catch (ThreadInterruptedException)
        {
            //stop 打断同步执行的命令等待 正常关停路径
        }
        catch (Exception e) when (e is SocketException or System.IO.IOException or ObjectDisposedException)
        {
            //客户端断开与关服时回包写向已关的 socket 都按连接结束处理 与原版吞 IOException 一致
        }
        catch (Exception e)
        {
            Log.Error($"Exception whilst parsing RCON input {e}");
        }
        finally
        {
            CloseSocket();
            Log.Info($"Thread {ThreadName} shutting down");
            Running = false;
        }
    }

    //Send 回一个包 长度与编号都是小端 尾部两个 0 终止符
    private void Send(int requestid, int cmd, string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        var payload = new byte[bytes.Length + 14];
        WriteIntLe(payload, 0, bytes.Length + 10);
        WriteIntLe(payload, 4, requestid);
        WriteIntLe(payload, 8, cmd);
        Buffer.BlockCopy(bytes, 0, payload, 12, bytes.Length);
        _client.Send(payload);
    }

    private static void WriteIntLe(byte[] target, int offset, int value)
    {
        target[offset] = (byte)value;
        target[offset + 1] = (byte)(value >> 8);
        target[offset + 2] = (byte)(value >> 16);
        target[offset + 3] = (byte)(value >> 24);
    }

    private void SendAuthFailure() => Send(-1, ServerdataAuthResponse, "");

    //SendCmdResponse 超过 4096 字符的回执切多包 对应原版 sendCmdResponse
    private void SendCmdResponse(int requestid, string response)
    {
        var len = response.Length;
        do
        {
            var dataLen = 4096 <= len ? 4096 : len;
            Send(requestid, ServerdataResponseValue, response[..dataLen]);
            response = response[dataLen..];
            len = response.Length;
        } while (len != 0);
    }

    public override void Stop()
    {
        Running = false;
        CloseSocket();
        base.Stop();
    }

    private void CloseSocket()
    {
        try
        {
            _client.Close();
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to close socket {e}");
        }
    }
}
