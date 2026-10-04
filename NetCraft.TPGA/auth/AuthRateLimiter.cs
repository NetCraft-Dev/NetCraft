using System.Collections.Concurrent;

namespace NetCraft.TPGA.Auth;

//AuthRateLimiter 认证限流 防暴力破解
//按 IP 记录失败次数 滑动窗口内超阈值则拒绝 成功后清除
//仅 ForbiddenOperationException 计数 body 格式错误不算
public sealed class AuthRateLimiter
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _failures = new();
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    //RecordFailure 记录失败 超窗口的清除
    public void RecordFailure(string ip)
    {
        var now = DateTime.UtcNow;
        var list = _failures.GetOrAdd(ip, _ => new List<DateTime>());
        lock (list)
        {
            list.Add(now);
            list.RemoveAll(t => now - t > Window);
        }
    }

    //IsLimited 检查 IP 是否被限流 窗口内失败次数超阈值
    public bool IsLimited(string ip)
    {
        if (!_failures.TryGetValue(ip, out var list)) return false;
        var now = DateTime.UtcNow;
        lock (list)
        {
            list.RemoveAll(t => now - t > Window);
            return list.Count >= MaxFailures;
        }
    }

    //Reset 成功后清除 IP 记录
    public void Reset(string ip) => _failures.TryRemove(ip, out _);
}
