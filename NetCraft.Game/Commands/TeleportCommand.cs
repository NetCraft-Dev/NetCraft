using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Primitives;

namespace NetCraft.Game.Commands;

//TeleportCommand teleport/tp 命令对应原版 net.minecraft.server.commands.TeleportCommand
//tp 为 teleport 的 redirect 坐标支持相对与本地 朝向可显式 rotation 或 facing 实体/坐标
//目标集合按玩家与关卡实体分派 玩家走位置确认链 关卡实体直接落服务端由追踪器补发移动包
//执行时按 relatives 把绝对量换算回相对量下发 客户端叠加自身当前值复原绝对位置
public static class TeleportCommand
{
    private static int _teleportId;

    //LookAt 传送后朝向 只负责给出目标点 算法对玩家与实体是同一套
    private interface LookAt
    {
        Vec3 TargetPoint();
    }

    //朝向实体 eyes/feet 决定目标基准点 对应原版 LookAt.LookAtEntity
    private sealed record LookAtEntity(CommandTarget Entity, EntityAnchorArgument.Anchor Anchor) : LookAt
    {
        public Vec3 TargetPoint() => AnchorPosition(Anchor, Entity);
    }

    //朝向坐标 对应原版 LookAt.LookAtPosition
    private sealed record LookAtPosition(Vec3 Position) : LookAt
    {
        public Vec3 TargetPoint() => Position;
    }

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        var teleport = dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("teleport")
            .Requires(s => s.HasPermission(2))
            .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("location", Vec3Argument.Vec3())
                .Executes(c =>
                {
                    var source = (ServerCommandSource)c.GetSource();
                    return TeleportToPos(source, new[] { CommandTarget.OfPlayer(source.PlayerOrThrow) },
                        Vec3Argument.GetCoordinates(c, "location"), null, null);
                }))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("destination", EntityArgument.Entity())
                .Executes(c =>
                {
                    var source = (ServerCommandSource)c.GetSource();
                    return TeleportToEntity(source, new[] { CommandTarget.OfPlayer(source.PlayerOrThrow) },
                        EntityArgument.GetSingleTarget(c, "destination"));
                }))
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Entities())
                //destination 与 location 平级挂在 targets 下 对应原版 teleport <targets> <destination>
                .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("destination", EntityArgument.Entity())
                    .Executes(c => TeleportToEntity((ServerCommandSource)c.GetSource(),
                        EntityArgument.GetEntities(c, "targets"), EntityArgument.GetSingleTarget(c, "destination"))))
                .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("location", Vec3Argument.Vec3())
                    .Executes(c => TeleportToPos((ServerCommandSource)c.GetSource(),
                        EntityArgument.GetEntities(c, "targets"), Vec3Argument.GetCoordinates(c, "location"), null, null))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("rotation", RotationArgument.Rotation())
                        .Executes(c => TeleportToPos((ServerCommandSource)c.GetSource(),
                            EntityArgument.GetEntities(c, "targets"), Vec3Argument.GetCoordinates(c, "location"),
                            RotationArgument.GetRotation(c, "rotation"), null)))
                    .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("facing")
                        .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("entity")
                            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("facingEntity", EntityArgument.Entity())
                                .Executes(c => TeleportToPos((ServerCommandSource)c.GetSource(),
                                    EntityArgument.GetEntities(c, "targets"), Vec3Argument.GetCoordinates(c, "location"), null,
                                    new LookAtEntity(EntityArgument.GetSingleTarget(c, "facingEntity"), EntityAnchorArgument.Anchor.Feet))))
                            .Then(RequiredArgumentBuilder<CommandSourceStack, EntityAnchorArgument.Anchor>.Argument("facingAnchor", EntityAnchorArgument.EntityAnchor())
                                .Executes(c => TeleportToPos((ServerCommandSource)c.GetSource(),
                                    EntityArgument.GetEntities(c, "targets"), Vec3Argument.GetCoordinates(c, "location"), null,
                                    new LookAtEntity(EntityArgument.GetSingleTarget(c, "facingEntity"), EntityAnchorArgument.GetAnchor(c, "facingAnchor"))))))
                        .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("facingLocation", Vec3Argument.Vec3())
                            .Executes(c => TeleportToPos((ServerCommandSource)c.GetSource(),
                                EntityArgument.GetEntities(c, "targets"), Vec3Argument.GetCoordinates(c, "location"), null,
                                new LookAtPosition(Vec3Argument.GetVec3(c, "facingLocation")))))))));
        //tp 重定向到 teleport 与原版共享整棵子树
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("tp")
            .Requires(s => s.HasPermission(2))
            .Redirect(teleport));
    }

    //TeleportTarget 把单个命令目标传送到绝对坐标并保持朝向 供其它命令复用
    //玩家走位置包同步链 关卡实体直接改位置由实体追踪器补发移动包
    public static void TeleportTarget(ServerCommandSource source, CommandTarget target, double x, double y, double z)
        => PerformTeleport(source, target, x, y, z, new HashSet<RelativeFlag>(), target.Yaw, target.Pitch, null);

    //TeleportToEntity 把目标集合传到目标实体脚下 继承其朝向
    private static int TeleportToEntity(ServerCommandSource source, IReadOnlyList<CommandTarget> targets, CommandTarget destination)
    {
        foreach (var target in targets)
            PerformTeleport(source, target, destination.Position.X, destination.Position.Y, destination.Position.Z,
                new HashSet<RelativeFlag>(), destination.Yaw, destination.Pitch, null);
        SendTeleportSuccess(source, targets, destination.Position);
        return targets.Count;
    }

    //TeleportToPos 把目标集合传到指定坐标 rotation 为空保持当前朝向
    private static int TeleportToPos(ServerCommandSource source, IReadOnlyList<CommandTarget> targets, Coordinates destination, Coordinates? rotation, LookAt? lookAt)
    {
        var pos = destination.GetPosition(source);
        float? yaw = null;
        float? pitch = null;
        if (rotation is not null)
        {
            var rot = rotation.GetRotation(source);
            yaw = rot.Yaw;
            pitch = rot.Pitch;
        }
        foreach (var target in targets)
        {
            //跨维度判定只对玩家有意义 控制台源没有所在维度 按同维度处理
            var sameDimension = target.Player is not { } player || source.Player is not { } origin
                || ReferenceEquals(player.Level, origin.Level);
            var relatives = GetRelatives(destination, rotation, sameDimension);
            if (yaw is null)
                PerformTeleport(source, target, pos.X, pos.Y, pos.Z, relatives, target.Yaw, target.Pitch, lookAt);
            else
                PerformTeleport(source, target, pos.X, pos.Y, pos.Z, relatives, yaw.Value, pitch!.Value, lookAt);
        }
        SendTeleportSuccess(source, targets, pos);
        return targets.Count;
    }

    //GetRelatives 推导传送的相对标志集 速度/位置/朝向三段 对应原版 getRelatives
    private static IReadOnlySet<RelativeFlag> GetRelatives(Coordinates destination, Coordinates? rotation, bool sameDimension)
    {
        var dir = RelativeFlags.Direction(destination.IsXRelative, destination.IsYRelative, destination.IsZRelative);
        //跨维度传送位置必为绝对 本地坐标保持不了速度也一并清零
        var pos = sameDimension
            ? RelativeFlags.Position(destination.IsXRelative, destination.IsYRelative, destination.IsZRelative)
            : new HashSet<RelativeFlag>();
        var rot = rotation is null
            ? RelativeFlags.Rotation
            : RelativeFlags.RotationOf(rotation.IsYRelative, rotation.IsXRelative);
        return RelativeFlags.Union(dir, pos, rot);
    }

    //PerformTeleport 按目标来源分派 x/y/z 与 yaw/pitch 都是绝对量
    private static void PerformTeleport(ServerCommandSource source, CommandTarget target, double x, double y, double z,
        IReadOnlySet<RelativeFlag> relatives, float yaw, float pitch, LookAt? lookAt)
    {
        if (target.Player is { } player)
        {
            PerformPlayerTeleport(source, player, x, y, z, relatives, yaw, pitch, lookAt);
            return;
        }
        PerformEntityTeleport(target, x, y, z, yaw, pitch, lookAt);
    }

    //PerformPlayerTeleport 应用传送并下发位置包 对应原版 performTeleport
    //相对分量减去当前值换成相对量 客户端按 relatives 叠加自身当前值复原绝对位置
    //有监听器时走它的等待确认流程 传送期间上报的旧坐标不会被采纳
    private static void PerformPlayerTeleport(ServerCommandSource source, ServerPlayer player, double x, double y, double z,
        IReadOnlySet<RelativeFlag> relatives, float yaw, float pitch, LookAt? lookAt)
    {
        var relX = x - (relatives.Contains(RelativeFlag.X) ? player.Position.X : 0);
        var relY = y - (relatives.Contains(RelativeFlag.Y) ? player.Position.Y : 0);
        var relZ = z - (relatives.Contains(RelativeFlag.Z) ? player.Position.Z : 0);
        var relYaw = yaw - (relatives.Contains(RelativeFlag.YRot) ? player.Yaw : 0f);
        var relPitch = pitch - (relatives.Contains(RelativeFlag.XRot) ? player.Pitch : 0f);

        //服务端先行应用绝对量 对应原版 teleportSetPosition 的 calculateAbsolute
        var target = new Vec3(
            relX + (relatives.Contains(RelativeFlag.X) ? player.Position.X : 0),
            relY + (relatives.Contains(RelativeFlag.Y) ? player.Position.Y : 0),
            relZ + (relatives.Contains(RelativeFlag.Z) ? player.Position.Z : 0));
        player.Position = target;
        player.Yaw = relYaw + (relatives.Contains(RelativeFlag.YRot) ? player.Yaw : 0f);
        player.Pitch = Math.Clamp(relPitch + (relatives.Contains(RelativeFlag.XRot) ? player.Pitch : 0f), -90f, 90f);

        //朝向先于位置包应用 最终朝向随位置包下发 对应原版 lookAt.perform
        //lookAt 改的是服务端朝向 客户端不知情 相对分量要把这次改动的差值算进去
        var yawBeforeLook = player.Yaw;
        var pitchBeforeLook = player.Pitch;
        ApplyLookAt(player, lookAt);

        //含 YRot/XRot 时下发相对量由客户端叠加自身当前值 不含时直接下发绝对量
        var packetYaw = relatives.Contains(RelativeFlag.YRot) ? relYaw + (player.Yaw - yawBeforeLook) : player.Yaw;
        var packetPitch = relatives.Contains(RelativeFlag.XRot) ? relPitch + (player.Pitch - pitchBeforeLook) : player.Pitch;
        var packed = RelativeFlags.Pack(relatives);

        var listener = player.Listener;
        if (listener is not null)
        {
            listener.Teleport(target, player.Yaw, player.Pitch, relX, relY, relZ, packetYaw, packetPitch, packed);
            return;
        }
        //没有监听器的场合(测试用假连接)退化为直接下发 位置与朝向已落到玩家状态
        player.Connection.Send(new ClientboundPlayerPositionPacket(
            relX, relY, relZ, packetYaw, packetPitch, packed, _teleportId++));
    }

    //PerformEntityTeleport 关卡实体没有位置确认流程 改完位置由实体追踪器补发移动包
    private static void PerformEntityTeleport(CommandTarget target, double x, double y, double z, float yaw, float pitch, LookAt? lookAt)
    {
        var position = new Vec3(x, y, z);
        if (lookAt is not null)
        {
            //朝向按落点算 与原版先把实体挪过去再转头一致
            var (lookYaw, lookPitch) = LookAngles(position, lookAt.TargetPoint());
            yaw = lookYaw;
            pitch = lookPitch;
        }
        target.WorldEntity!.SetPos(position, yaw, pitch);
    }

    //ApplyLookAt 把朝向落到玩家上 目标点为空时不改
    private static void ApplyLookAt(ServerPlayer player, LookAt? lookAt)
    {
        if (lookAt is null) return;
        var (yaw, pitch) = LookAngles(player.Position, lookAt.TargetPoint());
        player.Yaw = yaw;
        player.Pitch = pitch;
    }

    //AnchorPosition 求朝向目标按锚点的世界坐标
    //玩家用固定站立眼高 关卡实体没有单独的眼高数据 取包围盒九成高度处近似
    private static Vec3 AnchorPosition(EntityAnchorArgument.Anchor anchor, CommandTarget target)
    {
        if (target.Player is { } player) return EntityAnchorArgument.Apply(anchor, player);
        if (anchor == EntityAnchorArgument.Anchor.Feet) return target.Position;
        var box = target.BoundingBox;
        return new Vec3(target.Position.X, box.Min.Y + box.Size.Y * 0.9, target.Position.Z);
    }

    //LookAngles 从 from 点看向 to 点的角度 对应原版 Entity.lookAt
    private static (float Yaw, float Pitch) LookAngles(Vec3 from, Vec3 to)
    {
        var xd = to.X - from.X;
        var yd = to.Y - from.Y;
        var zd = to.Z - from.Z;
        var sd = Math.Sqrt(xd * xd + zd * zd);
        var pitch = WrapDegrees((float)(-(Math.Atan2(yd, sd) * 180.0 / Math.PI)));
        var yaw = WrapDegrees((float)((Math.Atan2(zd, xd) * 180.0 / Math.PI) - 90.0));
        return (yaw, pitch);
    }

    //WrapDegrees 角度规范到[-180,180) 对应原版 Mth.wrapDegrees
    private static float WrapDegrees(float value)
    {
        value %= 360;
        if (value >= 180) value -= 360;
        if (value < -180) value += 360;
        return value;
    }

    //SendTeleportSuccess 回执传送结果 单目标点名 多目标报数量
    private static void SendTeleportSuccess(ServerCommandSource source, IReadOnlyList<CommandTarget> targets, Vec3 pos)
    {
        if (targets.Count == 1)
            source.SendSuccess($"已将 {targets[0].Name} 传送到 {FormatDouble(pos.X)} {FormatDouble(pos.Y)} {FormatDouble(pos.Z)}");
        else
            source.SendSuccess($"已将 {targets.Count} 个实体传送到 {FormatDouble(pos.X)} {FormatDouble(pos.Y)} {FormatDouble(pos.Z)}");
    }

    //FormatDouble 六位小数 对应原版 String.format %f
    private static string FormatDouble(double value) => value.ToString("F6");
}
