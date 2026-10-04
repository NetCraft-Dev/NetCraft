using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands;

//RotateCommand rotate 命令对应原版 net.minecraft.server.commands.RotateCommand
//直接给目标改朝向 支持绝对角度与朝向坐标两种写法 原版的朝向实体分支暂缺
public static class RotateCommand
{
    //EyeHeight 玩家眼睛高度 朝向坐标算俯仰要减它
    private const double EyeHeight = 1.62;

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("rotate")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Players())
                .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("rotation", RotationArgument.Rotation())
                    .Executes(context => RotateByAngle(context)))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("facing")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec3Argument.Vec3())
                        .Executes(context => RotateToPos(context))))));
    }

    //RotateByAngle 用参数给出的绝对角度
    private static int RotateByAngle(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        var (yaw, pitch) = RotationArgument.GetRotation(context, "rotation").GetRotation(source);
        return Apply(source, targets, yaw, pitch);
    }

    //RotateToPos 由执行者位置指向目标点算偏航与俯仰 对应原版 LookAt 的公式
    private static int RotateToPos(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        var destination = Vec3Argument.GetVec3(context, "pos");
        var changed = 0;
        foreach (var target in targets)
        {
            var dx = destination.X - target.Position.X;
            var dy = destination.Y - (target.Position.Y + EyeHeight);
            var dz = destination.Z - target.Position.Z;
            var horizontal = Math.Sqrt(dx * dx + dz * dz);
            var yaw = (float)(Math.Atan2(dz, dx) * 180.0 / Math.PI) - 90f;
            var pitch = (float)(-Math.Atan2(dy, horizontal) * 180.0 / Math.PI);
            SendRotation(target, yaw, pitch);
            changed++;
        }

        source.SendSuccess($"已让 {changed} 名玩家转向 {destination.X:F1} {destination.Y:F1} {destination.Z:F1}");
        return changed;
    }

    //Apply 把角度写到目标并发朝向包
    private static int Apply(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets, float yaw, float pitch)
    {
        if (targets.Count == 0)
        {
            source.SendFailure("没有找到匹配的玩家");
            return 0;
        }

        foreach (var target in targets)
            SendRotation(target, yaw, pitch);

        source.SendSuccess($"已让 {targets.Count} 名玩家转向 {yaw:F1} {pitch:F1}");
        return targets.Count;
    }

    //SendRotation 写朝向并发绝对角度包 相对标志为 false
    private static void SendRotation(ServerPlayer target, float yaw, float pitch)
    {
        target.Yaw = yaw;
        target.Pitch = pitch;
        target.Connection.Send(new ClientboundPlayerRotationPacket(yaw, false, pitch, false));
    }
}
