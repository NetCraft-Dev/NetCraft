using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace NetCraft.TPGA.Auth;

//ApiSession 已认证的 wss 会话
//token UUID 系统颁发 socket 对应连接 apiAccountId 关联 api 账户
public sealed class ApiSession
{
    public string Token { get; set; } = "";
    public WebSocket Socket { get; set; } = null!;
    public int ApiAccountId { get; set; }
    public DateTime ConnectedAt { get; set; }
}

//ApiTokenStore wss↔UUID 内存映射
//每个已建立的 wss 对应唯一 UUID 断开即移除
//MVP 内存存储 重启失效符合每 wss 对应唯一 uuid 语义
//双索引 按 token 查会话与按 socket 反查 token 断开清理用
public sealed class ApiTokenStore
{
    private readonly ConcurrentDictionary<string, ApiSession> _byToken = new();
    private readonly ConcurrentDictionary<WebSocket, string> _bySocket = new();

    //Register 注册会话 同 socket 旧会话覆盖
    public void Register(ApiSession session)
    {
        if (_bySocket.TryRemove(session.Socket, out var oldToken))
            _byToken.TryRemove(oldToken, out _);
        _byToken[session.Token] = session;
        _bySocket[session.Socket] = session.Token;
    }

    //Find 按 token 查会话
    public ApiSession? Find(string token)
        => _byToken.TryGetValue(token, out var s) ? s : null;

    //Verify 校验 token 对应当前 ws 连接防止跨连接盗用
    public bool Verify(string token, WebSocket socket)
        => _byToken.TryGetValue(token, out var s) && ReferenceEquals(s.Socket, socket);

    //RemoveBySocket ws 断开时清理
    public void RemoveBySocket(WebSocket socket)
    {
        if (_bySocket.TryRemove(socket, out var token))
            _byToken.TryRemove(token, out _);
    }

    //Count 当前活跃会话数
    public int Count => _byToken.Count;
}
