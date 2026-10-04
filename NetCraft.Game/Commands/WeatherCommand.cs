using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Level;
using IntProvider = NetCraft.Game.World.Level.LevelGen.IntProvider;

namespace NetCraft.Game.Commands;

//WeatherCommand weather 命令对应原版 net.minecraft.server.commands.WeatherCommand
//clear/rain/thunder 三支 可选 duration 缺省时按对应时序常量采样
public static class WeatherCommand
{
    //DefaultTime 未给时长时的哨兵值 对应原版 DEFAULT_TIME
    private const int DefaultTime = -1;

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("weather")
            .Requires(s => s.HasPermission(2))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("clear")
                .Executes(c => SetClear((ServerCommandSource)c.GetSource(), DefaultTime))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("duration", TimeArgument.Time(1))
                    .Executes(c => SetClear((ServerCommandSource)c.GetSource(),
                        IntegerArgumentType.GetInteger(c, "duration")))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("rain")
                .Executes(c => SetRain((ServerCommandSource)c.GetSource(), DefaultTime))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("duration", TimeArgument.Time(1))
                    .Executes(c => SetRain((ServerCommandSource)c.GetSource(),
                        IntegerArgumentType.GetInteger(c, "duration")))))
            .Then(LiteralArgumentBuilder<CommandSourceStack>.Literal("thunder")
                .Executes(c => SetThunder((ServerCommandSource)c.GetSource(), DefaultTime))
                .Then(RequiredArgumentBuilder<CommandSourceStack, int>.Argument("duration", TimeArgument.Time(1))
                    .Executes(c => SetThunder((ServerCommandSource)c.GetSource(),
                        IntegerArgumentType.GetInteger(c, "duration"))))));
    }

    //GetDuration 未给时长时按默认分布采样 对应原版 getDuration
    private static int GetDuration(ServerCommandSource source, int input, IntProvider defaultDistribution)
        => input == DefaultTime ? defaultDistribution.Sample(source.PlayerOrThrow.Level.Random) : input;

    //SetClear 放晴指定刻数 对应原版 setClear
    private static int SetClear(ServerCommandSource source, int duration)
    {
        source.Server.SetWeatherParameters(GetDuration(source, duration, WeatherCycle.RainDelay), 0, false, false);
        source.SendSuccess("已将天气设为晴朗");
        return duration;
    }

    //SetRain 下雨指定刻数 对应原版 setRain
    private static int SetRain(ServerCommandSource source, int duration)
    {
        source.Server.SetWeatherParameters(0, GetDuration(source, duration, WeatherCycle.RainDuration), true, false);
        source.SendSuccess("已将天气设为下雨");
        return duration;
    }

    //SetThunder 雷暴指定刻数 对应原版 setThunder
    private static int SetThunder(ServerCommandSource source, int duration)
    {
        source.Server.SetWeatherParameters(0, GetDuration(source, duration, WeatherCycle.ThunderDuration), true, true);
        source.SendSuccess("已将天气设为雷暴");
        return duration;
    }
}
