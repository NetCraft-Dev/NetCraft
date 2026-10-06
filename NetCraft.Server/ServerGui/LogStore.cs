using System;
using System.Collections.Generic;
using System.Text;
using NetCraft.Logging;

namespace NetCraft.Server.Gui;

//LogStore, an in-process log buffer, shared by the main page and the log page as one source
//Previously each panel subscribed to Log.OnLogOutput itself and then each fetched the one-shot GetHistory cache
//That cache clears on first read, whoever constructs first gets it, and pages constructed later stay empty for the whole startup phase
//So the subscription and cache are collected here, panels only take snapshots and increments from here
public sealed class LogStore
{
    //Cache size, aligned with Log's own history cache and a little above the panel display cap for headroom
    private const int Capacity = 20000;

    //Entry, one log line, text and level stored together
    //Filtering selects by level, storing only text would make every panel parse it again
    public readonly record struct Entry(string Text, LogLevel Level);

    //Escape, the start character of color sequences, the whole sequence must be skipped when detecting the level
    private const char Escape = '\u001B';

    private readonly object _lock = new();
    private readonly Queue<Entry> _lines = new();

    //LineAdded fires on the output thread for a new log line, subscribers switch to the UI thread themselves
    public event Action<Entry>? LineAdded;

    //Attach first catches up on history before subscribing, same order as in the original panel
    public void Attach()
    {
        foreach (var line in Log.GetHistory()) Append(line);
        Log.ClearHistory();
        Log.OnLogOutput += Append;
    }

    //Detach unsubscribes, called when the window closes, otherwise the logging system keeps holding a reference here
    public void Detach() => Log.OnLogOutput -= Append;

    //Snapshot all current entries, oldest to newest
    public Entry[] Snapshot()
    {
        lock (_lock) return _lines.ToArray();
    }

    //Append adds to the buffer and notifies, drops from the head when over the cap
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

    //ParseLevel reads the level from the line
    //Cannot just look at the leading bracket: the fed text carries ANSI color codes, so the start is actually something like ESC[33m
    //That would read 33m and every line would be treated as Info
    //So it searches for whole level tags and takes the earliest one, even if the same literal appears in the message body it cannot come before the tag
    private static readonly (string Tag, LogLevel Level)[] LevelTags =
    {
        ("[DBG]", LogLevel.Debug),
        ("[INFO]", LogLevel.Info),
        ("[WARN]", LogLevel.Warning),
        ("[ERROR]", LogLevel.Error),
        ("[CRIT]", LogLevel.Critical),
    };

    //ParseLevel detects the level, color codes must be stripped before searching
    //Console lines only color the level tag, and the color code is inserted before the level name, so the actual text looks like [ESC[33mWARN ESC[0m]
    //The brackets are split by the color code, so searching directly for "[WARN]" matches nothing and the whole screen falls back to the default Info
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

    //StripAnsi removes SGR color sequences, returns the line as is when it has no color codes and does not allocate
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
