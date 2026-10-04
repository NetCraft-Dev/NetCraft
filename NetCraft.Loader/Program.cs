using NetCraft.Config;
using NetCraft;
using NetCraft.Game;
using NetCraft.Logging;
using NetCraft.ModLoader;

namespace NetCraft.Loader;

//NetCraft.Loader 启动器入口对应原版 launcher
//控制台 EXE 负责模式分发调用 ServerMain.Run 或 ClientMain.Run
//默认 --client 模式其余参数透传给 Run 由现有 LaunchOptions+GameOptions 机制解析
public static class Program
{
    //Main 启动器入口
    //只做一件事：注册内核程序集解析回调。内核程序集已挪出 deps.json，
    //运行时靠回调从 kernel 目录与主库内嵌资源取字节。这一步必须早于任何内核类型被解析，
    //而 JIT 会解析方法体里出现的全部类型，所以真正的启动逻辑必须挪到另一个方法里。
    public static int Main(string[] args)
    {
        EmbeddedAssemblyLoader.Initialize();
        return Launch(args);
    }

    //Launch 启动逻辑
    //声明 --server/--client 为内核 flag 让 LaunchOptions 自动吞掉不污染下游业务参数
    //扫描 args 识别模式后透传整个 args 调对应 Run
    private static int Launch(string[] args)
    {
        Log.Debug($"Main entry args={string.Join(",", args)}");
        LaunchOptions.DeclareKernelFlag("server");
        LaunchOptions.DeclareKernelFlag("client");
        LaunchOptions.DeclareKernelFlag("debug");

        //检测 --debug flag 开启全局调试模式触发 NetCraftKernel.PrintStartupInfo 等调试行为
        if (Array.IndexOf(args, "--debug") >= 0)
            DebugMode.IsEnabled = true;

        //模组注入跑在内核 Initialize 之前 日志出口不在这里提前放开 注入过程的 debug 记录会被整段丢掉
        if (DebugMode.IsEnabled)
        {
            Log.SetConsoleLevel(LogLevel.Debug);
            Log.SetFileLevel(LogLevel.Debug);
            Log.SetVerbose(true);
        }

        var mode = DetectMode(args);

        //模组引导必须早于内核子库被解析 所以夹在模式判定与 RunServer/RunClient 之间
        //这一步静态扫描 mods 目录并把注入规则装配好交给内嵌加载器 子库稍后按需加载时就会被改写
        ModBootstrap.Run(mode == LaunchMode.Server ? ModEnvironment.Server : ModEnvironment.Client);

        var result = mode switch
        {
            LaunchMode.Server => RunServer(args),
            _ => RunClient(args)
        };
        Log.Debug($"Main exit result={result}");
        return result;
    }

    //DetectMode 扫描 args 取第一个出现的 --server/--client 决定模式
    //未传模式 flag 默认 Client 对齐原版客户端优先
    private static LaunchMode DetectMode(string[] args)
    {
        //Log.Debug($"DetectMode 入口 args={string.Join(",", args)}");
        foreach (var arg in args)
        {
            if (arg == "--server")
            {
                //Log.Debug($"DetectMode 出口 result={LaunchMode.Server}");
                return LaunchMode.Server;
            }
            if (arg == "--client")
            {
                //Log.Debug($"DetectMode 出口 result={LaunchMode.Client}");
                return LaunchMode.Client;
            }
        }
        //Log.Debug($"DetectMode 出口 result={LaunchMode.Client}");
        return LaunchMode.Client;
    }

    //RunServer 透传 args 调 ServerMain.Run
    //ServerMain 内部订阅 GameOptions 后触发 NetCraftKernel.Initialize 解析剩余参数
    private static int RunServer(string[] args)
    {
        //Log.Debug($"RunServer 入口 args={string.Join(",", args)}");
        ServerMain.Run(args);
        //Log.Debug($"RunServer 出口 result=0");
        return 0;
    }

    //RunClient 透传 args 调 ClientMain.Run
    //ClientMain 内部订阅 GameOptions 后触发 NetCraftKernel.Initialize 解析剩余参数
    private static int RunClient(string[] args)
    {
        Log.Debug("Calling ClientMain.Run...");
        ClientMain.Run(args);
        Log.Debug("RunClient exit");
        return 0;
    }
}

//LaunchMode 启动模式枚举
//Client 客户端含渲染与输入Server 纯服务端
internal enum LaunchMode
{
    Client,
    Server
}
