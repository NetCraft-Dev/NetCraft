using System.Runtime.CompilerServices;
using NetCraft;
using NetCraft.Game;
using NetCraft.ModLoader;

namespace NetCraft.ClientExe;

//Program 客户端独立启动入口
//只做进程入口与模组引导 真正的客户端实现留在 NetCraft.Client 类库里
public static class Program
{
    //Main 进程入口
    //第一件事注册内核程序集解析回调：内核程序集已挪出 deps.json，
    //运行时靠回调从 kernel 目录与主库内嵌资源取字节 必须早于任何内核类型被解析。
    //JIT 会解析方法体里出现的全部类型 所以下面两层都禁止内联，
    //否则 ClientMain 会被提前解析 直接绕过改写把未注入的 NetCraft.Client 拉起来。
    public static int Main(string[] args)
    {
        EmbeddedAssemblyLoader.Initialize();
        BootMods();
        return Launch(args);
    }

    //BootMods 模组引导 这一层只碰加载器 不碰客户端类型
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void BootMods() => ModBootstrap.Run(ModEnvironment.Client);

    //Launch 客户端启动流程 到这层才允许解析 NetCraft.Client
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Launch(string[] args)
    {
        ClientMain.Run(args);
        return 0;
    }
}
