using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//StopSoundCommand stopsound command, maps to vanilla net.minecraft.server.commands.StopSoundCommand
//Stops the sound currently playing on the targets; without a source it stops all sources
public static class StopSoundCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("stopsound")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Executes(context => Stop(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("source", StringArgumentType.Word())
                    .Executes(context => Stop(context, ParseSource(context))))));
    }

    //ParseSource converts the word argument to the sound source enum; a name mismatch is treated as all sources
    private static SoundSource? ParseSource(CommandContext<CommandSourceStack> context)
    {
        var name = StringArgumentType.GetString(context, "source");
        return Enum.TryParse<SoundSource>(name, true, out var parsed) ? parsed : null;
    }

    //Stop sends the stop-sound packet; a null source stops all sources for that player
    private static int Stop(CommandContext<CommandSourceStack> context, SoundSource? source)
    {
        if (context.GetSource() is not ServerCommandSource source2) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        if (targets.Count == 0)
        {
            source2.SendFailure("no matching player found");
            return 0;
        }

        foreach (var target in targets)
            target.Connection.Send(new ClientboundStopSoundPacket(null, source));

        source2.SendSuccess(source is null
            ? $"stopped all sounds for {targets.Count} players"
            : $"stopped the {source} source for {targets.Count} players");
        return targets.Count;
    }
}
