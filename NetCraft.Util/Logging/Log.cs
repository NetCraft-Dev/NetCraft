using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
namespace NetCraft.Logging;

public enum LogLevel
{
    Debug, Info, Warning, Error, Critical, None
}

public static class LogColors
{
    public const string Debug = "\u001b[36m";
    public const string Info = "\u001b[32m";
    public const string Warning = "\u001b[33m";
    public const string Error = "\u001b[31m";
    public const string Critical = "\u001b[35m";
    public const string Reset = "\u001b[0m";
}

public static class LogPlaceholders
{
    public const string Timestamp = "{timestamp}";
    public const string Level = "{level}";
    public const string Source = "{source}";
    public const string File = "{file}";
    public const string Line = "{line}";
    public const string Member = "{member}";
    public const string Message = "{message}";
    public const string NewLine = "{newline}";
    public const string Tab = "{tab}";
    public const string Space = "{space}";
}

public static class Log
{
    private static readonly Stack<string> _logSourceStack = new();
    private static string _defaultLogSource = "Unknown";
    private static readonly Dictionary<Type, string> _classSourceCache = new();

    public static void SetClassSource<T>() => SetClassSource(typeof(T));

    public static void SetClassSource(Type type)
    {
        if (type == null) return;
        var fullName = type.FullName ?? type.Name;
        lock (_classSourceCache)
        {
            if (!_classSourceCache.ContainsKey(type))
                _classSourceCache[type] = fullName;
            _defaultLogSource = fullName;
            lock (_logSourceStack)
            {
                if (_logSourceStack.Count == 0)
                    _logSourceStack.Push(fullName);
                else
                {
                    _logSourceStack.Pop();
                    _logSourceStack.Push(fullName);
                }
            }
        }
    }

    public static IDisposable PushMethodSource<T>([CallerMemberName] string methodName = "")
    {
        var className = typeof(T).FullName ?? typeof(T).Name;
        return PushSource($"{className}.{methodName}");
    }

    public static IDisposable PushMethodSource([CallerMemberName] string methodName = "")
    {
        var frame = new System.Diagnostics.StackFrame(1);
        var declaringType = frame.GetMethod()?.DeclaringType;
        if (declaringType != null)
        {
            var className = declaringType.FullName ?? declaringType.Name;
            return PushSource($"{className}.{methodName}");
        }
        return PushSource(methodName);
    }

    public static void SetDefaultSource(string source) => _defaultLogSource = source;

    public static IDisposable PushSource(string source) => new LogSourceScope(source);

    private static string GetCurrentSource()
    {
        lock (_logSourceStack)
            return _logSourceStack.Count > 0 ? _logSourceStack.Peek() : _defaultLogSource;
    }

    private sealed class LogSourceScope : IDisposable
    {
        public LogSourceScope(string source)
        {
            lock (_logSourceStack)
                _logSourceStack.Push(source);
        }

        public void Dispose()
        {
            lock (_logSourceStack)
            {
                if (_logSourceStack.Count > 0)
                    _logSourceStack.Pop();
            }
        }
    }

    private static string _logDirectory = null!;
    //两个出口默认只到 Info 详细日志由内核按 --debug 调 SetConsoleLevel/SetFileLevel 放开
    //旧版默认 Debug 不加 flag 也刷满调试日志 控制台 IO 与格式化白吃 CPU
    private static LogLevel _consoleLevel = LogLevel.Info;
    private static LogLevel _fileLevel = LogLevel.Info;
    private static StreamWriter _fileWriter = null!;
    private static readonly object _lock = new();
    private static readonly object _consoleLock = new();

    //异步日志缓冲 调用线程只打包原始字段入队 不拼字符串也不碰 IO
    //_pending 动态增长 由输出线程在锁内整体换出后再在锁外格式化 减少两边的争用
    private static readonly object _bufferLock = new();
    private static List<LogEntry> _pending = new();
    //_signal 每入队一次放行一次 输出线程靠它睡眠唤醒而不是空转轮询
    private static readonly SemaphoreSlim _signal = new(0);
    private static Thread? _outputThread;
    private static volatile bool _shutdown;

