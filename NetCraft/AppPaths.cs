using System.IO;

namespace NetCraft;

//AppPaths 程序根目录路径集中管理
//以 AppContext.BaseDirectory 为基准避免依赖运行时工作目录
//消除"相对路径被解释为 cwd"的歧义统一 assets/worlds/logs 等子目录基准
public static class AppPaths
{
    //OverrideRoot 显式 --output-dir 传入则非空覆盖 BaseDirectory 整棵根目录
    //由 ServerMain/ClientMain 顶层调用 SetOverride 一次后续一从 AppPaths 取
    private static string? _overrideRoot;

    //BaseDirectory 程序根目录绝对路径优先 _overrideRoot 否则 AppContext.BaseDirectory
    public static string BaseDirectory => _overrideRoot ?? AppContext.BaseDirectory;

    //AssetsDir 资源根目录 assets/ 子目录供 AssetsExtractor 提取与业务加载
    public static string AssetsDir => Path.Combine(BaseDirectory, "assets");

    //DataDir 数据包根目录 data/ 子目录供 AssetsExtractor 提取 jar 的 data/ 前缀条目
    //服务端数据驱动来源 advancements/recipes/tags/functions 等
    public static string DataDir => Path.Combine(BaseDirectory, "data");

    //DatapacksDir 外部数据包目录 datapacks/ 子目录供 ResourceManager 扫描 *.zip 加载
    public static string DatapacksDir => Path.Combine(BaseDirectory, "datapacks");

    //WorldsDir 世界存档根目录 worlds/ 子目录供 LevelStorage
    public static string WorldsDir => Path.Combine(BaseDirectory, "worlds");

    //LogsDir 日志根目录 logs/ 子目录对齐 Log.cs 现有 AppDomain.BaseDirectory/logs
    public static string LogsDir => Path.Combine(BaseDirectory, "logs");

    //CrashReportsDir 崩溃报告根目录 crash-reports/ 子目录对齐原版
    public static string CrashReportsDir => Path.Combine(BaseDirectory, "crash-reports");

    //OptionsPath 客户端配置文件路径 assets/options.txt
    public static string OptionsPath => Path.Combine(AssetsDir, "options.txt");

    //ServerPropertiesPath 服务端配置文件路径根/server.properties
    public static string ServerPropertiesPath => Path.Combine(BaseDirectory, "server.properties");

    //OpsPath 管理员名单路径根/ops.json 对齐原版服务端根目录位置
    public static string OpsPath => Path.Combine(BaseDirectory, "ops.json");

    //WhitelistPath 白名单路径根/whitelist.json
    public static string WhitelistPath => Path.Combine(BaseDirectory, "whitelist.json");

    //BannedPlayersPath 玩家封禁名单路径根/banned-players.json
    public static string BannedPlayersPath => Path.Combine(BaseDirectory, "banned-players.json");

    //BannedIpsPath IP 封禁名单路径根/banned-ips.json
    public static string BannedIpsPath => Path.Combine(BaseDirectory, "banned-ips.json");

    //SetOverride 设置 --output-dir 覆盖整棵根目录
    //optionValue 空或空白忽略继续用 AppContext.BaseDirectory
    public static void SetOverride(string? optionValue)
    {
        if (string.IsNullOrWhiteSpace(optionValue)) return;
        _overrideRoot = Path.GetFullPath(optionValue);
    }
}
