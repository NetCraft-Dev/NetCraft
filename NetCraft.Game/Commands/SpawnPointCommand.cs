using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands;

//SpawnPointCommand spawnpoint command, maps to the spawnpoint literal of vanilla net.minecraft.server.commands.SetSpawnCommand
//Sets a player's personal respawn point; without a target the executor is used; without coordinates the executor's current position is floored
public static class SpawnPointCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("spawnpoint")
            .Requires(s => s.HasPermission(2))
            .Executes(context => Set(context, null, null, 0f))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Executes(context => Set(context, EntityArgument.GetPlayers(context, "targets"), null, 0f))
                .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument("pos", BlockPosArgument.BlockPos())
                    .Executes(context => Set(context, EntityArgument.GetPlayers(context, "targets"),
                        BlockPosArgument.GetBlockPos(context, "pos"), 0f))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("angle", FloatArgumentType.FloatArg(-180f, 180f))
                        .Executes(context => Set(context, EntityArgument.GetPlayers(context, "targets"),
                            BlockPosArgument.GetBlockPos(context, "pos"), FloatArgumentType.GetFloat(context, "angle")))))));
    }

    //Set writes the target player's personal respawn point; the landing point is the block center so x/z add half a block
    //Vanilla goes through getSpawnablePos to pick a safe height; nc uses the given coordinate directly
    private static int Set(CommandContext<CommandSourceStack> context, IReadOnlyList<ServerPlayer>? targets, BlockPos? pos, float angle)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var players = targets ?? new[] { source.PlayerOrThrow };
        var spawn = pos ?? new BlockPos(
            (int)Math.Floor(source.PlayerOrThrow.Position.X),
            (int)Math.Floor(source.PlayerOrThrow.Position.Y),
            (int)Math.Floor(source.PlayerOrThrow.Position.Z));

        foreach (var player in players)
        {
            player.RespawnPos = new Vec3(spawn.X + 0.5, spawn.Y, spawn.Z + 0.5);
            player.RespawnAngle = angle;
        }

        if (players.Count == 1)
            source.SendSuccess($"set {players[0].Profile.Name}'s respawn point to {spawn.X} {spawn.Y} {spawn.Z}");
        else
            source.SendSuccess($"set {players.Count} players' respawn point to {spawn.X} {spawn.Y} {spawn.Z}");
        return players.Count;
    }
}
