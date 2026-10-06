using System.Text;
using Avalonia.Media;

namespace NetCraft.Server.Gui;

//AnsiLogParser turns ANSI SGR sequences in logs into colored spans
//The logging system colors whole lines by level, this still parses generic SGR so future inline partial coloring also displays correctly
//The original black is unreadable on a dark background, so the black in the basic colors is replaced with bright gray
public static class AnsiLogParser
{
    //A run of same-colored text
    public readonly record struct LogSpan(string Text, IBrush Brush);

    private const char Escape = '\u001B';

    //Default foreground color on a dark background
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#C9D1D9"));

    //30-37 basic colors, indices map one-to-one to SGR codes
    private static readonly IBrush[] Basic =
    {
        new SolidColorBrush(Color.Parse("#8B949E")), // black
        new SolidColorBrush(Color.Parse("#F87171")), // red
        new SolidColorBrush(Color.Parse("#4ADE80")), // green
        new SolidColorBrush(Color.Parse("#FACC15")), // yellow
        new SolidColorBrush(Color.Parse("#60A5FA")), // blue
        new SolidColorBrush(Color.Parse("#C084FC")), // purple
        new SolidColorBrush(Color.Parse("#22D3EE")), // cyan
        new SolidColorBrush(Color.Parse("#E6E6E6")), // white
    };

    //90-97 bright colors
    private static readonly IBrush[] Bright =
    {
        new SolidColorBrush(Color.Parse("#C9D1D9")), // bright black
        new SolidColorBrush(Color.Parse("#FCA5A5")), // bright red
        new SolidColorBrush(Color.Parse("#86EFAC")), // bright green
        new SolidColorBrush(Color.Parse("#FDE047")), // bright yellow
        new SolidColorBrush(Color.Parse("#93C5FD")), // bright blue
        new SolidColorBrush(Color.Parse("#D8B4FE")), // bright purple
        new SolidColorBrush(Color.Parse("#67E8F9")), // bright cyan
        new SolidColorBrush(Color.Parse("#FFFFFF")), // bright white
    };

    //Parse splits a line into same-colored spans, one span for the whole line when there are no color codes
    public static List<LogSpan> Parse(string line)
    {
        var spans = new List<LogSpan>();
        var text = new StringBuilder();
        var brush = DefaultBrush;
        var index = 0;
        while (index < line.Length)
        {
            //Only SGR (ESC[...m) is recognized, unrecognized escape sequences stay in the text as ordinary characters
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

    //Resolve applies SGR parameters one by one, unrecognized ones keep the current color
    //38;2;r;g;b is truecolor and must consume the following three segments together, not be read one parameter at a time
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
