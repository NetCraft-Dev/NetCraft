using System.Collections.Concurrent;

namespace NetCraft.TPGA.Auth;

//PendingOAuth OAuth 回调后未绑定本地账户的暂存身份
//provider/subject 标识 OAuth 身份 username/email 预填补全页 token 关联前端
//15 分钟过期 注册成功或超时后移除
public sealed class PendingOAuth
{
    public string Token { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Username { get; set; } = "";
    public string? Email { get; set; }
    public DateTime Expires { get; set; }
}

//PendingOAuthStore 暂存未绑定的 OAuth 身份 内存存储 重启失效
//OAuth 回调拿到身份后未查到本地绑定则生成 token 存此 前端补全页带 token 建账户
public sealed class PendingOAuthStore
{
    private readonly ConcurrentDictionary<string, PendingOAuth> _pending = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    //Add 暂存 OAuth 身份 15 分钟后自动过期
    public void Add(PendingOAuth p)
    {
        p.Expires = DateTime.UtcNow + Ttl;
        _pending[p.Token] = p;
    }

    //Find 按 token 查 过期返回 null 并移除
    public PendingOAuth? Find(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        if (!_pending.TryGetValue(token, out var p)) return null;
        if (p.Expires < DateTime.UtcNow) { _pending.TryRemove(token, out _); return null; }
        return p;
    }

    //Remove 注册成功或取消后移除
    public void Remove(string token) => _pending.TryRemove(token, out _);
}
