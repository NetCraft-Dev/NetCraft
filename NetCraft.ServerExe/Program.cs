using System.Runtime.CompilerServices;
using NetCraft;
using NetCraft.Config;
using NetCraft.Game;
using NetCraft.Logging;
using NetCraft.ModLoader;

namespace NetCraft.ServerExe;

//Program 服务端独立启动入口
//只做进程入口与模组引导 真正的服务端实现留在 NetCraft.Server 类库里
public static class Program
{
    //Main 进程入口
    //第一件事注册内核程序集解析回调：内核程序集已挪出 deps.json，
    //运行时靠回调从 kernel 目录与主库内嵌资源取字节 必须早于任何内核类型被解析。
    //JIT 会解析方法体里出现的全部类型 所以下面两层都禁止内联，
    //否则 ServerMain 会被提前解析 直接绕过改写把未注入的 NetCraft.Server 拉起来。
    public static int Main(string[] args)
    {
        EmbeddedAssemblyLoader.Initialize();
        BootMods(args);
        return Launch(args);
    }

    //BootMods 模组引导 这一层只碰加载器 不碰服务端类型
    //--debug 必须在这里就放开日志出口 注入早于内核 Initialize 晚一步整段 debug 记录都会丢
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void BootMods(string[] args)
    {
        if (Array.IndexOf(args, "--debug") >= 0)
            DebugMode.IsEnabled = true;

        if (DebugMode.IsEnabled)
        {
            Log.SetConsoleLevel(LogLevel.Debug);
            Log.SetFileLevel(LogLevel.Debug);
            Log.SetVerbose(true);
        }

        ModBootstrap.Run(ModEnvironment.Server);
    }

    //Launch 服务端启动流程 到这层才允许解析 NetCraft.Server
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Launch(string[] args)
    {
        ServerMain.Run(args);
        return 0;
    }
}
