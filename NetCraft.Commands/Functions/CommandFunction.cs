using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Execution.Tasks;
using NetCraft.Commands.Tree;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Commands.Functions;

//CommandFunction a compiled function, maps to vanilla net.minecraft.commands.functions.CommandFunction
//Compiled from the line list of .mcfunction text; supports trailing-backslash line continuation, # comments and $ macro lines
public interface CommandFunction<T>
{
    //Id function identifier
    Identifier Id { get; }

    //Instantiate instantiates with macro arguments; an ordinary function returns itself
    InstantiatedFunction<T> Instantiate(CompoundTag? arguments, CommandDispatcher<T> dispatcher);

    //ShouldConcatenateNextLine a trailing backslash means line continuation
    private static bool ShouldConcatenateNextLine(string line)
        => line.Length > 0 && line[^1] == '\\';

    //FromLines compiles a function from a line list; maps to vanilla fromLines
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

        //ParseCommand compiles one command line into an unbound action; maps to vanilla parseCommand
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

    //ValidateParseResults throws on parse errors; maps to vanilla Commands.validateParseResults and getParseException
    public static void ValidateParseResults(ParseResults<T> parse)
    {
        //Unconsumed input always means an error; maps to the vanilla canRead check
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
