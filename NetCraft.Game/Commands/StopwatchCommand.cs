using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//StopwatchCommand stopwatch command, maps to vanilla net.minecraft.server.commands.StopwatchCommand
//Creates, queries, restarts and removes debug timers by id; the query return value is the elapsed seconds times a scale
public static class StopwatchCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("stopwatch")
            .Requires(s => s.HasPermission(2))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("create")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(Create)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("query")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(context => Query(context, 1.0))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("scale", DoubleArgumentType.DoubleArg())
                        .Executes(context => Query(context, DoubleArgumentType.GetDouble(context, "scale"))))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("restart")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(Restart)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("remove")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("id", IdentifierArgument.Id())
                    .Executes(Remove))));
    }

    //Create creates a timer; a duplicate name errors
    private static int Create(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (!source.Server.Stopwatches.Add(id, Stopwatches.CurrentTime()))
        {
            source.SendFailure($"timer {id} already exists");
            return 0;
        }

        source.SendSuccess($"created timer {id}");
        return 1;
    }

    //Query reports the elapsed seconds; the return value is scaled
    private static int Query(CommandContext<CommandSourceStack> context, double scale)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (source.Server.Stopwatches.GetStart(id) is not long start)
        {
            source.SendFailure($"timer {id} does not exist");
            return 0;
        }

        var elapsed = (Stopwatches.CurrentTime() - start) / 1000.0;
        source.SendSuccess($"timer {id} has run {elapsed:F3} seconds");
        return (int)(elapsed * scale);
    }

    //Restart resets the timer to now
    private static int Restart(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (!source.Server.Stopwatches.Restart(id, Stopwatches.CurrentTime()))
        {
            source.SendFailure($"timer {id} does not exist");
            return 0;
        }

        source.SendSuccess($"restarted timer {id}");
        return 1;
    }

    //Remove removes the timer
    private static int Remove(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var id = IdentifierArgument.GetId(context, "id");
        if (!source.Server.Stopwatches.Remove(id))
        {
            source.SendFailure($"timer {id} does not exist");
            return 0;
        }

        source.SendSuccess($"removed timer {id}");
        return 1;
    }
}
