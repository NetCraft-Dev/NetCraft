using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Server;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands;

//SpawnPointCommand spawnpoint 命令对应原版 net.minecraft.server.commands.SetSpawnCommand 的 spawnpoint 字面量
//设置玩家个人重生点 省略目标时用执行者自己 省略坐标时用执行者当前位置取整
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

    //Set 写目标玩家的个人重生点 落点在方块中心故 x/z 加半格
    //原版走 getSpawnablePos 会挑安全高度 nc 直接用给定坐标
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
            source.SendSuccess($"已将 {players[0].Profile.Name} 的重生点设为 {spawn.X} {spawn.Y} {spawn.Z}");
        else
            source.SendSuccess($"已将 {players.Count} 名玩家的重生点设为 {spawn.X} {spawn.Y} {spawn.Z}");
        return players.Count;
    }
}
