using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//SetIdleTimeoutCommand setidletimeout command, maps to vanilla net.minecraft.server.commands.SetIdleTimeoutCommand
//Idle kick minutes; 0 means never kick; written back to server.properties
public static class SetIdleTimeoutCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("setidletimeout")
            .Requires(s => s.HasPermission(3))
            .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("minutes", IntegerArgumentType.Integer(0))
                .Executes(Set)));
    }

    //Set rewrites the idle timeout and persists it; 0 disables the feature
    private static int Set(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var minutes = IntegerArgumentType.GetInteger(context, "minutes");
        source.Server.Settings.SetPlayerIdleTimeout(minutes);
        source.Server.Settings.SaveCurrent();
        source.SendSuccess(minutes == 0
            ? "idle kick disabled"
            : $"idle kick set to {minutes} minutes");
        return minutes;
    }
}
