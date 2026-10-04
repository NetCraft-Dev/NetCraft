using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft;

//内嵌程序集加载器（.NET 独家技术）。
//通过 AssemblyLoadContext.Resolving 事件，在子库未被运行时找到时，
//从主库内嵌资源（NetCraft.Embedded.*.dll）加载字节流。
//对应 [C#内核重写计划.md] 第四节"通过内嵌资源加载子库"。
public static class EmbeddedAssemblyLoader
{
    //主库程序集（包含内嵌资源）。
    private static readonly Assembly MainAssembly = typeof(EmbeddedAssemblyLoader).Assembly;

    //内嵌资源的命名前缀，与 csproj 中 LogicalName 一致。
    private const string ResourcePrefix = "NetCraft.Embedded.";

    //KernelDirectoryName 内核程序集子目录名。
    //内核上层程序集 Game/Server/Client/Gpu 反过来引用主库，无法内嵌进主库，
    //约定由各可执行项目构建后统一挪进输出目录下的这个子目录，运行时按需解析。
    public const string KernelDirectoryName = "kernel";

    //是否已初始化。
    private static int _initialized;

    //程序集字节改写器：模组加载器用它做注入改写，未设置时按原样加载。
    private static Func<string, byte[], byte[]>? _rewriter;

    //设置字节改写器。入参是程序集名与原始字节，返回改写后的字节。
    //必须在目标程序集首次解析之前设置，程序集是按需解析的，晚了就赶不上。
    //只作用于本加载器经手的内嵌资源与 kernel 目录，主库自身走常规解析不经过这里，所以常规模组改不了主库。
    public static void SetRewriter(Func<string, byte[], byte[]>? rewriter)
    {
        _rewriter = rewriter;
    }

    //注册内嵌资源解析回调。应在程序启动最早期调用一次。
    //幂等：多次调用只生效一次。
    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        var context = AssemblyLoadContext.Default;
        context.Resolving += OnResolvingAssembly;
    }

    //解析失败时回调：从内嵌资源或 kernel 目录取字节加载。
    //内核程序集已从输出根目录挪走，deps.json 里虽有登记但按路径找不到文件，
    //默认解析失败后落到这里，字节才第一次经过改写器，模组注入就发生在这一刻。
    private static Assembly? OnResolvingAssembly(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
        {
            return null;
        }

        var bytes = ReadAssemblyBytes(name.Name);
        if (bytes is null)
        {
            return null;
        }

        if (_rewriter is not null)
        {
            bytes = _rewriter(name.Name, bytes);
        }

        using var rewritten = new MemoryStream(bytes);
        return context.LoadFromStream(rewritten);
    }

    //ReadAssemblyBytes 按程序集名取原始字节：内嵌资源优先，kernel 目录次之，运行目录兜底。
    //取不到返回 null。预载改写版与解析回调都走这里，保证两条路拿到的是同一份来源。
    public static byte[]? ReadAssemblyBytes(string assemblyName)
    {
        var embedded = ReadEmbeddedAssembly(assemblyName);
        if (embedded is not null)
        {
            return embedded;
        }

        var kernelPath = Path.Combine(AppPaths.BaseDirectory, KernelDirectoryName, assemblyName + ".dll");
        if (File.Exists(kernelPath))
        {
            return File.ReadAllBytes(kernelPath);
        }

        //运行目录兜底：测试这类没走内核分发流程的输出目录里，dll 还躺在根目录
        var directPath = Path.Combine(AppPaths.BaseDirectory, assemblyName + ".dll");
        return File.Exists(directPath) ? File.ReadAllBytes(directPath) : null;
    }

    //读取仅内嵌资源里那份字节，没有则返回 null。
    //要取内核程序集（内嵌或 kernel 目录）的字节请用 ReadAssemblyBytes。
    public static byte[]? ReadEmbeddedAssembly(string assemblyName)
    {
        using var stream = MainAssembly.GetManifestResourceStream(ResourcePrefix + assemblyName + ".dll");
        if (stream is null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    //列出所有可加载的内嵌子库（仅用于诊断/调试）。
    public static IReadOnlyList<string> ListEmbeddedAssemblies()
    {
        var result = new List<string>();
        foreach (var name in MainAssembly.GetManifestResourceNames())
        {
            if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".dll", StringComparison.Ordinal))
            {
                var assemblyName = name.Substring(ResourcePrefix.Length, name.Length - ResourcePrefix.Length - ".dll".Length);
                result.Add(assemblyName);
            }
        }
        return result;
    }
}

