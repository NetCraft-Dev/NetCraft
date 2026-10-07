using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Level;
using IntProvider = NetCraft.Game.World.Level.LevelGen.IntProvider;

namespace NetCraft.Game.Commands;

//WeatherCommand weather command, maps to vanilla net.minecraft.server.commands.WeatherCommand
//Three branches clear/rain/thunder; the optional duration samples the matching timing constant when omitted
public static class WeatherCommand
{
    //DefaultTime sentinel when no duration is given, maps to vanilla DEFAULT_TIME
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

    //GetDuration samples the default distribution when no duration is given, maps to vanilla getDuration
    private static int GetDuration(ServerCommandSource source, int input, IntProvider defaultDistribution)
        => input == DefaultTime ? defaultDistribution.Sample(source.PlayerOrThrow.Level.Random) : input;

    //SetClear clears the weather for the given ticks, maps to vanilla setClear
    private static int SetClear(ServerCommandSource source, int duration)
    {
        source.Server.SetWeatherParameters(GetDuration(source, duration, WeatherCycle.RainDelay), 0, false, false);
        source.SendSuccess("weather set to clear");
        return duration;
    }

    //SetRain rains for the given ticks, maps to vanilla setRain
    private static int SetRain(ServerCommandSource source, int duration)
    {
        source.Server.SetWeatherParameters(0, GetDuration(source, duration, WeatherCycle.RainDuration), true, false);
        source.SendSuccess("weather set to rain");
        return duration;
    }

    //SetThunder thunders for the given ticks, maps to vanilla setThunder
    private static int SetThunder(ServerCommandSource source, int duration)
    {
        source.Server.SetWeatherParameters(0, GetDuration(source, duration, WeatherCycle.ThunderDuration), true, true);
        source.SendSuccess("weather set to thunder");
        return duration;
    }
}
