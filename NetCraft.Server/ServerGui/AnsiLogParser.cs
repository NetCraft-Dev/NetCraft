using System.Text;
using Avalonia.Media;

namespace NetCraft.Server.Gui;

//AnsiLogParser 把日志里的 ANSI SGR 序列还原成带颜色的片段
//日志系统按级别给整行着色 这里仍按通用 SGR 解析 将来出现行内分段着色也能照常显示
//深底上本来那支黑色读不出来 基本色里的黑换成亮灰
public static class AnsiLogParser
{
    //一段同色文本
    public readonly record struct LogSpan(string Text, IBrush Brush);

    private const char Escape = '\u001B';

    //深底默认前景色
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#C9D1D9"));

    //30-37 基本色 下标与 SGR 码一一对应
    private static readonly IBrush[] Basic =
    {
        new SolidColorBrush(Color.Parse("#8B949E")), // 黑
        new SolidColorBrush(Color.Parse("#F87171")), // 红
        new SolidColorBrush(Color.Parse("#4ADE80")), // 绿
        new SolidColorBrush(Color.Parse("#FACC15")), // 黄
        new SolidColorBrush(Color.Parse("#60A5FA")), // 蓝
        new SolidColorBrush(Color.Parse("#C084FC")), // 紫
        new SolidColorBrush(Color.Parse("#22D3EE")), // 青
        new SolidColorBrush(Color.Parse("#E6E6E6")), // 白
    };

    //90-97 亮色
    private static readonly IBrush[] Bright =
    {
        new SolidColorBrush(Color.Parse("#C9D1D9")), // 亮黑
        new SolidColorBrush(Color.Parse("#FCA5A5")), // 亮红
        new SolidColorBrush(Color.Parse("#86EFAC")), // 亮绿
        new SolidColorBrush(Color.Parse("#FDE047")), // 亮黄
        new SolidColorBrush(Color.Parse("#93C5FD")), // 亮蓝
        new SolidColorBrush(Color.Parse("#D8B4FE")), // 亮紫
        new SolidColorBrush(Color.Parse("#67E8F9")), // 亮青
        new SolidColorBrush(Color.Parse("#FFFFFF")), // 亮白
    };

    //Parse 拆出一行的同色片段 没有颜色码时整行一段
    public static List<LogSpan> Parse(string line)
    {
        var spans = new List<LogSpan>();
        var text = new StringBuilder();
        var brush = DefaultBrush;
        var index = 0;
        while (index < line.Length)
        {
            //只认 SGR(ESC[...m) 认不出的转义序列按普通字符留在文本里
            if (line[index] == Escape && index + 1 < line.Length && line[index + 1] == '[')
            {
                var end = line.IndexOf('m', index + 2);
                if (end < 0) break;
                if (text.Length > 0)
                {
                    spans.Add(new LogSpan(text.ToString(), brush));
                    text.Clear();
                }
                brush = Resolve(line[(index + 2)..end], brush);
                index = end + 1;
                continue;
            }
            text.Append(line[index]);
            index++;
        }
        if (text.Length > 0) spans.Add(new LogSpan(text.ToString(), brush));
        return spans;
    }

    //Resolve 逐个应用 SGR 参数 认不出的保持当前颜色
    //38;2;r;g;b 是真彩 要连后面三段一起吃掉 不能按单参数逐个看
    private static IBrush Resolve(string parameters, IBrush current)
    {
        var parts = parameters.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var code)) continue;
            switch (code)
            {
                case 0 or 39:
                    current = DefaultBrush;
                    break;
                case >= 30 and <= 37:
                    current = Basic[code - 30];
                    break;
                case >= 90 and <= 97:
                    current = Bright[code - 90];
                    break;
                case 38 when i + 4 < parts.Length && parts[i + 1] == "2":
                    current = new SolidColorBrush(Color.FromRgb(
                        ToByte(parts[i + 2]), ToByte(parts[i + 3]), ToByte(parts[i + 4])));
                    i += 4;
                    break;
            }
        }
        return current;
    }

    private static byte ToByte(string text)
        => int.TryParse(text, out var value) ? (byte)Math.Clamp(value, 0, 255) : (byte)0;
}
