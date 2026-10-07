using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Commands.Tree;

namespace NetCraft.Game.Commands;

//HelpCommand help command, maps to vanilla net.minecraft.server.commands.HelpCommand
//Without arguments lists a short usage for every available command; with a command name it gives the full usage
//The command name goes through brigadier:string rather than a custom argument type: the whole command tree must sync to the client
//Attaching an argument type not registered in the network id table makes the whole packet dropped at the encoding stage and the client sees no command
public static class HelpCommand
{
    private const string Header = "--- showing help ---";

    private static readonly DynamicCommandExceptionType ErrorFailed =
        new(command => new TranslatableMessage("commands.help.failed", command));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("help")
            .Executes(c => ShowHelp(c, dispatcher))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("command", StringArgumentType.Word())
                .Suggests((context, builder) => SuggestCommandNames(context, dispatcher, builder))
                .Executes(c => ShowUsage(c, dispatcher, StringArgumentType.GetString(c, "command")))));
    }

    //ShowHelp lists a short usage for each available command under the root, maps to vanilla help without arguments
    private static int ShowHelp(CommandContext<CommandSourceStack> context, CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var source = (ServerCommandSource)context.GetSource();
        var usages = dispatcher.GetSmartUsage(dispatcher.GetRoot(), source);
        source.SendSuccess(Header);
        foreach (var usage in usages.Values) source.SendSuccess("/" + usage);
        return usages.Count;
    }

    //ShowUsage gives the full usage of a single command, maps to vanilla help <command>
    private static int ShowUsage(CommandContext<CommandSourceStack> context, CommandDispatcher<CommandSourceStack> dispatcher,
        string name)
    {
        var source = (ServerCommandSource)context.GetSource();
        //A mismatched name reports unknown argument directly, same effect as failing at the parse stage
        var node = dispatcher.GetRoot().GetChild(name)
            ?? throw CommandSyntaxException.BuiltInExceptions.DispatcherUnknownArgument().Create();
        if (!node.CanUse(source)) throw ErrorFailed.Create(node.GetUsageText());
        source.SendSuccess(Header);
        var count = 0;
        //When the node itself is executable it reports the bare command name first, then each sub-path
        //The usage text excludes the node name, so it must be prepended here, or it shows as a headless " /[<targets>]"
        if (node.GetCommand() is not null)
        {
            source.SendSuccess("/" + name);
            count++;
        }
        foreach (var usage in dispatcher.GetSmartUsage(node, source).Values)
        {
            source.SendSuccess("/" + name + " " + usage);
            count++;
        }
        //When there is neither an executor nor sub-paths (e.g. tp used only for redirect) at least report the command name
        if (count == 0) source.SendSuccess("/" + name);
        return count;
    }

    //SuggestCommandNames completes help's command names, listing only those the executor may use
    private static Task<Suggestions> SuggestCommandNames(CommandContext<CommandSourceStack> context,
        CommandDispatcher<CommandSourceStack> dispatcher, SuggestionsBuilder builder)
    {
        var source = (ServerCommandSource)context.GetSource();
        var remaining = builder.Remaining;
        foreach (var child in dispatcher.GetRoot().GetChildren())
        {
            var name = child.GetUsageText();
            if (!name.StartsWith(remaining, StringComparison.Ordinal)) continue;
            if (!child.CanUse(source)) continue;
            builder.Add(name);
        }
        return builder.BuildFuture();
    }
}