    //LogEntry 一条待输出日志
    //格式化一律留给输出线程 调用线程只取时间与来源 这两个必须当场取否则跨线程会串
    //Barrier 非空表示这是一条屏障 输出线程执行到它就说明它前面的日志都已写出并刷盘
    //RawConsole/RawFile 非空表示跳过格式化直接输出 供日志系统自身的提示使用
    private readonly record struct LogEntry(
        LogLevel Level,
        string Message,
        string FilePath,
        int LineNumber,
        string MemberName,
        string Source,
        string Timestamp,
        bool FlushFile,
        Action? Barrier,
        string? RawConsole,
        string? RawFile);

    private static LogEntry RawEntry(string consoleLine, string fileLine) => new(
        LogLevel.Info, string.Empty, string.Empty, 0, string.Empty, string.Empty,
        string.Empty, true, null, consoleLine, fileLine);

    public static event Action<string>? OnLogOutput;

    //HistoryCapacity 历史缓存条数
    //GUI 起得比日志系统晚 订阅之前发生的日志只能靠这份缓存补看
    //开调试模式时启动阶段轻松上万条 原先两千条一眨眼就被后来的挤掉 用户翻到头也看不到开服那一段
    private const int HistoryCapacity = 20000;

    //历史缓存 记的是喂给 OnLogOutput 的那份文本
    //GUI 起来得比日志系统晚 订阅之前发生的日志只能靠这份缓存补看 格式一致拼起来才无缝
    private static readonly object _historyLock = new();
    private static readonly string[] _history = new string[HistoryCapacity];
    private static long _historyCount;

    //RecordHistory 记一行进环形缓存 只在输出线程调用
    private static void RecordHistory(string line)
    {
        lock (_historyLock)
        {
            _history[(int)(_historyCount % HistoryCapacity)] = line;
            _historyCount++;
        }
    }

    //GetHistory 取历史日志快照 按时间从旧到新
    public static string[] GetHistory()
    {
        lock (_historyLock)
        {
            var count = (int)Math.Min(_historyCount, HistoryCapacity);
            var start = _historyCount - count;
            var result = new string[count];
            for (var i = 0; i < count; i++)
                result[i] = _history[(int)((start + i) % HistoryCapacity)];
            return result;
        }
    }

    //ClearHistory 清空历史缓存 只在补看时用一次 取完就该释放
    public static void ClearHistory()
    {
        lock (_historyLock)
        {
            Array.Clear(_history);
            _historyCount = 0;
        }
    }

    private static int _warningCount;
    private static int _errorCount;

    public static int WarningCount => _warningCount;
    public static int ErrorCount => _errorCount;

    private static string? _lastWarningMessage;
    private static string? _lastErrorMessage;

    public static string? LastWarningMessage => _lastWarningMessage;
    public static string? LastErrorMessage => _lastErrorMessage;

    private static bool _enableFileLogging = true;

    //SimpleFormat 简洁格式 对应 2026-09-30 17:06:09[INFO] - 日志体
    private static readonly string SimpleFormat = $"{LogPlaceholders.Timestamp}[{LogPlaceholders.Level}] - {LogPlaceholders.Message}";

    //DetailFormat 详细格式 --debug 用 多带文件与行号 对应 2026-09-30 17:06:09.123[DBG][xxx.cs:xx] - 日志体
    private static readonly string DetailFormat = $"{LogPlaceholders.Timestamp}[{LogPlaceholders.Level}][{LogPlaceholders.File}:{LogPlaceholders.Line}] - {LogPlaceholders.Message}";

    //DefaultFormat 默认格式串 占位符直接取常量保持两边同步
    private static readonly string DefaultFormat = SimpleFormat;

    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private const string DetailTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private static string _consoleFormat = DefaultFormat;
    private static string _fileFormat = DefaultFormat;
    private static string _timestampFormat = TimestampFormat;
    //预编译的格式模板 改格式串时同步重建 输出线程每条日志直接按片段拼接
    private static CompiledFormat _consoleCompiled = new(DefaultFormat);
    private static CompiledFormat _fileCompiled = new(DefaultFormat);

