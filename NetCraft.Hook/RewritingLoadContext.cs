using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft.Hook;

//RewritingLoadContext 让程序集一进内存就被改写 IL
//这是"JIT 之前"唯一可靠的时机 程序集一旦加载并编译完 再改就没意义了
//被改写的程序集因此永远跑在插桩后的代码上 JIT 是否内联都不影响观测
public sealed class RewritingLoadContext : AssemblyLoadContext
{
    private readonly string _directory;
    private readonly string[] _prefixes;
    private readonly HashSet<string> _excluded;
    private readonly HookEngine _engine;

    //RewrittenAssemblies 实际被改写过的程序集名 按加载顺序
    public List<string> RewrittenAssemblies { get; } = new();

    //LastError 最近一次改写失败的原因 供调用方判定是否静默退回了原程序集
    public string? LastError { get; private set; }

    //excluded 列出的程序集一律不做改写 也必须排除探针类所在的程序集
    //否则子上下文会连探针类一起复制一份 上报会写进另一份静态状态里
    public RewritingLoadContext(string name, string directory, HookEngine engine, string[] excluded, params string[] prefixes)
        : base(name, isCollectible: false)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _excluded = new HashSet<string>(excluded, StringComparer.Ordinal);
        _prefixes = prefixes.Length > 0 ? prefixes : ["NetCraft"];
    }

    //Load 命中前缀的程序集先改写再加载 其余交给默认上下文
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name is null || _excluded.Contains(name) || !Matches(name))
            return null;

        var path = Path.Combine(_directory, name + ".dll");
        if (!File.Exists(path))
            return null;

        byte[] original;
        try
        {
            original = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            LastError = $"{name} read failed: {ex.Message}";
            return null;
        }

        byte[] rewritten;
        try
        {
            rewritten = _engine.Rewrite(original);
        }
        catch (Exception ex)
        {
            LastError = $"{name} rewrite failed: {ex.Message}";
            return LoadFromAssemblyPath(path);
        }

        RewrittenAssemblies.Add(name);
        return LoadFromStream(new MemoryStream(rewritten));
    }

    private bool Matches(string name)
    {
        foreach (var prefix in _prefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
