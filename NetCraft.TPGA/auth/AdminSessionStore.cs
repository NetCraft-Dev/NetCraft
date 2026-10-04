using System.Collections.Concurrent;

namespace NetCraft.TPGA.Auth;

//AdminSession web 后台会话
//sessionId 随机 token 存 cookie adminId/username 关联管理员 mustChangePassword 首登改密标志
public sealed class AdminSession
{
    public string SessionId { get; set; } = "";
    public int AdminId { get; set; }
    public string Username { get; set; } = "";
    public DateTime LoginAt { get; set; }
    public bool MustChangePassword { get; set; }
}

//AdminSessionStore web 后台 session 内存存储
//MVP 内存存储 重启失效 与 wss 的 ApiTokenStore 独立 这套仅服务 web 后台
public sealed class AdminSessionStore
{
    private readonly ConcurrentDictionary<string, AdminSession> _sessions = new();

    //Create 登录成功后创建 session 返回含 sessionId
    public AdminSession Create(int adminId, string username, bool mustChange)
    {
        var session = new AdminSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            AdminId = adminId,
            Username = username,
            LoginAt = DateTime.UtcNow,
            MustChangePassword = mustChange
        };
        _sessions[session.SessionId] = session;
        return session;
    }

    //Find 按 sessionId 查 session
    public AdminSession? Find(string? sessionId)
        => !string.IsNullOrEmpty(sessionId) && _sessions.TryGetValue(sessionId, out var s) ? s : null;

    //UpdateMustChange 更新改密标志 改密成功后置 false
    public void UpdateMustChange(string sessionId, bool mustChange)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
            s.MustChangePassword = mustChange;
    }

    //Remove 登出移除 session
    public void Remove(string sessionId) => _sessions.TryRemove(sessionId, out _);
}
