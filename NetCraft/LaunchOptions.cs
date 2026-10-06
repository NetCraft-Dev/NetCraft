namespace NetCraft;

//LaunchArgEventArgs data for a single unhandled launch argument event
//Token is the raw argument string, such as --game-dir or /path/to/dir
//Index is the position in the args array, for subscribers to locate related arguments
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

//LaunchOptions event-driven launch argument parser
//Arguments the kernel recognizes are consumed directly and not emitted; unhandled ones are broadcast through the UnhandledArgument event
//Business modules such as Game subscribe to the event and parse them into their own config containers
//DeclareKernelFlag/DeclareKernelOption let kernel sub-modules declare the arguments they consume before Initialize
public static class LaunchOptions
{
    private static readonly HashSet<string> _kernelFlags = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _kernelOptions = new(StringComparer.Ordinal);
    private static int _parsed;

    //UnhandledArgument unhandled argument event
    //Subscribers parse on demand and accumulate into their own config containers
    public static event EventHandler<LaunchArgEventArgs>? UnhandledArgument;

    //DeclareKernelFlag declares a boolean flag consumed by the kernel
    //When parsing, on --{name} the kernel swallows it directly without emitting
    public static void DeclareKernelFlag(string name)
    {
        ThrowIfAlreadyParsed();
        _kernelFlags.Add(name);
    }

    //DeclareKernelOption declares a valued argument consumed by the kernel
    //When parsing, on --{name} value or --{name}=value the kernel swallows it directly without emitting
    public static void DeclareKernelOption(string name)
    {
        ThrowIfAlreadyParsed();
        _kernelOptions.Add(name);
    }

    //Parse parses the args array
    //Flag/option arguments the kernel recognizes are consumed; unhandled tokens are emitted one by one via UnhandledArgument
    //Idempotent: multiple calls take effect only once
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

    //Reset resets internal state; for testing only
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
            throw new InvalidOperationException("LaunchOptions already parsed; cannot declare kernel arguments");
    }
}