    //LevelNames 级别名按枚举值索引 输出线程每条日志原本都要走一次 Enum.ToString 加 ToUpper
    private static readonly string[] LevelNames = { "DBG", "INFO", "WARN", "ERROR", "CRIT", "NONE" };

    //缓冲区条数上限 生产速度超过输出速度时必须丢日志 否则 _pending 会一路涨到 OutOfMemory
    //实测 --debug 全量日志能到 16 万条/秒 控制台写出远跟不上 内存再大也顶不住
    //三档让路: 软上限丢 Debug/Info 硬上限再丢 Warning 顶上时连 Error 一起丢 保住进程优先
    private const int PendingSoftLimit = 65536;
    private const int PendingHardLimit = 131072;
    private const int PendingMaxLimit = 262144;

    //MaxMessageLength 单条消息字符上限 超出即截断
    //区块数据这类大对象 ToString 单条能到几十 KB 截断省的是缓冲区里实打实的内存
    private const int MaxMessageLength = 4096;

    //_droppedCounts 按级别累计的被丢弃条数 输出线程汇总成一条 Error 提示后清零
    private static readonly long[] _droppedCounts = new long[LevelNames.Length];

    private static int _retentionDays = 30;

    static Log()
    {
        EnableVirtualTerminalSupport();
        StartOutputThread();
        SetLogDirectory(null);
        //进程退出前把缓冲区排空并刷盘 否则崩溃现场的末尾几条日志会留在内存里丢掉
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
    }

    public static void SetRetentionDays(int days)
    {
        if (days < 0) days = 0;
        _retentionDays = days;
    }

    public static int GetRetentionDays() => _retentionDays;

    private static void CleanupOldLogs()
    {
        if (_retentionDays <= 0 || string.IsNullOrEmpty(_logDirectory))
            return;
        try
        {
            if (!Directory.Exists(_logDirectory))
                return;
            var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                .Select(f => new FileInfo(f))
                .ToList();
            if (logFiles.Count == 0)
                return;
            var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
            var deletedCount = 0;
            foreach (var file in logFiles)
            {
                if (file.CreationTime < cutoffDate)
                {
                    try
                    {
                        file.Delete();
                        deletedCount++;
                    }
                    catch
                    {
                    }
                }
            }
            if (deletedCount > 0)
                WriteCleanupLog($"[Log Cleanup] Deleted {deletedCount} old log file(s) (retention: {_retentionDays} days)");
        }
        catch
        {
        }
    }

