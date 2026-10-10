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

    //Fallback log directory, the host's own root
    //It is only a placeholder now: the file is created when SetLogDirectory runs, so that a tool like ncm
    //launching the server cannot end up with the logs in the tool's directory
    private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    //Both sinks default to Info only; detailed logging is enabled by the kernel via --debug calling SetConsoleLevel/SetFileLevel
    //The old version defaulted to Debug, flooding debug logs without any flag, wasting CPU on console IO and formatting
    private static LogLevel _consoleLevel = LogLevel.Info;
    private static LogLevel _fileLevel = LogLevel.Info;
    private static StreamWriter _fileWriter = null!;
    private static readonly object _lock = new();
    private static readonly object _consoleLock = new();

    //Async log buffer: the calling thread only packs raw fields into the queue, no string building and no IO
    //_pending grows dynamically; the output thread swaps it out entirely under the lock then formats outside the lock to reduce contention on both sides
    private static readonly object _bufferLock = new();
    private static List<LogEntry> _pending = new();
    //_signal is released once per enqueue; the output thread sleeps and wakes on it instead of spinning
    private static readonly SemaphoreSlim _signal = new(0);
    private static Thread? _outputThread;
    private static volatile bool _shutdown;

    //LogEntry one log entry pending output
    //Formatting is left entirely to the output thread; the calling thread only grabs time and origin, which must be taken on the spot or they mix up across threads
    //Barrier non-null means this is a barrier; when the output thread reaches it, all prior logs have been written out and flushed
    //RawConsole/RawFile non-null means skip formatting and output directly, used for the log system's own messages
    private readonly record struct LogEntry(
        LogLevel Level,
        string Message,
        string FilePath,
        int LineNumber,
        string MemberName,
        string Source,
        string Timestamp,
        Action? Barrier,
        string? RawConsole,
        string? RawFile);

    private static LogEntry RawEntry(string consoleLine, string fileLine) => new(
        LogLevel.Info, string.Empty, string.Empty, 0, string.Empty, string.Empty,
        string.Empty, null, consoleLine, fileLine);

    public static event Action<string>? OnLogOutput;

    //HistoryCapacity history cache entry count
    //The GUI starts later than the log system, so logs emitted before subscribing can only be reviewed from this cache
    //With debug mode on, startup easily produces tens of thousands of entries; the old 2000 cap was pushed out in an instant, so users scrolled to the top and never saw the server-startup part
    private const int HistoryCapacity = 20000;

    //History cache stores the text fed to OnLogOutput
    //The GUI starts later than the log system, so logs emitted before subscribing can only be reviewed from this cache; consistent formatting makes them stitch together seamlessly
    private static readonly object _historyLock = new();
    private static readonly string[] _history = new string[HistoryCapacity];
    private static long _historyCount;

    //RecordHistory records a line into the ring cache, called only on the output thread
    private static void RecordHistory(string line)
    {
        lock (_historyLock)
        {
            _history[(int)(_historyCount % HistoryCapacity)] = line;
            _historyCount++;
        }
    }

    //GetHistory takes a snapshot of history logs, oldest to newest
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

    //ClearHistory clears the history cache, used once for review and released after
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

    //SimpleFormat concise format, like 2026-09-30 17:06:09[INFO] - message
    private static readonly string SimpleFormat = $"{LogPlaceholders.Timestamp}[{LogPlaceholders.Level}] - {LogPlaceholders.Message}";

    //DetailFormat detailed format used by --debug, includes file and line, like 2026-09-30 17:06:09.123[DBG][xxx.cs:xx] - message
    private static readonly string DetailFormat = $"{LogPlaceholders.Timestamp}[{LogPlaceholders.Level}][{LogPlaceholders.File}:{LogPlaceholders.Line}] - {LogPlaceholders.Message}";

    //DefaultFormat default format string, placeholders use constants directly to keep both sides in sync
    private static readonly string DefaultFormat = SimpleFormat;

    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private const string DetailTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private static string _consoleFormat = DefaultFormat;
    private static string _fileFormat = DefaultFormat;
    private static string _timestampFormat = TimestampFormat;
    //Precompiled format template, rebuilt when the format string changes; the output thread concatenates each log directly from fragments
    private static CompiledFormat _consoleCompiled = new(DefaultFormat);
    private static CompiledFormat _fileCompiled = new(DefaultFormat);

    //LevelNames level names indexed by enum value; the output thread originally ran Enum.ToString plus ToUpper for every log
    private static readonly string[] LevelNames = { "DBG", "INFO", "WARN", "ERROR", "CRIT", "NONE" };

    //Buffer entry cap: when production outpaces output, logs must be dropped or _pending grows until OutOfMemory
    //Measured: --debug full logging reaches 160k entries/sec, console output cannot keep up and no amount of memory can hold it
    //Three-tier shedding: soft cap drops Debug/Info, hard cap drops Warning too, and at the top Error is dropped as well; keeping the process alive wins
    private const int PendingSoftLimit = 65536;
    private const int PendingHardLimit = 131072;
    private const int PendingMaxLimit = 262144;

    //MaxMessageLength per-message character cap, truncated when exceeded
    //Large objects like chunk data can ToString into tens of KB per entry; truncation saves real memory in the buffer
    private const int MaxMessageLength = 4096;

    //_droppedCounts accumulates dropped counts per level; the output thread summarizes them into one Error note then resets
    private static readonly long[] _droppedCounts = new long[LevelNames.Length];

    private static int _retentionDays = 30;

    //Archiving runs once per process; SetLogDirectory may be called more than once and only the first real session counts
    private static bool _archived;

    static Log()
    {
        EnableVirtualTerminalSupport();
        StartOutputThread();
        //No file is opened here: the program root is not settled yet, AppPaths reports it later through SetLogDirectory
        //Everything logged before that still reaches the console and the history cache
        //Drains the buffer and flushes before process exit, otherwise the last few logs of the crash scene stay in memory and are lost
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
    }

    public static void SetRetentionDays(int days)
    {
        if (days < 0) days = 0;
        _retentionDays = days;
    }

    public static int GetRetentionDays() => _retentionDays;

    //ArchiveOldLogs packs what earlier sessions left behind, before this session opens its own file
    //Failure is swallowed: a missing compression library or a full disk must not take the file sink down with it
    //The count is reported either way, a silent zero is what made the earlier failure invisible
    private static void ArchiveOldLogs()
    {
        if (_archived) return;
        _archived = true;
        try
        {
            var packed = LogArchive.Archive(_logDirectory);
            if (packed > 0)
                WriteCleanupLog($"[Log Archive] Packed {packed} old log file(s) into {LogArchive.Extension}");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[log] archiving old logs failed: {e.Message}");
        }
    }

    private static void CleanupOldLogs()
    {
        if (_retentionDays <= 0 || string.IsNullOrEmpty(_logDirectory))
            return;
        try
        {
            if (!Directory.Exists(_logDirectory))
                return;
            //Archives age out under the same rule as raw logs, otherwise they would accumulate forever
            var logFiles = Directory.GetFiles(_logDirectory, "*.log")
                .Concat(Directory.GetFiles(_logDirectory, LogArchive.SearchPattern))
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

    //WriteCleanupLog the log system's own cleanup note, goes through the same output queue so it does not interleave with other logs
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

    //SetVerbose verbose mode switch, driven by --debug
    //When on, timestamps include milliseconds and the format adds file and line; when off, lines are as short as possible
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
            var resolved = string.IsNullOrEmpty(directory)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")
                : directory;
            //An unchanged directory keeps the current writer; re-setting it would split one session across two files
            if (_fileWriter != null && string.Equals(_logDirectory, resolved, StringComparison.OrdinalIgnoreCase))
                return;
            CloseFileWriter();
            _logDirectory = resolved;
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
            ArchiveOldLogs();
            CleanupOldLogs();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var logFile = Path.Combine(_logDirectory, $"{timestamp}.log");
            _fileWriter = new StreamWriter(logFile, true, System.Text.Encoding.UTF8);
            //Does not flush per entry; the output thread flushes after a batch or on Error-level logs, since an fsync per entry would stall the producing thread
            _fileWriter.AutoFlush = false;
        }
        catch (Exception e)
        {
            //A silent disable here looks exactly like "the log file is empty"; the reason has to be visible
            Console.Error.WriteLine($"[log] file sink disabled: {e}");
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

    //Static separator table; previously every log allocated a char array for LastIndexOfAny, a pointless allocation
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

    //LevelName gets the level name via lookup to avoid Enum.ToString plus ToUpper per log
    private static string LevelName(LogLevel level)
    {
        var index = (int)level;
        return index >= 0 && index < LevelNames.Length ? LevelNames[index] : level.ToString();
    }

    //CompiledFormat precompiled log format template
    //Previously FormatLogMessage did ten string.Replace per log, each scanning the whole string and allocating a new one
    //Here it splits into literal/slot sequences by placeholder at construction, then only Appends into a StringBuilder at output
    private sealed class CompiledFormat
    {
        //Slot the value slot for a placeholder
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
                //Unrecognized placeholders are preserved as-is, so a bad format string is directly visible in the output
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

        //Append appends one log into the buffer per the template
        //levelColor when non-null only the level tag is colored, the rest keeps the default color, so only tags like [INFO] are colored
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

    //WriteLog records one log, the calling thread only counts and enqueues
    //Time and origin must be taken on the spot: the origin is on the current thread's origin stack, and the time should be when it happened not when it was written
    //All string concatenation and IO is left to the output thread; hot paths like worldgen and network are no longer dragged down by logging
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
        //If neither sink accepts it the whole entry is dropped, without even taking origin and time
        if (!toConsole && !toFile) return;

        //Overlong messages are truncated before enqueue; the buffer stores the truncated short string, which is exactly the memory saved
        if (message.Length > MaxMessageLength)
            message = string.Concat(message.AsSpan(0, MaxMessageLength), "...(truncated)");

        Enqueue(new LogEntry(level, message, filePath, lineNumber, memberName, GetCurrentSource(),
            DateTime.Now.ToString(_timestampFormat), null, null, null));
    }

    //SetMainThread designates the server main thread
    //Only main-thread log positions indicate how far world advancement has gone; network and IO thread logs are frequent and would overwrite the position
    public static void SetMainThread(int threadId) => _mainThreadId = threadId;

    //LastOriginFile/Line/Member/Source call site of the most recent log actually written by the main thread
    //Printed when the watchdog reports Can't keep up, to confirm the last line the main thread reached before and after the stall
    private static int _mainThreadId;
    private static string _lastOriginFile = "";
    private static int _lastOriginLine;
    private static string _lastOriginMember = "";
    private static string _lastOriginSource = "";

    public static string LastOriginFile => _lastOriginFile;
    public static int LastOriginLine => _lastOriginLine;
    public static string LastOriginMember => _lastOriginMember;
    public static string LastOriginSource => _lastOriginSource;

    //Enqueue enqueues and wakes the output thread
    //Drops logs when the buffer hits its cap: when production outpaces output, not dropping means OutOfMemory
    //Sheds by level: Debug/Info first, Warning next, Error/Critical last, so a log flood does not take the crash scene down with it
    private static void Enqueue(LogEntry entry)
    {
        if (_shutdown) return;
        //Only records main-thread entries with body text; barriers and raw output carry no call site, and other threads' logs should not overwrite the main-thread position
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

    //ReportDrops summarizes accumulated drops into one Error note
    //Must produce output directly and not re-enqueue, or this note would be dropped when the buffer is full
    private static void ReportDrops()
    {
        List<string>? parts = null;
        for (var i = 0; i < _droppedCounts.Length; i++)
        {
            var dropped = Interlocked.Exchange(ref _droppedCounts[i], 0);
            if (dropped <= 0) continue;
            parts ??= new List<string>();
            parts.Add($"{dropped} x {LevelNames[i]}");
        }
        if (parts is null) return;

        var message = $"Log buffer full, dropped to prevent crash {string.Join(", ", parts)}";
        //Aligns with WriteLog's error statistics; this note itself counts toward the error count and last error message
        Interlocked.Increment(ref _errorCount);
        _lastErrorMessage = message;
        ProcessBatch(new List<LogEntry>
        {
            new(LogLevel.Error, message, "Log.cs", 0, nameof(ReportDrops), "NetCraft.Logging.Log",
                DateTime.Now.ToString(_timestampFormat), null, null, null)
        });
    }

    //StartOutputThread starts the single output thread, a background thread that does not block process exit
    private static void StartOutputThread()
    {
        _outputThread = new Thread(OutputLoop)
        {
            Name = "NetCraft-Log",
            IsBackground = true,
        };
        _outputThread.Start();
    }

    //OutputLoop the output thread body, repeatedly swapping out the whole buffer then processing it
    //The swap happens once under the lock, formatting and IO are outside the lock, so producers are almost never blocked
    private static void OutputLoop()
    {
        var draining = new List<LogEntry>();
        while (true)
        {
            _signal.Wait();
            //Clears excess signals, processes all buffer entries in one round
            while (_signal.Wait(0)) { }
            lock (_bufferLock)
            {
                //Swapped with an empty list, the old list keeps loading the next round with capacity kept at peak; this is the dynamically growing buffer
                (_pending, draining) = (draining, _pending);
            }
            if (draining.Count > 0)
            {
                ProcessBatch(draining);
                draining.Clear();
            }
            //The drop note is reported after this round's backlog is processed, so it is not buried by its own preceding logs
            ReportDrops();
            if (_shutdown) return;
        }
    }

    //ProcessBatch processes a batch of logs: the console merges the whole batch into one write, while file lines are still handed to the StreamWriter buffer one by one
    //Every console write flushes the underlying stream, so per-entry writing equals per-entry fsync, the most expensive part of logging
    private static void ProcessBatch(List<LogEntry> batch)
    {
        var consoleBatch = new StringBuilder();
        var single = new StringBuilder(192);
        foreach (var entry in batch)
        {
            //Barrier: flush all preceding entries before releasing waiters, guaranteeing that logs are really on disk when Flush() returns
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
            //Both the filename and level name sinks need it, computed only once here
            var fileName = ExtractFileName(entry.FilePath);
            var levelName = LevelName(entry.Level);
            if (_consoleLevel != LogLevel.None && entry.Level >= _consoleLevel)
            {
                single.Clear();
                //Colors only the level tag; wrapping the whole line would color the body too, making long logs noisy
                _consoleCompiled.Append(single, entry, fileName, levelName, GetAnsiColor(entry.Level));
                AppendConsole(consoleBatch, single);
            }
            if (_fileLevel == LogLevel.None || entry.Level < _fileLevel) continue;
            single.Clear();
            _fileCompiled.Append(single, entry, fileName, levelName, string.Empty);
            WriteFileLine(single.ToString());
        }
        FlushConsole(consoleBatch);
        //Every batch is flushed, not only the ones carrying an Error
        //FileStream.Flush merely hands the managed buffer to the OS, it is not an fsync, so the cost is negligible
        //The old Error-only rule left a freshly started server with a zero-byte log file, which is useless for live debugging
        FlushFileWriter();
    }

    //AppendConsole merges a line into the batch buffer, subscribers still receive text per entry
    private static void AppendConsole(StringBuilder batch, string line)
    {
        RecordHistory(line);
        OnLogOutput?.Invoke(line);
        batch.Append(line).Append('\n');
    }

    private static void AppendConsole(StringBuilder batch, StringBuilder line)
    {
        //History and subscribers share the same text, converted only once
        var text = line.ToString();
        RecordHistory(text);
        if (OnLogOutput is not null) OnLogOutput(text);
        batch.Append(line).Append('\n');
    }

    //FlushConsole writes the whole batch at once, only here touches the console
    //A write failure permanently disables the console sink; the output thread must not lose all subsequent logs to a single IO failure
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

    //WriteFileLine writes a line to the log file, called only on the output thread
    private static void WriteFileLine(string line)
    {
        if (!_enableFileLogging || _fileWriter is null) return;
        lock (_lock)
        {
            try
            {
                _fileWriter.WriteLine(line);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[log] file write failed: {e}");
                _enableFileLogging = false;
                CloseFileWriter();
            }
        }
    }

    //FlushFileWriter the sole sink writing logs to disk, triggered in batches by the output thread
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

    //Flush blocks until all logs enqueued before this point are written out and flushed
    //Called before exit and in scenarios that assert on logs, not needed normally
    //With a timeout: if the output thread gets stuck on console IO, the shutdown flow must not be held forever
    public static void Flush()
    {
        if (_shutdown || _outputThread is null) return;
        using var done = new ManualResetEventSlim(false);
        Enqueue(new LogEntry(LogLevel.Info, string.Empty, string.Empty, 0, string.Empty, string.Empty,
            string.Empty, done.Set, null, null));
        done.Wait(TimeSpan.FromSeconds(2));
    }

    //Shutdown drains the buffer, stops the output thread and closes files, idempotent
    public static void Shutdown()
    {
        if (_shutdown) return;
        Flush();
        _shutdown = true;
        _signal.Release();
        _outputThread?.Join(TimeSpan.FromSeconds(2));
        lock (_lock) CloseFileWriter();
    }

    //DebugLogHandler deferred construction parameter for Debug logs
    //Previously Log.Debug($"...") built the interpolated string before passing it in; that string was wasted when the level was off
    //Throwaway allocations dominate hot paths like per-cell sampling and per-tick queries; now nothing is built at all when the level is off
    [InterpolatedStringHandler]
    public ref struct DebugLogHandler
    {
        private readonly StringBuilder? _builder;

        public DebugLogHandler(int literalLength, int formattedCount)
            => _builder = DebugEnabled ? new StringBuilder(literalLength + formattedCount * 8) : null;

        //Enabled whether a string really needs to be produced, short-circuits downstream when off
        public readonly bool Enabled => _builder is not null;

        public void AppendLiteral(string value) => _builder?.Append(value);

        public void AppendFormatted<T>(T value) => _builder?.Append(value);

        public void AppendFormatted(string? value) => _builder?.Append(value);

        public void AppendFormatted(ReadOnlySpan<char> value) => _builder?.Append(value);

        public override readonly string ToString() => _builder?.ToString() ?? string.Empty;
    }

    //DebugEnabled whether Debug will be accepted by any sink
    public static bool DebugEnabled
        => (_consoleLevel != LogLevel.None && LogLevel.Debug >= _consoleLevel)
            || (_enableFileLogging && _fileWriter != null && _fileLevel != LogLevel.None
                && LogLevel.Debug >= _fileLevel);

    public static void Debug(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
        => WriteLog(LogLevel.Debug, message, filePath, lineNumber, memberName);

    //Debug interpolation overload; when the level is off the handler builds no buffer and all Appends become no-ops
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