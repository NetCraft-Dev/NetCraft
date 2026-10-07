using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//TimeArgument time argument, maps to vanilla TimeArgument
//A float value with an optional unit suffix d days s seconds t ticks; without a suffix it is ticks; parsed into integer ticks
public sealed class TimeArgument(int minimum) : ArgumentType<int>
{
    //Units unit-to-tick conversion; an empty string means no suffix, in ticks
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

    //Minimum minimum allowed tick count
    public int Minimum => minimum;

    public static TimeArgument Time() => new(0);
    public static TimeArgument Time(int minimum) => new(minimum);

    public int Parse(StringReader reader)
    {
        var value = reader.ReadFloat();
        var unit = reader.ReadUnquotedString();
        if (!Units.TryGetValue(unit, out var factor) || factor == 0)
            throw ErrorInvalidUnit.CreateWithContext(reader);
        //Matches Java Math.round's floor(x+0.5) rounding
        var ticks = (int)MathF.Floor(value * factor + 0.5f);
        if (ticks < minimum)
            throw ErrorTickCountTooLow.CreateWithContext(reader, ticks, minimum);
        return ticks;
    }

    //ListSuggestions suggests unit suffixes once the numeric part is valid; an empty suffix is not suggested
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