    //WriteCleanupLog 日志系统自身的清理提示 走同一条输出队列保证与其它日志不交错
    private static void WriteCleanupLog(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString(_timestampFormat);
            var logMessage = $"{timestamp}[INFO] - {message}";
            Enqueue(RawEntry($"{timestamp}{LogColors.Info}[INFO]{LogColors.Reset} - {message}", logMessage));
        }
        catch
        {
        }
    }

    public static void ManualCleanup() => CleanupOldLogs();

    public static void SetConsoleFormat(string format)
    {
        _consoleFormat = format;
        _consoleCompiled = new CompiledFormat(format);
    }

    public static void SetFileFormat(string format)
    {
        _fileFormat = format;
        _fileCompiled = new CompiledFormat(format);
    }

    public static void SetTimestampFormat(string format) => _timestampFormat = format;

    public static void ResetToDefaultFormats()
    {
        _consoleFormat = DefaultFormat;
        _fileFormat = DefaultFormat;
        _consoleCompiled = new CompiledFormat(DefaultFormat);
        _fileCompiled = new CompiledFormat(DefaultFormat);
        _timestampFormat = TimestampFormat;
    }

    //SetVerbose 详细模式开关 由 --debug 驱动
    //开着时时间戳带毫秒 格式多带文件与行号 关着时一行尽量短
    public static void SetVerbose(bool verbose)
    {
        _timestampFormat = verbose ? DetailTimestampFormat : TimestampFormat;
        _consoleCompiled = new CompiledFormat(verbose ? DetailFormat : SimpleFormat);
        _fileCompiled = new CompiledFormat(verbose ? DetailFormat : SimpleFormat);
    }

    public static void SetLogDirectory(string? directory)
    {
        lock (_lock)
        {
            CloseFileWriter();
            _logDirectory = string.IsNullOrEmpty(directory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")
                : directory;
            if (_enableFileLogging)
                InitializeFileWriter();
        }
    }

    private static void InitializeFileWriter()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
            CleanupOldLogs();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var logFile = Path.Combine(_logDirectory, $"{timestamp}.log");
            _fileWriter = new StreamWriter(logFile, true, System.Text.Encoding.UTF8);
            //不逐条刷盘 由输出线程在批量写出后或遇到 Error 级日志时统一刷 每条一次 fsync 会把生成线程拖死
            _fileWriter.AutoFlush = false;
        }
        catch
        {
            _enableFileLogging = false;
            _fileWriter = null!;
        }
    }

    private static void CloseFileWriter()
    {
        if (_fileWriter == null) return;
        try
        {
            _fileWriter.Flush();
            _fileWriter.Close();
            _fileWriter.Dispose();
        }
        catch
        {
        }
        finally
        {
            _fileWriter = null!;
        }
    }

    public static void EnableFileLogging(bool enable)
    {
        lock (_lock)
        {
            if (_enableFileLogging == enable) return;
            _enableFileLogging = enable;
            if (enable)
            {
                if (!string.IsNullOrEmpty(_logDirectory))
                    InitializeFileWriter();
            }
            else
                CloseFileWriter();
        }
    }

    public static bool IsFileLoggingEnabled => _enableFileLogging && _fileWriter != null;

    public static void SetConsoleLevel(LogLevel level) => _consoleLevel = level;
    public static void SetFileLevel(LogLevel level) => _fileLevel = level;

    private static string GetAnsiColor(LogLevel level) => level switch
    {
        LogLevel.Debug => LogColors.Debug,
        LogLevel.Info => LogColors.Info,
        LogLevel.Warning => LogColors.Warning,
        LogLevel.Error => LogColors.Error,
        LogLevel.Critical => LogColors.Critical,
        _ => LogColors.Reset
    };

    //静态分隔符表 原先每条日志都 new 一个字符数组给 LastIndexOfAny 是无谓的分配
    private static readonly char[] PathSeparators = { '\\', '/' };

    private static string ExtractFileName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "unknown";
        var lastSeparator = filePath.LastIndexOfAny(PathSeparators);
        return lastSeparator >= 0 && lastSeparator < filePath.Length - 1
            ? filePath.Substring(lastSeparator + 1)
            : filePath;
    }

    //LevelName 取级别名 走查表避免每条日志一次 Enum.ToString 加 ToUpper
    private static string LevelName(LogLevel level)
    {
        var index = (int)level;
        return index >= 0 && index < LevelNames.Length ? LevelNames[index] : level.ToString();
    }

    //CompiledFormat 预编译的日志格式模板
    //原先 FormatLogMessage 每条日志做十次 string.Replace 每次都全串扫描并新分配一条字符串
    //这里构造时按占位符切成 字面量/槽位 序列 输出时只往 StringBuilder 里 Append
    private sealed class CompiledFormat
    {
        //Slot 占位符对应的取值槽
        private enum Slot
        {
            Timestamp, Level, Source, File, Line, Member, Message, NewLine, Tab, Space, Literal
        }

        private readonly Slot[] _slots;
        private readonly string[] _texts;

        public CompiledFormat(string format)
        {
            var slots = new List<Slot>();
            var texts = new List<string>();
            var index = 0;
            while (index < format.Length)
            {
                var open = format.IndexOf('{', index);
                if (open < 0)
                {
                    slots.Add(Slot.Literal);
                    texts.Add(format[index..]);
                    break;
                }
                if (open > index)
                {
                    slots.Add(Slot.Literal);
                    texts.Add(format[index..open]);
                }
                var close = format.IndexOf('}', open + 1);
                if (close < 0)
                {
                    slots.Add(Slot.Literal);
                    texts.Add(format[open..]);
                    break;
                }
                var token = format[(open + 1)..close];
                var slot = Resolve(token);
                slots.Add(slot);
                //认不出的占位符原样保留 格式串写错时输出里能直接看出来
                texts.Add(slot == Slot.Literal ? format[open..(close + 1)] : string.Empty);
                index = close + 1;
            }
            _slots = slots.ToArray();
            _texts = texts.ToArray();
        }

        private static Slot Resolve(string token) => token switch
        {
            "timestamp" => Slot.Timestamp,
            "level" => Slot.Level,
            "source" => Slot.Source,
            "file" => Slot.File,
            "line" => Slot.Line,
            "member" => Slot.Member,
            "message" => Slot.Message,
            "newline" => Slot.NewLine,
            "tab" => Slot.Tab,
            "space" => Slot.Space,
            _ => Slot.Literal
        };

        //Append 按模板把一条日志追加进缓冲
        //levelColor 非空时只给等级标记上色 其余部分保持默认色 对应日志里只有 [INFO] 这类标记是彩的
        public void Append(StringBuilder sb, in LogEntry entry, string fileName, string levelName, string levelColor)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                switch (_slots[i])
                {
                    case Slot.Literal: sb.Append(_texts[i]); break;
                    case Slot.Timestamp: sb.Append(entry.Timestamp); break;
                    case Slot.Level:
                        if (levelColor.Length == 0)
                        {
                            sb.Append(levelName);
                            break;
                        }
                        sb.Append(levelColor).Append(levelName).Append(LogColors.Reset);
                        break;
                    case Slot.Source: sb.Append(entry.Source); break;
                    case Slot.File: sb.Append(fileName); break;
                    case Slot.Line: sb.Append(entry.LineNumber); break;
                    case Slot.Member: sb.Append(string.IsNullOrEmpty(entry.MemberName) ? "unknown" : entry.MemberName); break;
                    case Slot.Message: sb.Append(entry.Message); break;
                    case Slot.NewLine: sb.Append(Environment.NewLine); break;
                    case Slot.Tab: sb.Append('\t'); break;
                    case Slot.Space: sb.Append(' '); break;
                }
            }
        }
    }

    //WriteLog 记录一条日志 调用线程只做计数与入队
    //时间与来源必须当场取: 来源在当前线程的来源栈上 时间也该是发生时刻而不是写出时刻
    //所有字符串拼接与 IO 都留给输出线程 生成/网络等热路径不再被日志拖住
    private static void WriteLog(LogLevel level, string message, string filePath, int lineNumber, string memberName)
    {
        if (level == LogLevel.Warning) System.Threading.Interlocked.Increment(ref _warningCount);
        if (level == LogLevel.Error || level == LogLevel.Critical) System.Threading.Interlocked.Increment(ref _errorCount);

        if (level == LogLevel.Warning)
            _lastWarningMessage = message;
        else if (level == LogLevel.Error || level == LogLevel.Critical)
            _lastErrorMessage = message;

        var toConsole = _consoleLevel != LogLevel.None && level >= _consoleLevel;
        var toFile = _enableFileLogging && _fileWriter != null && _fileLevel != LogLevel.None && level >= _fileLevel;
        //两个出口都不收就整条丢掉 连来源与时间都不用取
        if (!toConsole && !toFile) return;

        //超长消息在入队前截断 缓冲里存的就是截断后的短串 省的正是那块内存
        if (message.Length > MaxMessageLength)
            message = string.Concat(message.AsSpan(0, MaxMessageLength), "...(已截断)");

        Enqueue(new LogEntry(level, message, filePath, lineNumber, memberName, GetCurrentSource(),
            DateTime.Now.ToString(_timestampFormat), level >= LogLevel.Error, null, null, null));
    }

    //SetMainThread 圈定服务端主线程
    //只有主线程的日志位置才代表世界推进走到了哪 网络与 IO 线程的日志很频繁会把位置冲掉
    public static void SetMainThread(int threadId) => _mainThreadId = threadId;

    //LastOriginFile/Line/Member/Source 最近一条主线程真正写出的日志的调用位置
    //看门狗判定 Can't keep up 时把它打出来 用来确认卡顿前后主线程最后走到哪一行
    private static int _mainThreadId;
    private static string _lastOriginFile = "";
    private static int _lastOriginLine;
    private static string _lastOriginMember = "";
    private static string _lastOriginSource = "";

    public static string LastOriginFile => _lastOriginFile;
    public static int LastOriginLine => _lastOriginLine;
    public static string LastOriginMember => _lastOriginMember;
    public static string LastOriginSource => _lastOriginSource;

    //Enqueue 入队并唤醒输出线程
    //缓冲区到达上限就丢日志: 生产快于输出时不丢就是 OutOfMemory
    //按级别让路 Debug/Info 先丢 Warning 次之 Error/Critical 最后 别让爆日志把崩溃现场一起带走
    private static void Enqueue(LogEntry entry)
    {
        if (_shutdown) return;
        //只记主线程且有正文的条目 屏障与裸输出不带调用位置 其它线程的日志也不该盖住主线程的位置
        if (entry.Message.Length > 0 && Environment.CurrentManagedThreadId == _mainThreadId)
        {
            _lastOriginFile = entry.FilePath;
            _lastOriginLine = entry.LineNumber;
            _lastOriginMember = entry.MemberName;
            _lastOriginSource = entry.Source;
        }
        lock (_bufferLock)
        {
            var count = _pending.Count;
            if (count >= PendingMaxLimit
                || (count >= PendingHardLimit && entry.Level < LogLevel.Error)
                || (count >= PendingSoftLimit && entry.Level < LogLevel.Warning))
            {
                Interlocked.Increment(ref _droppedCounts[(int)entry.Level]);
                return;
            }
            _pending.Add(entry);
        }
        _signal.Release();
    }

    //ReportDrops 把累计的丢弃量汇总成一条 Error 提示
    //必须自己直接产出不能再入队 否则缓冲区已满时这条提示又会被丢掉
    private static void ReportDrops()
    {
        List<string>? parts = null;
        for (var i = 0; i < _droppedCounts.Length; i++)
        {
            var dropped = Interlocked.Exchange(ref _droppedCounts[i], 0);
            if (dropped <= 0) continue;
            parts ??= new List<string>();
            parts.Add($"{dropped} 条 {LevelNames[i]}");
        }
        if (parts is null) return;

        var message = $"日志缓冲区已满 为防止崩溃已丢弃 {string.Join("、", parts)}";
        //对齐 WriteLog 的错误统计语义 这条提示本身也计入错误数与最后错误消息
        Interlocked.Increment(ref _errorCount);
        _lastErrorMessage = message;
        ProcessBatch(new List<LogEntry>
        {
            new(LogLevel.Error, message, "Log.cs", 0, nameof(ReportDrops), "NetCraft.Logging.Log",
                DateTime.Now.ToString(_timestampFormat), true, null, null, null)
        });
    }

    //StartOutputThread 启动唯一输出线程 后台线程不阻止进程退出
    private static void StartOutputThread()
    {
        _outputThread = new Thread(OutputLoop)
        {
            Name = "NetCraft-Log",
            IsBackground = true,
        };
        _outputThread.Start();
    }

    //OutputLoop 输出线程主体 反复把缓冲区整体换出再处理
    //换出只在锁内做一次 格式化与 IO 都在锁外 生产端几乎不会被挡住
    private static void OutputLoop()
    {
        var draining = new List<LogEntry>();
        while (true)
        {
            _signal.Wait();
            //清掉多余信号 一轮把缓冲区里所有条目一起处理掉
            while (_signal.Wait(0)) { }
            lock (_bufferLock)
            {
                //与空列表对调 旧清单继续用来装载下一轮 容量按峰值保留 这就是动态增长的那块缓冲
                (_pending, draining) = (draining, _pending);
            }
            if (draining.Count > 0)
            {
                ProcessBatch(draining);
                draining.Clear();
            }
            //丢弃提示在本轮积压处理完之后报 免得提示被自己前面的日志盖住
            ReportDrops();
            if (_shutdown) return;
        }
    }

    //ProcessBatch 处理一批日志 控制台整批合并成一次写出 文件行仍逐条交给 StreamWriter 缓冲
    //控制台的每次写出都会把底层流刷出去 逐条写等于逐条刷盘 是日志开销里最贵的一段
    private static void ProcessBatch(List<LogEntry> batch)
    {
        var flushFile = false;
        var consoleBatch = new StringBuilder();
        var single = new StringBuilder(192);
        foreach (var entry in batch)
        {
            //屏障: 先把前面的都刷盘再放行等待方 保证 Flush() 返回后日志确实已经落盘
            if (entry.Barrier is not null)
            {
                FlushConsole(consoleBatch);
                FlushFileWriter();
                entry.Barrier();
                continue;
            }
            if (entry.RawConsole is not null)
            {
                AppendConsole(consoleBatch, entry.RawConsole);
                if (entry.RawFile is not null) WriteFileLine(entry.RawFile);
                continue;
            }
            //文件名与级别名两个出口都要用 这里只算一次
            var fileName = ExtractFileName(entry.FilePath);
            var levelName = LevelName(entry.Level);
            if (_consoleLevel != LogLevel.None && entry.Level >= _consoleLevel)
            {
                single.Clear();
                //只给等级标记上色 整行包色会让正文也跟着变色 长日志读起来很吵
                _consoleCompiled.Append(single, entry, fileName, levelName, GetAnsiColor(entry.Level));
                AppendConsole(consoleBatch, single);
            }
            if (_fileLevel == LogLevel.None || entry.Level < _fileLevel) continue;
            single.Clear();
            _fileCompiled.Append(single, entry, fileName, levelName, string.Empty);
            WriteFileLine(single.ToString());
            if (entry.FlushFile) flushFile = true;
        }
        FlushConsole(consoleBatch);
        //Error 及以上整批写完就刷盘 平时不刷
        if (flushFile) FlushFileWriter();
    }

    //AppendConsole 把一行并入批缓冲 订阅者仍按条拿到文本
    private static void AppendConsole(StringBuilder batch, string line)
    {
        RecordHistory(line);
        OnLogOutput?.Invoke(line);
        batch.Append(line).Append('\n');
    }

    private static void AppendConsole(StringBuilder batch, StringBuilder line)
    {
        //历史与订阅者拿同一份文本 只转一次
        var text = line.ToString();
        RecordHistory(text);
        if (OnLogOutput is not null) OnLogOutput(text);
        batch.Append(line).Append('\n');
    }

    //FlushConsole 整批一次写出 只有这里碰控制台
    //写出失败就永久关掉控制台出口 输出线程不能因为一次 IO 失败把后续日志全带走
    private static void FlushConsole(StringBuilder batch)
    {
        if (batch.Length == 0) return;
        var text = batch.ToString();
        batch.Clear();
        try
        {
            lock (_consoleLock)
                Console.Out.Write(text);
        }
        catch
        {
            _consoleLevel = LogLevel.None;
        }
    }

    //WriteFileLine 写一行到日志文件 只在输出线程调用
    private static void WriteFileLine(string line)
    {
        if (!_enableFileLogging || _fileWriter is null) return;
        lock (_lock)
        {
            try
            {
                _fileWriter.WriteLine(line);
            }
            catch
            {
                _enableFileLogging = false;
                CloseFileWriter();
            }
        }
    }

    //FlushFileWriter 日志落盘的唯一出口 由输出线程批量触发
    private static void FlushFileWriter()
    {
        if (!_enableFileLogging || _fileWriter is null) return;
        lock (_lock)
        {
            try
            {
                _fileWriter.Flush();
            }
            catch
            {
                _enableFileLogging = false;
                CloseFileWriter();
            }
        }
    }

    //Flush 阻塞到此刻之前入队的日志都已写出并刷盘
    //退出前与需要拿日志做断言的场景调用 平时不需要
    //带超时: 输出线程万一卡在控制台 IO 上 退出流程不能被它永久拖住
    public static void Flush()
    {
        if (_shutdown || _outputThread is null) return;
        using var done = new ManualResetEventSlim(false);
        Enqueue(new LogEntry(LogLevel.Info, string.Empty, string.Empty, 0, string.Empty, string.Empty,
            string.Empty, true, done.Set, null, null));
        done.Wait(TimeSpan.FromSeconds(2));
    }

    //Shutdown 排空缓冲区停止输出线程并关闭文件 幂等
    public static void Shutdown()
    {
        if (_shutdown) return;
        Flush();
        _shutdown = true;
        _signal.Release();
        _outputThread?.Join(TimeSpan.FromSeconds(2));
        lock (_lock) CloseFileWriter();
    }

    //DebugLogHandler Debug 日志的延迟构造参数
    //原来 Log.Debug($"...") 会先把插值拼成字符串再丢进来 级别关闭时那个字符串白分配
    //逐格采样与每 tick 查询这类热路径上丢弃式分配占了大头 改成级别关时连拼都不拼
    [InterpolatedStringHandler]
    public ref struct DebugLogHandler
    {
        private readonly StringBuilder? _builder;

        public DebugLogHandler(int literalLength, int formattedCount)
            => _builder = DebugEnabled ? new StringBuilder(literalLength + formattedCount * 8) : null;

        //Enabled 是否真的需要产出字符串 关闭时下游直接短路
        public readonly bool Enabled => _builder is not null;

        public void AppendLiteral(string value) => _builder?.Append(value);

        public void AppendFormatted<T>(T value) => _builder?.Append(value);

        public void AppendFormatted(string? value) => _builder?.Append(value);

        public void AppendFormatted(ReadOnlySpan<char> value) => _builder?.Append(value);

        public override readonly string ToString() => _builder?.ToString() ?? string.Empty;
    }

    //DebugEnabled Debug 是否会被任一出口收下
    public static bool DebugEnabled
        => (_consoleLevel != LogLevel.None && LogLevel.Debug >= _consoleLevel)
            || (_enableFileLogging && _fileWriter != null && _fileLevel != LogLevel.None
                && LogLevel.Debug >= _fileLevel);

    public static void Debug(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Debug, message, filePath, lineNumber, memberName);

    //Debug 插值重载 级别关闭时 handler 不建缓冲区 Appends 全成空操作
    public static void Debug([InterpolatedStringHandlerArgument] ref DebugLogHandler handler,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        if (!handler.Enabled) return;
        WriteLog(LogLevel.Debug, handler.ToString(), filePath, lineNumber, memberName);
    }

    public static void Info(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Info, message, filePath, lineNumber, memberName);

    public static void Warning(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Warning, message, filePath, lineNumber, memberName);

    public static void Error(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Error, message, filePath, lineNumber, memberName);

    public static void Critical(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Critical, message, filePath, lineNumber, memberName);

    public static void Exception(Exception ex, string? message = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        var msg = message == null ? ex.ToString() : $"{message}: {ex.Message}\n{ex.StackTrace}";
        WriteLog(LogLevel.Error, msg, filePath, lineNumber, memberName);
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessingFlag = 0x0004;

    private static void EnableVirtualTerminalSupport()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == (IntPtr)(-1)) return;
            if (!GetConsoleMode(handle, out var mode)) return;
            var newMode = mode | EnableVirtualTerminalProcessingFlag;
            if (newMode != mode) SetConsoleMode(handle, newMode);
        }
        catch
        {
        }
    }
}