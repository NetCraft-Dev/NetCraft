using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Util;

namespace NetCraft.Game.Commands;

//WorldBorderCommand worldborder command, maps to vanilla net.minecraft.server.commands.WorldBorderCommand
//set/add change the size; center changes the center; damage changes out-of-bounds damage and buffer; warning changes warnings; get queries the current size
public static class WorldBorderCommand
{
    private static readonly SimpleCommandExceptionType ErrorSameCenter =
        new(new LiteralMessage("the border center did not change"));

    private static readonly SimpleCommandExceptionType ErrorSameSize =
        new(new LiteralMessage("the border size did not change"));

    private static readonly SimpleCommandExceptionType ErrorTooSmall =
        new(new LiteralMessage("the border size cannot be smaller than 1.0"));

    private static readonly SimpleCommandExceptionType ErrorTooBig =
        new(new LiteralMessage($"the border size cannot exceed {NetCraft.Storage.WorldBorder.MaxSize}"));

    private static readonly SimpleCommandExceptionType ErrorTooFarOut =
        new(new LiteralMessage($"the border center cannot exceed {NetCraft.Storage.WorldBorder.MaxCenterCoordinate}"));

    private static readonly SimpleCommandExceptionType ErrorSameWarningTime =
        new(new LiteralMessage("the border warning time did not change"));

    private static readonly SimpleCommandExceptionType ErrorSameWarningDistance =
        new(new LiteralMessage("the border warning distance did not change"));

    private static readonly SimpleCommandExceptionType ErrorSameDamageBuffer =
        new(new LiteralMessage("the out-of-bounds damage buffer did not change"));

