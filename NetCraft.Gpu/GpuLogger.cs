namespace NetCraft.Gpu;

//IGpuLogger GPU module logging abstraction, decoupled from NetCraft.Logging so NetCraft.Gpu stands alone
//Callers inject a custom implementation, e.g. an adapter over NetCraft.Util.Logging.Log
//The default ConsoleGpuLogger writes to Console.Error
public interface IGpuLogger
{
    void Warning(string message);
    void Info(string message);
    void Error(string message);
}

//ConsoleGpuLogger default implementation writing to Console.Error
public sealed class ConsoleGpuLogger : IGpuLogger
{
    public void Warning(string message) => Console.Error.WriteLine($"[GPU] WARN {message}");
    public void Info(string message) => Console.Error.WriteLine($"[GPU] INFO {message}");
    public void Error(string message) => Console.Error.WriteLine($"[GPU] ERROR {message}");
}
