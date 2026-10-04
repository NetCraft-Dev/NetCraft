using System.Reflection;
using System.Runtime.Loader;

namespace NetCraft.ModLoader;

//ModAssemblies 模组程序集加载
//统一处理已在 Default 里就复用 同名加载出两份会让类型判等失败
internal static class ModAssemblies
{
    //Rewriter 加载前改写器 引导装配完规则后设置 不设就按原样加载
    //模组之间的互相注入就发生在这里 被注入的模组必须在加载那一刻被换掉
    internal static Func<string, byte[], byte[]>? Rewriter { get; set; }

    //_loading 正在加载中的程序集名 用来挡住规则成环导致的无限递归
    private static readonly HashSet<string> _loading = new(StringComparer.Ordinal);

    //Load 按程序集名与路径加载 已经在 Default 里时直接复用那一份
    //加载前过改写器 命中注入规则的模组在这里被换成改写版
    public static Assembly Load(string assemblyName, string assemblyPath)
    {
        var existing = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == assemblyName);
        if (existing is not null)
            return existing;

        //替换方自己也被注入时会递归回到这里 同名重入说明规则里有环
        lock (_loading)
        {
            if (!_loading.Add(assemblyName))
                throw new InvalidOperationException($"程序集 {assemblyName} 出现循环加载 注入规则成环");
        }

        try
        {
            var bytes = File.ReadAllBytes(assemblyPath);
            if (Rewriter is not null)
                bytes = Rewriter(assemblyName, bytes);
            return AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(bytes));
        }
        finally
        {
            lock (_loading)
            {
                _loading.Remove(assemblyName);
            }
        }
    }
}
