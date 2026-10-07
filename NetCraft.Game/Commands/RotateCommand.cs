using NetCraft.Commands;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands;

//RotateCommand rotate command, maps to vanilla net.minecraft.server.commands.RotateCommand
//Directly changes the target's facing; supports absolute angles and facing coordinates; vanilla's facing-entity branch is missing
public static class RotateCommand
{
    //EyeHeight player eye height; subtract it when computing pitch from a facing coordinate
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

    //RotateByAngle uses the absolute angle from the argument
    private static int RotateByAngle(CommandContext<CommandSourceStack> context)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var targets = EntityArgument.GetPlayers(context, "targets");
        var (yaw, pitch) = RotationArgument.GetRotation(context, "rotation").GetRotation(source);
        return Apply(source, targets, yaw, pitch);
    }

    //RotateToPos computes yaw and pitch from the executor's position toward the target point, maps to vanilla LookAt's formula
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

        source.SendSuccess($"turned {changed} players toward {destination.X:F1} {destination.Y:F1} {destination.Z:F1}");
        return changed;
    }

    //Apply writes the angle to the target and sends the facing packet
    private static int Apply(ServerCommandSource source, IReadOnlyList<ServerPlayer> targets, float yaw, float pitch)
    {
        if (targets.Count == 0)
        {
            source.SendFailure("no matching player found");
            return 0;
        }

        foreach (var target in targets)
            SendRotation(target, yaw, pitch);

        source.SendSuccess($"turned {targets.Count} players toward {yaw:F1} {pitch:F1}");
        return targets.Count;
    }

    //SendRotation writes the facing and sends the absolute angle packet with the relative flag false
    private static void SendRotation(ServerPlayer target, float yaw, float pitch)
    {
        target.Yaw = yaw;
        target.Pitch = pitch;
        target.Connection.Send(new ClientboundPlayerRotationPacket(yaw, false, pitch, false));
    }
}
