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

//TeleportCommand teleport/tp command, maps to vanilla net.minecraft.server.commands.TeleportCommand
//tp is a redirect of teleport; coordinates support relative and local; facing can be an explicit rotation or facing an entity/coordinate
//The target set is dispatched by player and level entity: players go through the position confirmation chain, level entities land on the server and the tracker re-sends the move packet
//At execution the absolute quantities are converted back to relative ones by relatives; the client adds its own current value to restore the absolute position
public static class TeleportCommand
{
    private static int _teleportId;

    //LookAt the facing after teleport; it only supplies the target point; the algorithm is the same for players and entities
    private interface LookAt
    {
        Vec3 TargetPoint();
    }

    //Facing entity; eyes/feet decide the target reference point, maps to vanilla LookAt.LookAtEntity
    private sealed record LookAtEntity(CommandTarget Entity, EntityAnchorArgument.Anchor Anchor) : LookAt
    {
        public Vec3 TargetPoint() => AnchorPosition(Anchor, Entity);
    }

    //Facing coordinate, maps to vanilla LookAt.LookAtPosition
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
                //destination hangs under targets alongside location, maps to vanilla teleport <targets> <destination>
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
        //tp redirects to teleport, sharing the whole subtree like vanilla
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("tp")
            .Requires(s => s.HasPermission(2))
            .Redirect(teleport));
    }

    //TeleportTarget teleports a single command target to an absolute coordinate keeping the facing, for reuse by other commands
    //Players go through the position packet sync chain; level entities change position directly and the entity tracker re-sends the move packet
    public static void TeleportTarget(ServerCommandSource source, CommandTarget target, double x, double y, double z)
        => PerformTeleport(source, target, x, y, z, new HashSet<RelativeFlag>(), target.Yaw, target.Pitch, null);

    //TeleportToEntity teleports the target set under the target entity, inheriting its facing
    private static int TeleportToEntity(ServerCommandSource source, IReadOnlyList<CommandTarget> targets, CommandTarget destination)
    {
        foreach (var target in targets)
            PerformTeleport(source, target, destination.Position.X, destination.Position.Y, destination.Position.Z,
                new HashSet<RelativeFlag>(), destination.Yaw, destination.Pitch, null);
        SendTeleportSuccess(source, targets, destination.Position);
        return targets.Count;
    }

    //TeleportToPos teleports the target set to the given coordinate; a null rotation keeps the current facing
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
            //The cross-dimension check is only meaningful for players; a console source has no dimension, so it is treated as the same dimension
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

    //GetRelatives derives the relative flag set of the teleport, in three parts speed/position/facing, maps to vanilla getRelatives
    private static IReadOnlySet<RelativeFlag> GetRelatives(Coordinates destination, Coordinates? rotation, bool sameDimension)
    {
        var dir = RelativeFlags.Direction(destination.IsXRelative, destination.IsYRelative, destination.IsZRelative);
        //A cross-dimension teleport position must be absolute; local coordinates cannot hold and speed is zeroed too
        var pos = sameDimension
            ? RelativeFlags.Position(destination.IsXRelative, destination.IsYRelative, destination.IsZRelative)
            : new HashSet<RelativeFlag>();
        var rot = rotation is null
            ? RelativeFlags.Rotation
            : RelativeFlags.RotationOf(rotation.IsYRelative, rotation.IsXRelative);
        return RelativeFlags.Union(dir, pos, rot);
    }

    //PerformTeleport dispatches by target source; x/y/z and yaw/pitch are all absolute
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

    //PerformPlayerTeleport applies the teleport and sends the position packet, maps to vanilla performTeleport
    //Relative components subtract the current value to become relative; the client adds its own current value by relatives to restore the absolute position
    //With a listener it goes through the listener's wait-for-ack flow; old coordinates reported during the teleport are not accepted
    private static void PerformPlayerTeleport(ServerCommandSource source, ServerPlayer player, double x, double y, double z,
        IReadOnlySet<RelativeFlag> relatives, float yaw, float pitch, LookAt? lookAt)
    {
        var relX = x - (relatives.Contains(RelativeFlag.X) ? player.Position.X : 0);
        var relY = y - (relatives.Contains(RelativeFlag.Y) ? player.Position.Y : 0);
        var relZ = z - (relatives.Contains(RelativeFlag.Z) ? player.Position.Z : 0);
        var relYaw = yaw - (relatives.Contains(RelativeFlag.YRot) ? player.Yaw : 0f);
        var relPitch = pitch - (relatives.Contains(RelativeFlag.XRot) ? player.Pitch : 0f);

        //The server applies the absolute quantities first, maps to vanilla teleportSetPosition's calculateAbsolute
        var target = new Vec3(
            relX + (relatives.Contains(RelativeFlag.X) ? player.Position.X : 0),
            relY + (relatives.Contains(RelativeFlag.Y) ? player.Position.Y : 0),
            relZ + (relatives.Contains(RelativeFlag.Z) ? player.Position.Z : 0));
        player.Position = target;
        player.Yaw = relYaw + (relatives.Contains(RelativeFlag.YRot) ? player.Yaw : 0f);
        player.Pitch = Math.Clamp(relPitch + (relatives.Contains(RelativeFlag.XRot) ? player.Pitch : 0f), -90f, 90f);

        //The facing is applied before the position packet; the final facing is sent with the position packet, maps to vanilla lookAt.perform
        //lookAt changes the server-side facing unbeknownst to the client; the relative components must include this change's delta
        var yawBeforeLook = player.Yaw;
        var pitchBeforeLook = player.Pitch;
        ApplyLookAt(player, lookAt);

        //When YRot/XRot are present the relative amount is sent and the client adds its own current value; otherwise the absolute amount is sent
        var packetYaw = relatives.Contains(RelativeFlag.YRot) ? relYaw + (player.Yaw - yawBeforeLook) : player.Yaw;
        var packetPitch = relatives.Contains(RelativeFlag.XRot) ? relPitch + (player.Pitch - pitchBeforeLook) : player.Pitch;
        var packed = RelativeFlags.Pack(relatives);

        var listener = player.Listener;
        if (listener is not null)
        {
            listener.Teleport(target, player.Yaw, player.Pitch, relX, relY, relZ, packetYaw, packetPitch, packed);
            return;
        }
        //Without a listener (a test fake connection) it degrades to sending directly; position and facing have already landed in the player state
        player.Connection.Send(new ClientboundPlayerPositionPacket(
            relX, relY, relZ, packetYaw, packetPitch, packed, _teleportId++));
    }

    //PerformEntityTeleport level entities have no position confirmation flow; after the change the entity tracker re-sends the move packet
    private static void PerformEntityTeleport(CommandTarget target, double x, double y, double z, float yaw, float pitch, LookAt? lookAt)
    {
        var position = new Vec3(x, y, z);
        if (lookAt is not null)
        {
            //The facing is computed from the landing point, consistent with vanilla moving the entity first then turning the head
            var (lookYaw, lookPitch) = LookAngles(position, lookAt.TargetPoint());
            yaw = lookYaw;
            pitch = lookPitch;
        }
        target.WorldEntity!.SetPos(position, yaw, pitch);
    }

    //ApplyLookAt applies the facing to the player; an empty target point changes nothing
    private static void ApplyLookAt(ServerPlayer player, LookAt? lookAt)
    {
        if (lookAt is null) return;
        var (yaw, pitch) = LookAngles(player.Position, lookAt.TargetPoint());
        player.Yaw = yaw;
        player.Pitch = pitch;
    }

    //AnchorPosition resolves the world coordinate of the facing target at the anchor
    //Players use the fixed standing eye height; level entities have no separate eye height data, approximated at 90 percent of the bounding box height
    private static Vec3 AnchorPosition(EntityAnchorArgument.Anchor anchor, CommandTarget target)
    {
        if (target.Player is { } player) return EntityAnchorArgument.Apply(anchor, player);
        if (anchor == EntityAnchorArgument.Anchor.Feet) return target.Position;
        var box = target.BoundingBox;
        return new Vec3(target.Position.X, box.Min.Y + box.Size.Y * 0.9, target.Position.Z);
    }

    //LookAngles the angle from the from point looking at the to point, maps to vanilla Entity.lookAt
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

    //WrapDegrees normalizes the angle to [-180,180), maps to vanilla Mth.wrapDegrees
    private static float WrapDegrees(float value)
    {
        value %= 360;
        if (value >= 180) value -= 360;
        if (value < -180) value += 360;
        return value;
    }

    //SendTeleportSuccess reports the teleport result; a single target is named, multiple report the count
    private static void SendTeleportSuccess(ServerCommandSource source, IReadOnlyList<CommandTarget> targets, Vec3 pos)
    {
        if (targets.Count == 1)
            source.SendSuccess($"teleported {targets[0].Name} to {FormatDouble(pos.X)} {FormatDouble(pos.Y)} {FormatDouble(pos.Z)}");
        else
            source.SendSuccess($"teleported {targets.Count} entities to {FormatDouble(pos.X)} {FormatDouble(pos.Y)} {FormatDouble(pos.Z)}");
    }

    //FormatDouble six decimals, maps to vanilla String.format %f
    private static string FormatDouble(double value) => value.ToString("F6");
}
