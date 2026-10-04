using System.IO;
using System.Threading;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Logging;

namespace NetCraft.Server.ServerConsole;

//ReplConsole 服务端命令行
//交互时逐键读 行内编辑 历史 补全 写日志前先把提示符行腾出来
//输入或输出被重定向时降级成只读行 输出原样直写 一个控制序列都不发
//提交的命令走服务端控制台命令源 与 GUI 控制台那条路同一个入口
public sealed class ReplConsole : IDisposable
{
    private const string Prompt = "> ";

    //清掉光标所在整行并把光标送回行首
    private const string ClearLine = "\r\u001B[2K";

    //HistoryCapacity 历史保留上限 与 InputLine 的内存上限对齐
    private const int HistoryCapacity = 1000;

    //反向搜索提示符 命中与未命中两种写法 与 readline 一致
    private const string SearchPrompt = "(reverse-i-search)`";
    private const string FailedSearchPrompt = "(failed reverse-i-search)`";
    //查询串与命中文本之间那一段
    private const string SearchTail = "': ";

    private readonly object _lock = new();
    private readonly InputLine _input = new();
    private readonly DedicatedServer _server;
    private readonly TextWriter _output;
    private readonly bool _interactive;
    private readonly ICompletionSource? _completion;
    private readonly ICommandHighlighter? _highlighter;

    private Thread? _thread;
    private volatile bool _running;

    //_candidates 当前留在提示符上方的补全候选 为 null 表示没有
    //留着状态是为了重绘 日志到来时把候选一起画回去 不然刚列出来就被冲掉
    private IReadOnlyList<string>? _candidates;
    //_candidateRows 候选区占了几行 擦除时按它上移 行数是本类自己渲染出来的 不必估终端宽度
    private int _candidateRows;
    //_drawn 提示符行是否已经画在屏幕上 空输入的空重绘靠它跳过 敲多少下回车都不往屏幕上添东西
    private bool _drawn;

    //_searching 是否停在 Ctrl+R 反向搜索里 搜索期间按键走另一套分派
    private bool _searching;
    private string _searchQuery = string.Empty;
    //_searchIndex 搜索游标 从历史末尾起往前退 指向下一条要比对的位置
    private int _searchIndex;
    //_searchSavedText 进入搜索前的那行输入 按 Esc 还回去
    private string _searchSavedText = string.Empty;
    //_searchMissed 上一次查找没命中 提示符上标出来 与 readline 一样
    private bool _searchMissed;

    private ReplConsole(DedicatedServer server, TextWriter output, bool interactive, ICompletionSource? completion,
        ICommandHighlighter? highlighter)
    {
        _server = server;
        _output = output;
        _interactive = interactive;
        _completion = completion;
        _highlighter = highlighter;
    }

    //Start 接管服务端控制台
    //交互模式下先接输出再接输入 反过来的话启动期日志会把提示符冲掉
    //两处都被重定向时不接输出 管道里的内容应当保持原样
    public static ReplConsole Start(DedicatedServer server)
    {
        var interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
        var repl = new ReplConsole(
            server,
            Console.Out,
            interactive,
            interactive ? new CommandCompletionSource(server) : null,
            interactive ? new CommandHighlighter(server) : null);

        if (interactive)
        {
            //交互模式才载入历史与接输出 重定向那一支输出原样透传 历史也用不上
            repl.LoadHistory();
            Console.SetOut(new ConsoleLineWriter(repl));
        }

        repl._running = true;
        repl._thread = new Thread(interactive ? repl.RunInteractive : repl.RunRedirected)
        {
            Name = "NetCraft-Console",
            //后台线程 服务端主循环退出了不必等它
            IsBackground = true,
        };
        repl._thread.Start();
        return repl;
    }

    //HistoryPath 历史落盘位置 放程序根目录 对应 Paper 控制台的 console_history
    private static string HistoryPath => Path.Combine(AppPaths.BaseDirectory, "console_history");

