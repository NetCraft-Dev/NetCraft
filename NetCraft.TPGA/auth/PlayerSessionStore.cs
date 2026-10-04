using System.Collections.Concurrent;

namespace NetCraft.TPGA.Auth;

//PlayerSession web 玩家会话 与 AdminSession 独立
//sessionId 随机 token 存 cookie userId/username/uuid 关联玩家 LoginAt 审计用
public sealed class PlayerSession
{
    public string SessionId { get; set; } = "";
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public DateTime LoginAt { get; set; }
}

//PlayerSessionStore web 玩家 session 内存存储
//MVP 内存存储 重启失效 与 AdminSessionStore/ApiTokenStore 独立 这套仅服务玩家 web 自服务
public sealed class PlayerSessionStore
{
    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();

    //Create 登录成功后创建 session 返回含 sessionId
    public PlayerSession Create(int userId, string username, string uuid)
    {
        var session = new PlayerSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Username = username,
            Uuid = uuid,
            LoginAt = DateTime.UtcNow
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    //Find 按 sessionId 查 session
    public PlayerSession? Find(string? sessionId)
        => !string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var s) ? s : null;

    //Remove 登出移除 session
    public void Remove(string sessionId) => _sessions.TryRemove(sessionId, out _);
}
