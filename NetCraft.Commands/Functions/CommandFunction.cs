using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Execution.Tasks;
using NetCraft.Commands.Tree;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//CommandFunction 编译后的函数对应原版 net.minecraft.commands.functions.CommandFunction
//从 .mcfunction 文本的行列表编译而来 支持行尾反斜杠续行 # 注释 与 $ 宏行
public interface CommandFunction<T>
{
    //Id 函数标识
    Identifier Id { get; }

    //Instantiate 带宏参数实例化 普通函数返回自身
    InstantiatedFunction<T> Instantiate(CompoundTag? arguments, CommandDispatcher<T> dispatcher);

    //ShouldConcatenateNextLine 行尾反斜杠表示续行
    private static bool ShouldConcatenateNextLine(string line)
        => line.Length > 0 && line[^1] == '\\';

    //FromLines 从行列表编译函数对应原版 fromLines
    public static CommandFunction<T> FromLines(Identifier id, CommandDispatcher<T> dispatcher, T compilationContext, List<string> lines)
    {
        var functionBuilder = new FunctionBuilder<T>();
        for (var i = 0; i < lines.Count; ++i)
        {
            var lineNumber = i + 1;
            var inputLine = lines[i].Trim();
            string line;
            if (ShouldConcatenateNextLine(inputLine))
            {
                var builder = new System.Text.StringBuilder(inputLine);
                do
                {
                    if (++i == lines.Count)
                    {
                        throw new ArgumentException("Line continuation at end of file");
                    }
                    builder.Remove(builder.Length - 1, 1);
                    var innerLine = lines[i].Trim();
                    builder.Append(innerLine);
                    CommandFunctions.CheckCommandLineLength(builder);
                } while (ShouldConcatenateNextLine(builder.ToString()));
                line = builder.ToString();
            }
            else
            {
                line = inputLine;
            }
            CommandFunctions.CheckCommandLineLength(line);
            var input = new StringReader(line);
            if (!input.CanRead() || input.Peek() == '#') continue;
            if (input.Peek() == '/')
            {
                input.Skip();
                if (input.Peek() == '/')
                {
                    throw new ArgumentException($"Unknown or invalid command '{line}' on line {lineNumber} (if you intended to make a comment, use '#' not '//')");
                }
                var name = input.ReadUnquotedString();
                throw new ArgumentException($"Unknown or invalid command '{line}' on line {lineNumber} (did you mean '{name}'? Do not use a preceding forwards slash.)");
            }
            if (input.Peek() == '$')
            {
                functionBuilder.AddMacro(line[1..], lineNumber, compilationContext);
                continue;
            }
            try
            {
                functionBuilder.AddCommand(ParseCommand(dispatcher, compilationContext, input));
            }
            catch (CommandSyntaxException e)
            {
                throw new ArgumentException($"Whilst parsing command on line {lineNumber}: {e.Message}");
            }
        }
        return functionBuilder.Build(id);
    }

        //ParseCommand 把一行命令编译成未绑定动作对应原版 parseCommand
    public static UnboundEntryAction<T> ParseCommand(CommandDispatcher<T> dispatcher, T compilationContext, StringReader input)
    {
        var parse = dispatcher.Parse(input, compilationContext);
        ValidateParseResults(parse);
        var commandChain = ContextChain<T>.TryFlatten(parse.GetContext().Build(input.String));
        if (!commandChain.IsPresent)
        {
            throw CommandSyntaxException.BuiltInExceptions.DispatcherUnknownCommand().CreateWithContext(parse.GetReader());
        }
        var unbound = new BuildContexts<T>.Unbound(input.String, commandChain.Get());
        return unbound.ToUnboundAction();
    }

    //ValidateParseResults 解析有错就抛对应原版 Commands.validateParseResults 与 getParseException
    public static void ValidateParseResults(ParseResults<T> parse)
    {
        //输入没读完必有错 对应原版 canRead 检查
        if (!parse.GetReader().CanRead())
        {
            return;
        }
        if (parse.GetExceptions().Count == 1)
        {
            throw parse.GetExceptions().Values.First();
        }
        if (parse.GetContext().GetRange().IsEmpty())
        {
            throw CommandSyntaxException.BuiltInExceptions.DispatcherUnknownCommand().CreateWithContext(parse.GetReader());
        }
    }
}
