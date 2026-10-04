using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands;

//SetWorldSpawnCommand setworldspawn 命令对应原版 net.minecraft.server.commands.SetWorldSpawnCommand
//改写世界出生点并广播给所有玩家 省略坐标时用执行者当前位置取整
public static class SetWorldSpawnCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("setworldspawn")
            .Requires(s => s.HasPermission(2))
            .Executes(context => Set(context, null, 0f))
            .Then(RequiredArgumentBuilder<CommandSourceStack, BlockCoordinates>.Argument("pos", BlockPosArgument.BlockPos())
                .Executes(context => Set(context, BlockPosArgument.GetBlockPos(context, "pos"), 0f))
                .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("angle", FloatArgumentType.FloatArg())
                    .Executes(context => Set(context, BlockPosArgument.GetBlockPos(context, "pos"),
                        FloatArgumentType.GetFloat(context, "angle"))))));
    }

    //Set 写存档出生点并广播 落点在方块中心故 x/z 加半格
    private static int Set(CommandContext<CommandSourceStack> context, BlockPos? explicitPos, float angle)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var pos = explicitPos ?? new BlockPos(
            (int)Math.Floor(source.Position.X),
            (int)Math.Floor(source.Position.Y),
            (int)Math.Floor(source.Position.Z));

        source.Server.SetSpawnPos(new Vec3(pos.X + 0.5, pos.Y, pos.Z + 0.5));
        source.Server.PlayerList.BroadcastAll(new ClientboundSetDefaultSpawnPositionPacket(pos, angle));
        source.SendSuccess($"世界出生点已设为 {pos.X} {pos.Y} {pos.Z}");
        return 1;
    }
}