    private static readonly SimpleCommandExceptionType ErrorSameDamageAmount =
        new(new LiteralMessage("the per-block out-of-bounds damage did not change"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("worldborder")
            .Requires(s => s.HasPermission(2))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("add")
                .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("distance",
                        DoubleArgumentType.DoubleArg(-NetCraft.Storage.WorldBorder.MaxSize,
                            NetCraft.Storage.WorldBorder.MaxSize))
                    .Executes(c => SetSize((ServerCommandSource)c.GetSource(),
                        Border(c).GetSize() + DoubleArgumentType.GetDouble(c, "distance"), 0L))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(0))
                        .Executes(c => SetSize((ServerCommandSource)c.GetSource(),
                            Border(c).GetSize() + DoubleArgumentType.GetDouble(c, "distance"),
                            Border(c).GetLerpTime() + IntegerArgumentType.GetInteger(c, "time"))))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("set")
                .Then(RequiredArgumentBuilder<CommandSourceStack, double>.Argument("distance",
                        DoubleArgumentType.DoubleArg(-NetCraft.Storage.WorldBorder.MaxSize,
                            NetCraft.Storage.WorldBorder.MaxSize))
                    .Executes(c => SetSize((ServerCommandSource)c.GetSource(),
                        DoubleArgumentType.GetDouble(c, "distance"), 0L))
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(0))
                        .Executes(c => SetSize((ServerCommandSource)c.GetSource(),
                            DoubleArgumentType.GetDouble(c, "distance"),
                            IntegerArgumentType.GetInteger(c, "time"))))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("center")
                .Then(RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec2Argument.Vec2())
                    .Executes(SetCenter)))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("damage")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("amount")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("damagePerBlock",
                            FloatArgumentType.FloatArg(0f))
                        .Executes(c => SetDamageAmount((ServerCommandSource)c.GetSource(),
                            FloatArgumentType.GetFloat(c, "damagePerBlock")))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("buffer")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, float>.Argument("distance",
                            FloatArgumentType.FloatArg(0f))
                        .Executes(c => SetDamageBuffer((ServerCommandSource)c.GetSource(),
                            FloatArgumentType.GetFloat(c, "distance"))))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("get")
                .Executes(c => GetSize((ServerCommandSource)c.GetSource())))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("warning")
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("distance")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("distance",
                            IntegerArgumentType.Integer(0))
                        .Executes(c => SetWarningDistance((ServerCommandSource)c.GetSource(),
                            IntegerArgumentType.GetInteger(c, "distance")))))
                .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("time")
                    .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("time", TimeArgument.Time(0))
                        .Executes(c => SetWarningTime((ServerCommandSource)c.GetSource(),
                            IntegerArgumentType.GetInteger(c, "time")))))));
    }

    //Border gets the executor's level world border
    private static NetCraft.Storage.WorldBorder Border(CommandContext<CommandSourceStack> context)
        => ((ServerCommandSource)context.GetSource()).PlayerOrThrow.Level.WorldBorder;

    //SetSize sets the border size; a non-zero tick count interpolates, maps to vanilla setSize
    private static int SetSize(ServerCommandSource source, double distance, long ticks)
    {
        var level = source.PlayerOrThrow.Level;
        var border = level.WorldBorder;
        var current = border.GetSize();
        if (current == distance) throw ErrorSameSize.Create();
        if (distance < 1.0) throw ErrorTooSmall.Create();
        if (distance > NetCraft.Storage.WorldBorder.MaxSize) throw ErrorTooBig.Create();
        if (ticks > 0)
        {
            border.LerpSizeBetween(current, distance, ticks, level.GameTime);
            source.SendSuccess(distance > current
                ? $"the border will expand to {distance:0.0} in {FormatTicksToSeconds(ticks)} seconds"
                : $"the border will shrink to {distance:0.0} in {FormatTicksToSeconds(ticks)} seconds");
        }
        else
        {
            border.SetSize(distance);
            source.SendSuccess($"the border size is set to {distance:0.0}");
        }
        return (int)(distance - current);
    }

    //SetCenter sets the border center, maps to vanilla setCenter
    private static int SetCenter(CommandContext<CommandSourceStack> context)
    {
        var source = (ServerCommandSource)context.GetSource();
        var (x, z) = Vec2Argument.GetVec2(context, "pos");
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.CenterX == x && border.CenterZ == z) throw ErrorSameCenter.Create();
        if (Math.Abs(x) > NetCraft.Storage.WorldBorder.MaxCenterCoordinate
            || Math.Abs(z) > NetCraft.Storage.WorldBorder.MaxCenterCoordinate)
            throw ErrorTooFarOut.Create();
        border.SetCenter(x, z);
        source.SendSuccess($"the border center is set to {x:0.00} {z:0.00}");
        return 0;
    }

    //SetWarningTime sets the out-of-bounds warning lead ticks, maps to vanilla setWarningTime
    private static int SetWarningTime(ServerCommandSource source, int ticks)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.WarningTime == ticks) throw ErrorSameWarningTime.Create();
        border.SetWarningTime(ticks);
        source.SendSuccess($"the border warning time is set to {FormatTicksToSeconds(ticks)} seconds");
        return ticks;
    }

    //SetWarningDistance sets the out-of-bounds warning distance, maps to vanilla setWarningDistance
    private static int SetWarningDistance(ServerCommandSource source, int distance)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.WarningBlocks == distance) throw ErrorSameWarningDistance.Create();
        border.SetWarningBlocks(distance);
        source.SendSuccess($"the border warning distance is set to {distance}");
        return distance;
    }

    //SetDamageBuffer sets the out-of-bounds damage buffer, maps to vanilla setDamageBuffer
    private static int SetDamageBuffer(ServerCommandSource source, float distance)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.SafeZone == distance) throw ErrorSameDamageBuffer.Create();
        border.SetSafeZone(distance);
        source.SendSuccess($"the out-of-bounds damage buffer is set to {distance:0.00}");
        return (int)distance;
    }

    //SetDamageAmount sets the per-block out-of-bounds damage, maps to vanilla setDamageAmount
    private static int SetDamageAmount(ServerCommandSource source, float damagePerBlock)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.DamagePerBlock == damagePerBlock) throw ErrorSameDamageAmount.Create();
        border.SetDamagePerBlock(damagePerBlock);
        source.SendSuccess($"the per-block out-of-bounds damage is set to {damagePerBlock:0.00}");
        return (int)damagePerBlock;
    }

    //GetSize reports the current border size, maps to vanilla getSize
    private static int GetSize(ServerCommandSource source)
    {
        var size = source.PlayerOrThrow.Level.WorldBorder.GetSize();
        source.SendSuccess($"the current border size is {size:0}");
        return Mth.Floor(size + 0.5);
    }

    //FormatTicksToSeconds converts ticks to seconds with two decimals
    private static string FormatTicksToSeconds(long ticks) => (ticks / 20.0).ToString("0.00");
}
