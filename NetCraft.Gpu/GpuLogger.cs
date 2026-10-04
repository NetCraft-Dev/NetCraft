namespace NetCraft.Gpu;

//IGpuLogger GPU 模块日志抽象解耦 NetCraft.Logging 让 NetCraft.Gpu 可独立
//调用方注入自定义实现如 NetCraft.Util.Logging.Log 的适配器
//默认 ConsoleGpuLogger 输出到 Console.Error
public interface IGpuLogger
{
    void Warning(string message);
    void Info(string message);
    void Error(string message);
}

//ConsoleGpuLogger 默认实现输出到 Console.Error
public sealed class ConsoleGpuLogger : IGpuLogger
{
    public void Warning(string message) => Console.Error.WriteLine($"[GPU] WARN {message}");
    public void Info(string message) => Console.Error.WriteLine($"[GPU] INFO {message}");
    public void Error(string message) => Console.Error.WriteLine($"[GPU] ERROR {message}");
}
