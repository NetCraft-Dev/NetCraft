using System.Diagnostics;
using NetCraft.Logging;

namespace NetCraft.ModLoader;

//ProfilerRelaunch 需要运行时注入时把进程带着原生注入层重启一遍
//ReJIT 的开关只在进程启动那一刻读环境变量 跑起来之后再设一律无效 只能重来一次
internal static class ProfilerRelaunch
{
    //ReloadFlag 由本类设给子进程 用来兜住"环境变量设了却没生效"导致的反复重启
    private const string ReloadFlag = "NC_PROFILER_ATTACHED";

    //ProfilerGuid 必须与原生库里的 CLSID 一致
    private const string ProfilerGuid = "{7A2E4C1B-9D3F-4E58-A6B0-1C5D8F2A3E70}";

    //LibraryNames 原生库在各平台上的文件名
    private static readonly string[] LibraryNames =
    {
        "lead_hook_native.dll",
        "liblead_hook_native.so",
        "lead_hook_native.dylib",
    };

    //Attached 当前进程已经带着原生注入层在跑
    //用户自己挂了别的 profiler 时也算已挂 不去抢
    public static bool Attached =>
        Environment.GetEnvironmentVariable(ReloadFlag) == "1" ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH"));

    //Relaunch 带原生注入层重启自身 返回 false 表示没有重启
    //父进程等子进程退出后原样转发退出码 返回 true 时调用方不该再往下走
    public static bool Relaunch()
    {
        var library = FindLibrary();
        if (library is null)
        {
            Log.Warning("Runtime injection needs the native layer but no lead_hook_native library was found next to the program");
            return false;
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            Log.Warning("Cannot locate the running executable, restart with the native layer is not possible");
            return false;
        }

        var startInfo = new ProcessStartInfo(exe) { UseShellExecute = false };
        //第 0 项是可执行文件自身 其余命令行原样转发 否则重启会丢掉 --debug 这类参数
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
            startInfo.ArgumentList.Add(args[i]);
        startInfo.Environment["CORECLR_ENABLE_PROFILING"] = "1";
        startInfo.Environment["CORECLR_PROFILER"] = ProfilerGuid;
        startInfo.Environment["CORECLR_PROFILER_PATH"] = library;
        startInfo.Environment[ReloadFlag] = "1";

        Log.Info("Restarting with the native injection layer to enable runtime injection rules");
        using var child = Process.Start(startInfo);
        if (child is null)
        {
            Log.Warning("The restarted process did not start");
            return false;
        }

        child.WaitForExit();
        Environment.Exit(child.ExitCode);
        return true;
    }

    //FindLibrary 在程序目录与 Lead.Hook 所在目录里找原生库
    //NC_PROFILER_PATH 可以指定别处那份 开发时指向 Rust 构建产物用
    private static string? FindLibrary()
    {
        var explicitPath = Environment.GetEnvironmentVariable("NC_PROFILER_PATH");
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        var directories = new List<string> { AppPaths.BaseDirectory };
        //Lead.Hook 由流加载时没有磁盘位置 这种时候只能靠程序目录
        var hookDirectory = Path.GetDirectoryName(typeof(Lead.Hook.HookEngine).Assembly.Location);
        if (!string.IsNullOrEmpty(hookDirectory))
            directories.Add(hookDirectory);

        foreach (var directory in directories)
        {
            foreach (var name in LibraryNames)
            {
                var candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}
