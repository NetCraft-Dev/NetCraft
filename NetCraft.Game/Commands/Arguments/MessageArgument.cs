using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//MessageArgument chat message argument, maps to vanilla net.minecraft.commands.arguments.MessageArgument
//Takes all text to the end of the command; max length 256, maps to vanilla TOO_LONG
//Registered at network id 20 (message); the client tokenizes with the vanilla parser by the same id
public sealed class MessageArgument : ArgumentType<string>
{
    //MaxLength message length limit, maps to vanilla 256
    private const int MaxLength = 256;

    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "Hello world!", "foo", "@e", "Hello @p :)" };

    private static readonly MessageArgument Instance = new();

    public static MessageArgument Message() => Instance;

    public string Parse(StringReader reader)
    {
        var remaining = reader.Remaining;
        if (remaining.Length > MaxLength) throw ErrorTooLong.Create(remaining.Length, MaxLength);
        reader.SetCursor(reader.TotalLength);
        return remaining;
    }

    //ErrorTooLong message too long, maps to vanilla TOO_LONG
    private static readonly Dynamic2CommandExceptionType ErrorTooLong =
        new((length, max) => new LiteralMessage($"message length {length} exceeds the limit {max}"));

    //GetMessage gets the parsed message text, maps to vanilla getMessage
    //Vanilla expands @selectors into entity name components; NC has no such parsing and uses plain text directly
    public static string GetMessage(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<string>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
