using System.Reflection;

namespace NetCraft.Hook;

//RuntimeInjector 借原生注入层把已加载程序集里的方法在运行时换掉
//与 RewritingLoadContext 的区别是它能改 已经加载并且已经 JIT 过 的代码
public static class RuntimeInjector
{
    //IsAvailable 原生注入层是否已经挂到进程上 没挂时登记请求发不出去
    public static bool IsAvailable => NativeInjector.IsAvailable;

    //Inject 按引擎里的规则改写一个已加载的程序集 返回真正登记出去的方法清单
    //改写由 Cecil 在内存里完成 结果不写盘 直接编成描述交给原生层提交给 CLR
    //mode 挑这一趟走哪一类规则 默认只走运行时注入那一类 加载时改写的规则不该在这里重复落地
    public static IReadOnlyList<string> Inject(Assembly assembly, HookEngine engine,
        PatchMode mode = PatchMode.RuntimeInject)
    {
        var path = assembly.Location;
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("The assembly has no disk location, its original bytes cannot be read", nameof(assembly));

        return Inject(File.ReadAllBytes(path), Path.GetFileName(path), engine, mode);
    }

    //Inject 用调用方给的原始字节改写 moduleName 是原生层认模块用的文件名
    //内核程序集是流加载进来的 没有磁盘位置 这类目标走这个入口
    public static IReadOnlyList<string> Inject(byte[] originalBytes, string moduleName, HookEngine engine,
        PatchMode mode = PatchMode.RuntimeInject)
    {
        using var rewritten = engine.RewriteInMemory(originalBytes, mode);

        var injected = new List<string>();
        //改写命中的宿主由引擎对比前后得出 规则里不一定写得出宿主是谁
        foreach (var method in engine.RewrittenHosts)
        {
            var typeName = method.DeclaringType.FullName;
            var description = WireWriter.Write(method);

            var code = NativeInjector.RequestRewrite(moduleName, typeName, method.Name, description);
            if (code != 0)
                throw new InvalidOperationException(
                    $"Failed to register runtime rewrite, native layer returned {code} for {typeName}::{method.Name}");

            injected.Add($"{typeName}::{method.Name}");
        }

        return injected;
    }
}
