using System.IO;
using System.Text;

namespace NetCraft.Server.ServerConsole;

//ConsoleLineWriter takes over Console.Out and routes kernel log output to the REPL
//The kernel writes to the console only at FlushConsole, one batch may span several lines, so this splits by newline and hands them over one by one
//The REPL writes its own output through the original writer saved at startup and does not route back, to avoid recursion
public sealed class ConsoleLineWriter : TextWriter
{
    private readonly ReplConsole _repl;
    private string _pending = string.Empty;

    public ConsoleLineWriter(ReplConsole repl) => _repl = repl;

    public override Encoding Encoding => Console.OutputEncoding;

    public override void Write(char value) => Append(value.ToString());

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value)) Append(value);
    }

    public override void Write(char[] buffer, int index, int count)
        => Append(new string(buffer, index, count));

    public override void Flush()
    {
        //Hand over a trailing fragment without a newline too, logs normally do not end mid-line but do not bet on it
        if (_pending.Length == 0) return;
        var line = _pending;
        _pending = string.Empty;
        _repl.WriteLine(line);
    }

    //Append splits into full lines by newline, the tail without a trailing newline is buffered for the next batch
    private void Append(string text)
    {
        var data = _pending + text;
        var start = 0;
        while (true)
        {
            var index = data.IndexOf('\n', start);
            if (index < 0) break;
            //On Windows line endings are \r\n, the trailing \r is not included in the text
            _repl.WriteLine(data[start..index].TrimEnd('\r'));
            start = index + 1;
        }
        _pending = data[start..];
    }
}