    //LoadHistory 载入落盘的会话历史并接上后续落盘
    //历史文件读不动也不该挡住命令行 任何异常都吞掉 当作没有历史
    private void LoadHistory()
    {
        _input.HistoryAppended = SaveHistoryLine;
        try
        {
            if (!File.Exists(HistoryPath)) return;

            var lines = File.ReadAllLines(HistoryPath);
            _input.LoadHistory(lines);
            //文件比内存上限还长就顺手收一次 免得越攒越大
            if (lines.Length > HistoryCapacity) File.WriteAllLines(HistoryPath, _input.History);
        }
        catch (Exception e)
        {
            Log.Warning($"Console history load failed: {e.Message}");
        }
    }

    //SaveHistoryLine 每提交一条就追加落盘 下次启动还能翻出来
    //写失败只记一条警告 已经跑出去的命令不该被它带偏
    private void SaveHistoryLine(string line)
    {
        try
        {
            File.AppendAllText(HistoryPath, line + Environment.NewLine);
        }
        catch (Exception e)
        {
            Log.Warning($"Console history save failed: {e.Message}");
        }
    }

    //WriteLine 内核日志的出口
    //交互时先把提示符行腾空再写 写完把提示符画回去 日志折行交给终端滚
    public void WriteLine(string text)
    {
        if (!_interactive)
        {
            _output.Write(text);
            _output.Write('\n');
            _output.Flush();
            return;
        }

        lock (_lock)
        {
            //日志会插在候选区与提示符之间 先擦掉候选区 写完再连同候选一起画回来
            EraseCandidates();
            _output.Write(ClearLine);
            _output.Write(text);
            _output.Write('\n');
            //日志把提示符挤掉了 这一次必须重画
            Render(force: true);
        }
    }

    public void Dispose()
    {
        //读键线程可能正阻塞在 ReadKey 上 它是后台线程 不等它退出
        _running = false;
    }

