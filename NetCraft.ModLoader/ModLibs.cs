using System.Reflection;
using System.Runtime.Loader;
using NetCraft.Logging;

namespace NetCraft.ModLoader;

//ModLibs 模组内嵌依赖的解析 对应原版 Fabric 的 Jar-in-Jar
//模组把依赖库当内嵌资源打进自己的 dll 解析不到程序集时从这里取
//条目就是程序集里所有 .dll 资源 构建期把 dotnet add package 加来的库直接嵌进来
//内核子库的解析归 EmbeddedAssemblyLoader 管 这里只管模组自带的那部分
//注册必须早于 ModHooks.Build —— 装配要解析替换类 那一刻依赖就得取得到
public static class ModLibs
{
    //_entries 各模组内嵌的依赖 一条一个资源名
    private static readonly List<(string ModPath, string ResourceName)> _entries = new();
    private static int _registered;

    //Register 收集内嵌依赖并挂上解析回调
    //条目来源是程序集里嵌着的全部 .dll 资源 模板项目的构建会把依赖直接嵌进来
    //只收环境匹配的模组 端不匹配的模组后面也不会被加载
    //先收条目再判要不要挂回调 顺序反过来的话第一次进来没有条目就再也挂不上了
    public static void Register(IEnumerable<ScannedMod> mods)
    {
        foreach (var mod in mods)
        {
            foreach (var resource in mod.EmbeddedResources)
            {
                if (resource.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    _entries.Add((mod.AssemblyPath, resource));
            }
        }

        if (_entries.Count == 0)
            return;

        //回调只挂一次 条目可以续着补
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        AssemblyLoadContext.Default.Resolving += OnResolving;
        Log.Info($"Embedded mod dependencies registered: {_entries.Count}");
    }

    //OnResolving 内核那边没取到时从模组内嵌资源里取
    private static Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        var bytes = TryRead(name.Name);
        if (bytes is null)
            return null;

        Log.Debug($"Resolved assembly {name.Name} from embedded mod resources");
        return context.LoadFromStream(new MemoryStream(bytes));
    }

    //TryRead 按程序集名在已登记的条目里找
    public static byte[]? TryRead(string? assemblyName) => TryReadFrom(_entries, assemblyName);

    //TryReadFrom 按程序集名在一组条目里找 找不到返回 null
    //抽出来是为了能直接喂一组条目做验证 不必走注册
    public static byte[]? TryReadFrom(IEnumerable<(string ModPath, string ResourceName)> entries, string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return null;

        foreach (var (modPath, resourceName) in entries)
        {
            if (!MatchesLibName(assemblyName, resourceName))
                continue;

            var bytes = ModScanner.ReadEmbeddedResource(modPath, resourceName);
            if (bytes is not null)
                return bytes;
        }
        return null;
    }

    //MatchesLibName 资源名与程序集名是否指同一个库
    //资源名一般是 MyLib.dll
    //内嵌资源用默认名时会带上项目命名空间前缀 所以后缀命中也要认
    public static bool MatchesLibName(string assemblyName, string resourceName)
    {
        var trimmed = resourceName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? resourceName[..^4]
            : resourceName;

        return trimmed.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("." + assemblyName, StringComparison.OrdinalIgnoreCase);
    }
}
