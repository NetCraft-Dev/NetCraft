using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//PlaySoundCommand playsound command, maps to vanilla net.minecraft.server.commands.PlaySoundCommand
//Plays the given sound at the target position; vanilla's source/position/volume fade branches are missing, only the sound and targets are kept
public static class PlaySoundCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("playsound")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, string>.Argument("sound", StringArgumentType.String())
                .Executes(context => Play(context, null))
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                    .Executes(context => Play(context, EntityArgument.GetPlayers(context, "targets"))))));
    }

    //Play sends the play-sound packet per target; without targets it plays only to the executor
    private static int Play(CommandContext<CommandSourceStack> context, IReadOnlyList<ServerPlayer>? explicitTargets)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var raw = StringArgumentType.GetString(context, "sound");
        if (Identifier.TryParse(raw) is not { } id)
        {
            source.SendFailure($"invalid sound identifier {raw}");
            return 0;
        }

        var targets = explicitTargets ?? new[] { source.Player };
        var sound = new SoundEvent(id);
        var played = 0;
        foreach (var target in targets)
        {
            target.Connection.Send(new ClientboundSoundPacket(sound, SoundSource.Master,
                target.Position.X, target.Position.Y, target.Position.Z, 1f, 1f, Random.Shared.NextInt64()));
            played++;
        }

        source.SendSuccess($"played sound {id} for {played} players");
        return played;
    }
}
