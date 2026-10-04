using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//TimeArgument 时间参数对应原版 TimeArgument
//float数值加单位后缀 d天s秒t刻 无后缀按刻 解析成整数tick
public sealed class TimeArgument(int minimum) : ArgumentType<int>
{
    //Units 单位到tick换算 空串表示无后缀按刻
    private static readonly IReadOnlyDictionary<string, int> Units = new Dictionary<string, int>
    {
        ["d"] = 24000,
        ["s"] = 20,
        ["t"] = 1,
        [""] = 1,
    };

    private static readonly IReadOnlyList<string> ExamplesList = new[] { "0d", "0s", "0t", "0" };

    public static readonly SimpleCommandExceptionType ErrorInvalidUnit =
        new(new TranslatableMessage("argument.time.invalid_unit"));

    public static readonly Dynamic2CommandExceptionType ErrorTickCountTooLow =
        new((value, limit) => new TranslatableMessage("argument.time.tick_count_too_low", value, limit));

    //Minimum 允许的最小tick数
    public int Minimum => minimum;

    public static TimeArgument Time() => new(0);
    public static TimeArgument Time(int minimum) => new(minimum);

    public int Parse(StringReader reader)
    {
        var value = reader.ReadFloat();
        var unit = reader.ReadUnquotedString();
        if (!Units.TryGetValue(unit, out var factor) || factor == 0)
            throw ErrorInvalidUnit.CreateWithContext(reader);
        //对齐Java Math.round的floor(x+0.5)取整
        var ticks = (int)MathF.Floor(value * factor + 0.5f);
        if (ticks < minimum)
            throw ErrorTickCountTooLow.CreateWithContext(reader, ticks, minimum);
        return ticks;
    }

    //ListSuggestions 数值部分合法后建议单位后缀 空串后缀不进建议
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var reader = new StringReader(builder.Remaining);
        try
        {
            reader.ReadFloat();
        }
        catch (CommandSyntaxException)
        {
            return builder.BuildFuture();
        }
        var offset = builder.CreateOffset(builder.Start + reader.Cursor);
        foreach (var unit in Units.Keys)
            if (unit.Length > 0)
                offset.Add(unit);
        return offset.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
