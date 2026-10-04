using System;
using System.Collections.Generic;
using System.Text;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//LogStore 进程内的一份日志缓冲 主页与日志页共用同一个源
//原先各面板自己订阅 Log.OnLogOutput 再各自去取 GetHistory 那份一次性缓存
//那份缓存取一次就清 谁先构造谁拿到 后构造的页面整个启动阶段都是空的
//所以把订阅与缓存收到这里 面板只从这拿快照与增量
public sealed class LogStore
{
    //缓存条数 与 Log 自己的历史缓存对齐 比面板显示上限略多一点余量
    private const int Capacity = 20000;

    //Entry 一条日志 文本与级别一起存
    //过滤要按级别筛 只存文本的话每个面板都要再解析一遍
    public readonly record struct Entry(string Text, LogLevel Level);

    //Escape 着色序列的起始字符 认级别时要把整段序列跳过
    private const char Escape = '\u001B';

    private readonly object _lock = new();
    private readonly Queue<Entry> _lines = new();

    //LineAdded 新日志 在输出线程上触发 订阅方自己往 UI 线程切
    public event Action<Entry>? LineAdded;

    //Attach 先补看订阅之前的历史再订阅 顺序与原先面板里的一致
    public void Attach()
    {
        foreach (var line in Log.GetHistory()) Append(line);
        Log.ClearHistory();
        Log.OnLogOutput += Append;
    }

    //Detach 解订阅 窗口关闭时调 否则这里会被日志系统一直引用着
    public void Detach() => Log.OnLogOutput -= Append;

    //Snapshot 当前全部条目 按时间从旧到新
    public Entry[] Snapshot()
    {
        lock (_lock) return _lines.ToArray();
    }

    //Append 入缓冲并通知 超出上限从头部丢
    private void Append(string line)
    {
        var entry = new Entry(line, ParseLevel(line));
        lock (_lock)
        {
            if (_lines.Count >= Capacity) _lines.Dequeue();
            _lines.Enqueue(entry);
        }
        LineAdded?.Invoke(entry);
    }

    //ParseLevel 取行里的级别
    //不能只看行首那个方括号: 喂过来的文本带着 ANSI 着色码 行首其实是 ESC[33m 这种
    //那样取到的会是 33m 全部行都会被当成 Info
    //所以按整块级别标记去找 取位置最靠前的那个 消息体里就算出现同样的字面量也排不到标记前头
    private static readonly (string Tag, LogLevel Level)[] LevelTags =
    {
        ("[DBG]", LogLevel.Debug),
        ("[INFO]", LogLevel.Info),
        ("[WARN]", LogLevel.Warning),
        ("[ERROR]", LogLevel.Error),
        ("[CRIT]", LogLevel.Critical),
    };

    //ParseLevel 认级别 找之前必须先把着色码剥掉
    //控制台行只给等级标记上色 颜色码是插在级别名之前的 实际文本形如 [ESC[33mWARN ESC[0m]
    //也就是方括号被颜色码拆开了 直接找 "[WARN]" 一个都匹配不上 整屏都会落到默认的 Info
    private static LogLevel ParseLevel(string line)
    {
        var plain = StripAnsi(line);
        var best = -1;
        var level = LogLevel.Info;
        foreach (var (tag, value) in LevelTags)
        {
            var at = plain.IndexOf(tag, StringComparison.Ordinal);
            if (at < 0 || (best >= 0 && at >= best)) continue;
            best = at;
            level = value;
        }
        return level;
    }

    //StripAnsi 去掉 SGR 着色序列 行里没有着色码就原样返回 不额外分配
    private static string StripAnsi(string line)
    {
        if (line.IndexOf(Escape) < 0) return line;
        var text = new StringBuilder(line.Length);
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == Escape && i + 1 < line.Length && line[i + 1] == '[')
            {
                var end = line.IndexOf('m', i + 2);
                if (end < 0) break;
                i = end;
                continue;
            }
            text.Append(line[i]);
        }
        return text.ToString();
    }
}
