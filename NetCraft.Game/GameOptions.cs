using NetCraft;

namespace NetCraft.Game;

//GameOptions Game 模块启动参数容器
//订阅 LaunchOptions.UnhandledArgument 事件累积内核未识别的参数
//解析规则：--flag 加到 Flags，--opt value 或 --opt=value 加到 Options
public sealed class GameOptions
{
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly List<string> _positionals = new();
    private bool _subscribed;

    //Flags 布尔参数集合如 --demo --fullscreen
    public IReadOnlySet<string> Flags => _flags;

    //Options 带值参数字典如 --game-dir /path
    public IReadOnlyDictionary<string, string> Options => _options;

    //Positionals 位置参数无 -- 前缀的裸 token
    public IReadOnlyList<string> Positionals => _positionals;

    //Subscribe 订阅 LaunchOptions.UnhandledArgument 事件
    //必须在 NetCraftKernel.Initialize 调用前订阅才能收到事件
    public void Subscribe()
    {
        if (_subscribed) return;
        LaunchOptions.UnhandledArgument += OnUnhandled;
        _subscribed = true;
    }

    //Unsubscribe 取消订阅通常不需要调用除非测试隔离
    public void Unsubscribe()
    {
        if (!_subscribed) return;
        LaunchOptions.UnhandledArgument -= OnUnhandled;
        _subscribed = false;
    }

    //HasFlag 是否包含指定布尔参数
    public bool HasFlag(string name) => _flags.Contains(name);

    //TryGetOption 查询带值参数
    public bool TryGetOption(string name, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string value)
        => _options.TryGetValue(name, out value);

    //GetOptionOrDefault 查询带值参数不存在返回默认值
    public string GetOptionOrDefault(string name, string defaultValue)
        => _options.TryGetValue(name, out var v) ? v : defaultValue;

    //Clear 清空所有累积参数测试隔离用
    public void Clear()
    {
        _flags.Clear();
        _options.Clear();
        _positionals.Clear();
    }

    //OnUnhandled 事件回调累积未识别参数
    //解析规则：--flag 加 Flags，--opt value 下一个 token 作为值，--opt=value 加 Options
    //裸 token 加 Positionals
    private void OnUnhandled(object? sender, LaunchArgEventArgs e)
    {
        var token = e.Token;
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            //裸 token 可能是上一个 --opt 的值由内部 _pendingOption 处理
            if (_pendingOption is not null)
            {
                _options[_pendingOption] = token;
                _pendingOption = null;
            }
            else
            {
                _positionals.Add(token);
            }
            return;
        }

        var body = token.AsSpan(2);
        int eqIdx = body.IndexOf('=');
        if (eqIdx >= 0)
        {
            var name = body.Slice(0, eqIdx).ToString();
            var value = body.Slice(eqIdx + 1).ToString();
            _options[name] = value;
            _pendingOption = null;
            return;
        }

        var key = body.ToString();
        if (_pendingOption is not null)
        {
            //上一个 --opt 没等到值当前是新的 --flag 上一个降级为 flag
            _flags.Add(_pendingOption);
        }
        _pendingOption = key;
    }

    //FlushPending 解析结束后把仍挂起的 --opt 当 flag 处理
    public void FlushPending()
    {
        if (_pendingOption is not null)
        {
            _flags.Add(_pendingOption);
            _pendingOption = null;
        }
    }

    private string? _pendingOption;
}
