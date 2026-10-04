using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Util;

namespace NetCraft.Game.Commands;

//WorldBorderCommand worldborder 命令对应原版 net.minecraft.server.commands.WorldBorderCommand
//set/add 改尺寸 center 改中心 damage 改越界伤害与缓冲 warning 改预警 get 查当前尺寸
public static class WorldBorderCommand
{
    private static readonly SimpleCommandExceptionType ErrorSameCenter =
        new(new LiteralMessage("边界中心未变化"));

    private static readonly SimpleCommandExceptionType ErrorSameSize =
        new(new LiteralMessage("边界尺寸未变化"));

    private static readonly SimpleCommandExceptionType ErrorTooSmall =
        new(new LiteralMessage("边界尺寸不能小于 1.0"));

    private static readonly SimpleCommandExceptionType ErrorTooBig =
        new(new LiteralMessage($"边界尺寸不能大于 {NetCraft.Storage.WorldBorder.MaxSize}"));

    private static readonly SimpleCommandExceptionType ErrorTooFarOut =
        new(new LiteralMessage($"边界中心不能超出 {NetCraft.Storage.WorldBorder.MaxCenterCoordinate}"));

    private static readonly SimpleCommandExceptionType ErrorSameWarningTime =
        new(new LiteralMessage("边界预警时间未变化"));

    private static readonly SimpleCommandExceptionType ErrorSameWarningDistance =
        new(new LiteralMessage("边界预警距离未变化"));

    private static readonly SimpleCommandExceptionType ErrorSameDamageBuffer =
        new(new LiteralMessage("越界免伤缓冲未变化"));

    private static readonly SimpleCommandExceptionType ErrorSameDamageAmount =
        new(new LiteralMessage("每格越界伤害未变化"));

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

    //Border 取执行者所在关卡的世界边界
    private static NetCraft.Storage.WorldBorder Border(CommandContext<CommandSourceStack> context)
        => ((ServerCommandSource)context.GetSource()).PlayerOrThrow.Level.WorldBorder;

    //SetSize 设置边界尺寸 非零刻数走插值 对应原版 setSize
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
                ? $"边界将在 {FormatTicksToSeconds(ticks)} 秒内扩大到 {distance:0.0}"
                : $"边界将在 {FormatTicksToSeconds(ticks)} 秒内缩小到 {distance:0.0}");
        }
        else
        {
            border.SetSize(distance);
            source.SendSuccess($"边界尺寸已设为 {distance:0.0}");
        }
        return (int)(distance - current);
    }

    //SetCenter 设置边界中心 对应原版 setCenter
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
        source.SendSuccess($"边界中心已设为 {x:0.00} {z:0.00}");
        return 0;
    }

    //SetWarningTime 设置越界预警提前刻数 对应原版 setWarningTime
    private static int SetWarningTime(ServerCommandSource source, int ticks)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.WarningTime == ticks) throw ErrorSameWarningTime.Create();
        border.SetWarningTime(ticks);
        source.SendSuccess($"边界预警时间已设为 {FormatTicksToSeconds(ticks)} 秒");
        return ticks;
    }

    //SetWarningDistance 设置越界预警距离 对应原版 setWarningDistance
    private static int SetWarningDistance(ServerCommandSource source, int distance)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.WarningBlocks == distance) throw ErrorSameWarningDistance.Create();
        border.SetWarningBlocks(distance);
        source.SendSuccess($"边界预警距离已设为 {distance}");
        return distance;
    }

    //SetDamageBuffer 设置越界免伤缓冲 对应原版 setDamageBuffer
    private static int SetDamageBuffer(ServerCommandSource source, float distance)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.SafeZone == distance) throw ErrorSameDamageBuffer.Create();
        border.SetSafeZone(distance);
        source.SendSuccess($"越界免伤缓冲已设为 {distance:0.00}");
        return (int)distance;
    }

    //SetDamageAmount 设置每格越界伤害 对应原版 setDamageAmount
    private static int SetDamageAmount(ServerCommandSource source, float damagePerBlock)
    {
        var border = source.PlayerOrThrow.Level.WorldBorder;
        if (border.DamagePerBlock == damagePerBlock) throw ErrorSameDamageAmount.Create();
        border.SetDamagePerBlock(damagePerBlock);
        source.SendSuccess($"每格越界伤害已设为 {damagePerBlock:0.00}");
        return (int)damagePerBlock;
    }

    //GetSize 回执当前边界尺寸 对应原版 getSize
    private static int GetSize(ServerCommandSource source)
    {
        var size = source.PlayerOrThrow.Level.WorldBorder.GetSize();
        source.SendSuccess($"当前边界尺寸为 {size:0}");
        return Mth.Floor(size + 0.5);
    }

    //FormatTicksToSeconds 刻数折算成秒并保留两位
    private static string FormatTicksToSeconds(long ticks) => (ticks / 20.0).ToString("0.00");
}
