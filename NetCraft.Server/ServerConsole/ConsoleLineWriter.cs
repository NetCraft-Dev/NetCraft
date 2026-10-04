using System.IO;
using System.Text;

namespace NetCraft.Server.ServerConsole;

//ConsoleLineWriter 接替 Console.Out 把内核日志交给 REPL
//内核只在 FlushConsole 一处写控制台 整批可能好几行 这里按换行拆开逐条交出去
//REPL 自己输出用启动时存下的原始 writer 不绕回来 否则会自递归
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
        //没换行收尾的残段也交出去 日志正常不以半行结束 但不赌这一点
        if (_pending.Length == 0) return;
        var line = _pending;
        _pending = string.Empty;
        _repl.WriteLine(line);
    }

    //Append 按换行切成整行 末尾没换行的那截攒着等下一批
    private void Append(string text)
    {
        var data = _pending + text;
        var start = 0;
        while (true)
        {
            var index = data.IndexOf('\n', start);
            if (index < 0) break;
            //Windows 下是 \r\n 行尾的 \r 不带进文本
            _repl.WriteLine(data[start..index].TrimEnd('\r'));
            start = index + 1;
        }
        _pending = data[start..];
    }
}
