using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Network.Chat;
using NetCraft.Util;

namespace NetCraft;

//NetCraft 内核主入口（聚合类库的公开 API）。
//对应原版 net.minecraft.server.Main + net.minecraft.Bootstrap 的入口职责。
//注意：本类库不是 exe，调用方需自行创建 Main 函数并调用 Initialize。
public static class NetCraftKernel
{
    private static int _initialized;

    //初始化 NetCraft 内核。包括：
    //  <item>初始化内嵌程序集加载器（从内嵌资源加载子库 dll）</item>
    //  <item>解析启动参数（内核识别的消费，未识别的通过 LaunchOptions.UnhandledArgument 事件广播）</item>
    //  <item>打印启动版本/协议信息（Debug 时）</item>
    //幂等：多次调用只生效一次。
    //args 命令行参数 调用前订阅者应已订阅 LaunchOptions.UnhandledArgument
    public static void Initialize(string[]? args = null)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            //Log.Debug("Initialize 出口");
            return;
        }
        Log.Debug($"Initialize entry args={(args == null ? "null" : string.Join(",", args))}");

        // 0. 日志出口级别按调试模式定 两个出口一起走
        //    默认只到 Info 不加 --debug 时不该把 Debug 详细日志也刷出来
        //    Loader 已按 --debug 设过 DebugMode 这里再兜一层 宿主直接传参也能生效
        if (args is { Length: > 0 } && Array.IndexOf(args, "--debug") >= 0)
            DebugMode.IsEnabled = true;
        var detailLevel = DebugMode.IsEnabled ? LogLevel.Debug : LogLevel.Info;
        Log.SetConsoleLevel(detailLevel);
        Log.SetFileLevel(detailLevel);
        //--debug 下格式也换详细档 时间戳带毫秒并附上文件与行号
        Log.SetVerbose(DebugMode.IsEnabled);

        // 1. 先初始化内嵌资源加载器，确保后续类型解析能找到子库
        EmbeddedAssemblyLoader.Initialize();

        //设置日志源为内核
        Log.SetClassSource(typeof(NetCraftKernel));

        //全局托管异常兜底 崩溃报告落到程序根目录 crash-reports/ 下
        //客户端与服务端共用这一份逻辑 报告名里的角色段由各自启动入口设置
        CrashHandler.Install(AppPaths.BaseDirectory);

        //语言文件解压到根目录 lang/ 并按默认语言码先装一次表
        //放在这里是为了尽可能早 模组加载与内核启动期的日志才有词可取
        //真正的语言码由 ServerMain/ClientMain 读到配置后再重装一次
        NcLanguageFiles.Extract();
        NcLanguage.Load(Language.Default);

        //启动字符画banner
        PrintBanner();

        // 2. 解析启动参数 内核识别的消费 未识别的通过事件广播
        //    订阅者必须在调用 Initialize 前订阅 UnhandledArgument
        if (args is { Length: > 0 })
        {
            LaunchOptions.Parse(args);
        }

        // 3. 调试模式开启时打印启动信息（运行时 --debug flag 触发 Release 构建也可用）
        if (DebugMode.IsEnabled)
            PrintStartupInfo();
        //Log.Debug("Initialize 出口");
    }

    //获取内核版本。
    public static string Version => SharedConstants.Version;

    //获取当前协议版本号。
    public static int ProtocolVersion => SharedConstants.ProtocolVersion;

    //PrintStartupInfo 打印内核启动详情对应原版 Main 输出版本/协议/世界数据版本/内嵌子库
    //由 DebugMode.IsEnabled 运行时触发 Release 构建也可通过 --debug 开启
    private static void PrintStartupInfo()
    {
        //Log.Debug("PrintStartupInfo 入口");
        Log.Info($"Initializing kernel v{SharedConstants.Version}");
        Log.Info($"Protocol version: {SharedConstants.ProtocolVersion} (min {SharedConstants.ProtocolVersionLowerBound})");
        Log.Info($"World data version: {SharedConstants.WorldDataVersion}");
        Log.Info($"Target TPS: {SharedConstants.TicksPerSecond}");

        var embedded = EmbeddedAssemblyLoader.ListEmbeddedAssemblies();
        if (embedded.Count > 0)
        {
            Log.Info($"Embedded sub-libraries ({embedded.Count}):");
            foreach (var name in embedded)
                Log.Info($"  - {name}");
        }
        //Log.Debug("PrintStartupInfo 出口");
    }

    //打印启动字符画banner
    private static void PrintBanner()
    {
        ConsoleAnsiArtist.PrintRainbowText("NetCraft");
    }
}

