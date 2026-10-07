using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;

namespace NetCraft.Game.Commands;

//SaveCommand save-all/save-off/save-on commands, maps to vanilla net.minecraft.server.commands.SaveAllCommand etc.
//save-all flushes everything immediately; save-off/save-on toggle periodic auto-flush
public static class SaveCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("save-all")
            .Requires(s => s.HasPermission(4))
            .Executes(All)
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("flush")
                .Executes(All)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("save-off")
            .Requires(s => s.HasPermission(4))
            .Executes(context => SetAuto(context, false)));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("save-on")
            .Requires(s => s.HasPermission(4))
            .Executes(context => SetAuto(context, true)));
    }

    //All flushes immediately; a full write blocks the main thread so the reply is sent first
    private static int All(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        source.SendSuccess("saving the world");
        source.Server.SaveAllNow();
        source.SendSuccess("the world has been saved");
        return 1;
    }

    //SetAuto toggles auto-flush; it reports no duplicate setting when already in the target state
    private static int SetAuto(CommandContext<CommandSourceStack> context, bool enabled)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        if (source.Server.IsSavingEnabled == enabled)
        {
            source.SendFailure(enabled ? "auto-save is already on" : "auto-save is already off");
            return 0;
        }

        source.Server.SetSavingEnabled(enabled);
        source.SendSuccess(enabled ? "auto-save enabled" : "auto-save disabled; remember to flush manually with save-all");
        return 1;
    }
}
