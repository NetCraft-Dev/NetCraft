using System.Text;
using NetCraft.Commands;
using NetCraft.Commands.Tree;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;

namespace NetCraft.Server.ServerConsole;

//ICommandHighlighter 输入行语法着色
public interface ICommandHighlighter
{
    //Highlight 给整行套色返回带 ANSI 的文本
    //可见字符一个不多一个不少 调用方仍按原文本长度算光标位置
    string Highlight(string text);
}

//CommandHighlighter 用 brigadier 解析结果给输入行上色
//算法取自 Paper 的 BrigadierCommandHighlighter
//字面量节点保持默认色 参数节点按出现顺序在调色板里轮换 没解析到的尾巴标红
public sealed class CommandHighlighter : ICommandHighlighter
{
    //参数层级轮换用的调色板 与 Paper 一致
    private static readonly int[] Colors = { 6, 3, 2, 5, 4 };

    //解析不动的部分标红 对应 Paper 的 foreground(1)
    private const string ErrorColor = "\u001B[31m";
    private const string Reset = "\u001B[0m";

    private readonly CommandManager _commands;
    //_source 解析用的控制台命令源 构造一次就够 每按一键都新建没必要
    private readonly ServerCommandSource _source;

    public CommandHighlighter(DedicatedServer server)
    {
        _commands = server.Commands;
        _source = ServerCommandSource.Console(server);
    }

    public string Highlight(string text)
    {
        if (text.Length == 0) return text;

        ParseResults<CommandSourceStack> results;
        try
        {
            results = _commands.Dispatcher.Parse(text, _source);
        }
        catch (Exception)
        {
            //解析器不该把控制台线程带走 真出事就整行标红
            return ErrorColor + text + Reset;
        }

        var builder = new StringBuilder(text.Length + 24);
        var pos = 0;
        var component = -1;
        foreach (var parsed in results.GetContext().GetLastChild().GetNodes())
        {
            var start = parsed.Range.Start;
            if (start >= text.Length) break;
            //节点之间若有重叠或倒序就跳过 不能让切片命令抛出去
            if (start < pos) continue;
            var end = Math.Min(parsed.Range.End, text.Length);

            //间隔的空白与字面量都用默认样式
            if (start > pos) builder.Append(text, pos, start - pos);
            if (parsed.Node is LiteralCommandNode<CommandSourceStack>)
            {
                builder.Append(text, start, end - start);
            }
            else
            {
                component = (component + 1) % Colors.Length;
                builder.Append("\u001B[3").Append(Colors[component]).Append('m');
                builder.Append(text, start, end - start);
                builder.Append(Reset);
            }

            pos = end;
        }

        //剩下没被解析到的尾巴是半个命令或写错的参数 标红提示
        if (pos < text.Length)
        {
            builder.Append(ErrorColor);
            builder.Append(text, pos, text.Length - pos);
            builder.Append(Reset);
        }

        return builder.ToString();
    }
}
