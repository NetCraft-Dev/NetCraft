using System.Text;

namespace NetCraft.TPGA.Logging;

//LogLevel 日志级别 越界值越大优先级越高
public enum LogLevel { Debug, Info, Warning, Error, Critical, None }

//Log 简化版静态日志 控制台 + 文件
//仿 NetCraft.Util.Log 接口与格式 独立实现不引入 NetCraft.Util 依赖链
//格式 时间 - 级别 - [来源] - 消息
public static class Log
{
    private static string _logDirectory = "logs";
    private static LogLevel _consoleLevel = LogLevel.Debug;
    private static LogLevel _fileLevel = LogLevel.Debug;
    private static StreamWriter? _fileWriter;
    private static readonly object _lock = new();
    private static readonly string _timestampFormat = "yyyy-MM-dd HH:mm:ss,fff";

    //Init 初始化日志目录并打开当天日志文件
    public static void Init(string logDirectory, LogLevel consoleLevel = LogLevel.Debug, LogLevel fileLevel = LogLevel.Debug)
    {
        _logDirectory = logDirectory;
        _consoleLevel = consoleLevel;
        _fileLevel = fileLevel;
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);
        OpenFileWriter();
    }

    //OpenFileWriter 打开按日期命名的日志文件 失败降级仅控制台
    private static void OpenFileWriter()
    {
        if (string.IsNullOrEmpty(_logDirectory)) return;
        try
        {
            var path = Path.Combine(_logDirectory, $"tpga_{DateTime.Now:yyyyMMdd}.log");
            _fileWriter = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        }
        catch
        {
            _fileWriter = null;
        }
    }

    //Debug/Info/Warning/Error/Critical 带来源的级别入口
    public static void Debug(string source, string message) => Write(LogLevel.Debug, source, message);
    public static void Info(string source, string message) => Write(LogLevel.Info, source, message);
    public static void Warning(string source, string message) => Write(LogLevel.Warning, source, message);
    public static void Error(string source, string message) => Write(LogLevel.Error, source, message);
    public static void Critical(string source, string message) => Write(LogLevel.Critical, source, message);

    //不带来源的重载 默认来源 TPGA
    public static void Info(string message) => Write(LogLevel.Info, "TPGA", message);
    public static void Warning(string message) => Write(LogLevel.Warning, "TPGA", message);
    public static void Error(string message) => Write(LogLevel.Error, "TPGA", message);

    //Write 级别过滤后写控制台和文件
    private static void Write(LogLevel level, string source, string message)
    {
        if (level < _consoleLevel && level < _fileLevel) return;
        var line = $"{DateTime.Now.ToString(_timestampFormat)} - {level.ToString().ToUpper(),8} - [{source}] - {message}";
        lock (_lock)
        {
            if (level >= _consoleLevel)
                Console.WriteLine(line);
            if (level >= _fileLevel && _fileWriter != null)
            {
                try { _fileWriter.WriteLine(line); }
                catch { /*写入失败忽略避免崩溃*/ }
            }
        }
    }
}
