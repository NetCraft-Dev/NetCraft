using System.Text;
using NetCraft.Commands;
using NetCraft.Commands.Tree;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;

namespace NetCraft.Server.ServerConsole;

//ICommandHighlighter, syntax coloring for the input line
public interface ICommandHighlighter
{
    //Highlight colors the whole line and returns text with ANSI
    //Visible characters are neither added nor removed, the caller still computes the cursor position from the original text length
    string Highlight(string text);
}

//CommandHighlighter colors the input line using brigadier parse results
//The algorithm comes from Paper's BrigadierCommandHighlighter
//Literal nodes keep the default color, argument nodes rotate through the palette in order of appearance, and an unparsed tail is marked red
public sealed class CommandHighlighter : ICommandHighlighter
{
    //Palette used to rotate argument levels, same as Paper
    private static readonly int[] Colors = { 6, 3, 2, 5, 4 };

    //Unparseable parts are marked red, maps to Paper's foreground(1)
    private const string ErrorColor = "\u001B[31m";
    private const string Reset = "\u001B[0m";

    private readonly CommandManager _commands;
    //_source the console command source used for parsing, built once, no need to recreate it per keypress
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
            //The parser must not take down the console thread, if it really fails the whole line is marked red
            return ErrorColor + text + Reset;
        }

        var builder = new StringBuilder(text.Length + 24);
        var pos = 0;
        var component = -1;
        foreach (var parsed in results.GetContext().GetLastChild().GetNodes())
        {
            var start = parsed.Range.Start;
            if (start >= text.Length) break;
            //Skip overlapping or out-of-order nodes, slicing must not throw
            if (start < pos) continue;
            var end = Math.Min(parsed.Range.End, text.Length);

            //Gaps of whitespace and literals both use the default style
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

        //The unparsed tail left over is half a command or a mistyped argument, marked red as a hint
        if (pos < text.Length)
        {
            builder.Append(ErrorColor);
            builder.Append(text, pos, text.Length - pos);
            builder.Append(Reset);
        }

        return builder.ToString();
    }
}