    //RunInteractive 输入是终端时的主路径
    private void RunInteractive()
    {
        lock (_lock) Render();

        while (_running)
        {
            if (!Console.KeyAvailable)
            {
                //15ms 一跳 按键延迟感觉不出来 也不至于空转烧核
                Thread.Sleep(15);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            lock (_lock) HandleKey(key);
        }
    }

    //RunRedirected 输入不是终端时的降级路径
    //一行一条命令 读到 null 说明管道那头关了 线程自己收工
    private void RunRedirected()
    {
        while (_running)
        {
            string? line;
            try
            {
                line = Console.ReadLine();
            }
            catch (IOException)
            {
                return;
            }

            if (line is null) return;
            Execute(line);
        }
    }

    //HandleKey 按键分派
    private void HandleKey(ConsoleKeyInfo key)
    {
        //搜索态另有一套键位 从字面输入切到查找串 这里整支截走
        if (_searching)
        {
            HandleSearchKey(key);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                Submit();
                return;
            case ConsoleKey.Tab:
                Complete();
                return;
            case ConsoleKey.Backspace:
                _input.Backspace();
                break;
            case ConsoleKey.Delete:
                _input.Delete();
                break;
            case ConsoleKey.LeftArrow:
                _input.MoveLeft();
                break;
            case ConsoleKey.RightArrow:
                _input.MoveRight();
                break;
            case ConsoleKey.Home:
                _input.MoveHome();
                break;
            case ConsoleKey.End:
                _input.MoveEnd();
                break;
            case ConsoleKey.UpArrow:
                _input.HistoryUp();
                break;
            case ConsoleKey.DownArrow:
                _input.HistoryDown();
                break;
            case ConsoleKey.Escape:
                //与 readline 一致 丢掉当前这一行
                _input.Clear();
                break;
            case ConsoleKey.L when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                //清屏后把输入与提示符重新画一遍 屏幕上的候选行已随清屏消失 状态也要跟着清
                _output.Write("\u001B[2J\u001B[H");
                _candidates = null;
                _candidateRows = 0;
                _drawn = false;
                Render();
                return;
            case ConsoleKey.R when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                //Ctrl+R 进反向搜索 与 readline 一个路子
                BeginSearch();
                Render(force: true);
                return;
            default:
                //控制字符与功能键不进输入
                if (key.KeyChar < ' ' || key.KeyChar == '\u007F') return;
                _input.Insert(key.KeyChar);
                break;
        }

        //输入变了 上一次的补全结果已经过期
        _candidates = null;
        Render();
    }

    //BeginSearch 进入 Ctrl+R 反向搜索 记下当前这行 按 Esc 时还回去
    private void BeginSearch()
    {
        _searching = true;
        _searchQuery = string.Empty;
        //游标停在历史末尾之后 按一次 Ctrl+R 正好从最新一条起查
        _searchIndex = _input.History.Count;
        _searchSavedText = _input.Text;
        _searchMissed = false;
        //上一轮留下的候选跟搜索无关 让 Render 顺手清掉
        _candidates = null;
    }

    //HandleSearchKey 搜索态按键 查询串走 _searchQuery 输入行只承载命中项
    private void HandleSearchKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.R && key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            //再按一次 Ctrl+R 从当前命中往前接着找
            SearchBackward();
            Render(force: true);
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                //接受命中并直接提交 与 readline 的 accept-line 一致
                _searching = false;
                ResetSearch();
                Submit();
                return;
            case ConsoleKey.Escape:
                //放弃搜索 把进搜索前那行还回输入
                _searching = false;
                _input.SetText(_searchSavedText);
                ResetSearch();
                Render(force: true);
                return;
            case ConsoleKey.Backspace:
                if (_searchQuery.Length > 0) _searchQuery = _searchQuery[..^1];
                //查询串变了 从末尾重查一遍
                SearchBackward(reset: true);
                Render(force: true);
                return;
            default:
                if (key.KeyChar < ' ' || key.KeyChar == '\u007F')
                {
                    //其它控制键就地收掉搜索 命中项留在输入上接着编辑
                    _searching = false;
                    ResetSearch();
                    Render(force: true);
                    return;
                }
                _searchQuery += key.KeyChar;
                SearchBackward(reset: true);
                Render(force: true);
                return;
        }
    }

    //SearchBackward 从历史里往前找第一条含查询串的
    //reset 为真时从最新一条重查 否则从当前游标再往前退一步 对应再按一次 Ctrl+R
    private void SearchBackward(bool reset = false)
    {
        var history = _input.History;
        //查询串还是空的时候就停在原地 不算未命中
        if (_searchQuery.Length == 0 || history.Count == 0)
        {
            _searchMissed = false;
            return;
        }

        var index = reset ? history.Count - 1 : _searchIndex - 1;
        while (index >= 0)
        {
            if (history[index].Contains(_searchQuery, StringComparison.Ordinal))
            {
                _searchIndex = index;
                _input.SetText(history[index]);
                _searchMissed = false;
                return;
            }
            index--;
        }

        //到头没找着 上一次命中留在输入上 只把提示符标成未命中
        _searchMissed = true;
    }

    //ResetSearch 收掉搜索态的全部中间状态
    private void ResetSearch()
    {
        _searchQuery = string.Empty;
        _searchIndex = 0;
        _searchSavedText = string.Empty;
        _searchMissed = false;
    }

    //Submit 提交当前行
    private void Submit()
    {
        var line = _input.Commit();
        //提交时收掉候选区 回执要紧接着这一行 中间不该还夹着上一次的候选
        _candidates = null;

        //空行只是敲了一下回车 屏幕上不该多留一行 把提示符原样画回去就行
        if (line is null)
        {
            Render();
            return;
        }

        EraseCandidates();
        //提交的这一刻把这一行留在屏幕上 后面的回执才接得上
        _output.Write(ClearLine);
        _output.Write(Prompt);
        _output.Write(line);
        _output.Write('\n');

        Execute(line);
        //提交行连同回执都写在新行上 这一次必须重画
        Render(force: true);
    }

    //Complete 行内补全 与 readline 一个套路
    //唯一候选直接补上 多个候选先补到公共前缀 补不动了才把候选列出来
    private void Complete()
    {
        if (_completion is null) return;

        var text = _input.Text;
        IReadOnlyList<string> candidates;
        try
        {
            candidates = _completion.GetCompletions(text, _input.Caret);
        }
        catch (Exception ex)
        {
            //补全用的解析器会见什么解析什么 不能让它把控制台线程带走
            WriteLine($"Completion failed: {ex.GetType().Name} {ex.Message}");
            return;
        }

        if (candidates.Count == 0) return;

        if (candidates.Count == 1)
        {
            _input.SetText(candidates[0]);
            _candidates = null;
            Render();
            return;
        }

        var prefix = CommonPrefix(candidates);
        if (prefix.Length > text.Length)
        {
            _input.SetText(prefix);
            _candidates = null;
            Render();
            return;
        }

        //多候选又补不到公共前缀 就留在提示符上方等下一次编辑才收
        _candidates = candidates;
        Render();
    }

    //Execute 投递一行命令 由主循环取出执行
    //带上斜杠也认 原版终端两种写法都收
    //不在控制台线程直接跑 世界状态与主循环交叉会出问题 异常兜底也归主循环那边
    private void Execute(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('/')) text = text[1..];
        if (text.Length == 0) return;
        _server.EnqueueConsoleCommand(ServerCommandSource.Console(_server), text);
    }

    //Render 重画候选区与提示符行 光标最后落在输入位置
    //候选留在提示符上方 日志一来擦掉重画 于是新的日志不会把候选冲走
    //force 为假时 屏幕上已经是提示符本身且没别的状态就直接跳过 一个字符都不写
    //空输入敲回车走的就是这条 屏幕上看不出任何动静
    private void Render(bool force = false)
    {
        if (!force && _drawn && _candidateRows == 0 && _candidates is null && _input.Text.Length == 0) return;

        EraseCandidates();
        _output.Write(ClearLine);
        if (_candidates is { Count: > 0 } candidates)
        {
            //候选一行一个 与游戏里按 Tab 出来的补全列表排法一致
            //一行一个还保证了擦除准: 行长远小于终端宽度 终端不会自己折行 记几行就是几行
            foreach (var candidate in candidates)
            {
                _output.Write(candidate);
                _output.Write('\n');
                _candidateRows++;
            }
        }

        if (_searching)
        {
            //搜索态提示符换成 readline 那一行 光标停在查找串末尾而不是命中行末尾
            _output.Write(_searchMissed ? FailedSearchPrompt : SearchPrompt);
            _output.Write(_searchQuery);
            _output.Write(SearchTail);
            _output.Write(_input.Text);
            var tail = _input.Text.Length + SearchTail.Length;
            if (tail > 0) _output.Write($"\u001B[{tail}D");
        }
        else
        {
            _output.Write(Prompt);
            //命令树认得的部分按节点上色 高亮只往字缝里插转义 可见长度不变
            _output.Write(_highlighter?.Highlight(_input.Text) ?? _input.Text);
            //画完把光标左移到输入位置 ANSI 的 D 是左移 n 列
            var back = _input.Text.Length - _input.Caret;
            if (back > 0) _output.Write($"\u001B[{back}D");
        }

        _drawn = true;
        _output.Flush();
    }

    //EraseCandidates 把候选区连同提示符行一起从屏幕上抹掉 光标停在原来候选首行
    //用"从光标清到屏幕尾"而不是逐行清空: 逐行清只把字抹掉 那几行还占着屏幕 收起来就留一片空行
    //提示符行永远在屏幕最底部 光标下方不会有别的内容 清到屏幕尾是安全的
    //行数是本类自己渲染时记下的 上移这么多行正好落在候选首行
    private void EraseCandidates()
    {
        if (_candidateRows == 0) return;
        _output.Write($"\u001B[{_candidateRows}A");
        _output.Write("\r\u001B[J");
        _candidateRows = 0;
    }

    //CommonPrefix 取所有候选的公共前缀 多候选时先补到分歧点
    private static string CommonPrefix(IReadOnlyList<string> values)
    {
        var prefix = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            var other = values[i];
            var length = 0;
            while (length < prefix.Length && length < other.Length &&
                   char.ToLowerInvariant(prefix[length]) == char.ToLowerInvariant(other[length]))
            {
                length++;
            }

            prefix = prefix[..length];
            if (prefix.Length == 0) break;
        }

        return prefix;
    }
}
