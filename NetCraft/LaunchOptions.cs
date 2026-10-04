namespace NetCraft;

//LaunchArgEventArgs 单个未识别启动参数事件数据
//Token 是原始参数字符串如 --game-dir 或 /path/to/dir
//Index 是在 args 数组中的位置供订阅者定位关联参数
public sealed class LaunchArgEventArgs : EventArgs
{
    public string Token { get; }
    public int Index { get; }

    public LaunchArgEventArgs(string token, int index)
    {
        Token = token;
        Index = index;
    }
}

//LaunchOptions 启动参数事件驱动解析器
//内核识别的参数直接消化不发出 未识别的通过 UnhandledArgument 事件广播
//Game 等业务模块订阅事件自行解析累积到自己的配置容器
//DeclareKernelFlag/DeclareKernelOption 内核子模块在 Initialize 前声明自己消费的参数
public static class LaunchOptions
{
    private static readonly HashSet<string> _kernelFlags = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _kernelOptions = new(StringComparer.Ordinal);
    private static int _parsed;

    //UnhandledArgument 未识别参数事件
    //订阅者按需解析累积到自己的配置容器
    public static event EventHandler<LaunchArgEventArgs>? UnhandledArgument;

    //DeclareKernelFlag 声明内核消费的布尔 flag
    //解析时遇到 --{name} 内核直接吞掉不发出
    public static void DeclareKernelFlag(string name)
    {
        ThrowIfAlreadyParsed();
        _kernelFlags.Add(name);
    }

    //DeclareKernelOption 声明内核消费的带值参数
    //解析时遇到 --{name} value 或 --{name}=value 内核直接吞掉不发出
    public static void DeclareKernelOption(string name)
    {
        ThrowIfAlreadyParsed();
        _kernelOptions.Add(name);
    }

    //Parse 解析 args 数组
    //内核识别的 flag/option 消费 未识别的 token 通过 UnhandledArgument 逐个发出
    //幂等：多次调用只生效一次
    public static void Parse(string[] args)
    {
        if (Interlocked.Exchange(ref _parsed, 1) == 1) return;

        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                RaiseUnhandled(token, i);
                continue;
            }

            var body = token.AsSpan(2);
            int eqIdx = body.IndexOf('=');
            string name;
            bool hasEq = eqIdx >= 0;
            if (hasEq)
            {
                name = body.Slice(0, eqIdx).ToString();
            }
            else
            {
                name = body.ToString();
            }

            if (_kernelOptions.Contains(name))
            {
                if (!hasEq && i + 1 < args.Length)
                {
                    i++;
                }
                continue;
            }

            if (!hasEq && _kernelFlags.Contains(name))
            {
                continue;
            }

            RaiseUnhandled(token, i);
        }
    }

    //Reset 重置内部状态仅测试用
    public static void Reset()
    {
        _kernelFlags.Clear();
        _kernelOptions.Clear();
        _parsed = 0;
        UnhandledArgument = null;
    }

    private static void RaiseUnhandled(string token, int index)
    {
        UnhandledArgument?.Invoke(null, new LaunchArgEventArgs(token, index));
    }

    private static void ThrowIfAlreadyParsed()
    {
        if (_parsed != 0)
            throw new InvalidOperationException("LaunchOptions 已解析不能再声明内核参数");
    }
}
