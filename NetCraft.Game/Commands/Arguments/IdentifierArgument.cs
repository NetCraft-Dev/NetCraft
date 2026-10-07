using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Registry;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//IdentifierArgument identifier argument, maps to vanilla IdentifierArgument
//Greedily reads identifier-legal characters then parses by the namespace:path rule
public sealed class IdentifierArgument : ArgumentType<Identifier>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "012" };

    public static readonly SimpleCommandExceptionType ErrorInvalid =
        new(new TranslatableMessage("argument.id.invalid"));

    public static IdentifierArgument Id() => new();

    public Identifier Parse(StringReader reader) => ReadIdentifier(reader);

    //ReadIdentifier greedily reads identifier characters and parses; on failure rolls back the cursor and throws
    public static Identifier ReadIdentifier(StringReader reader)
    {
        var start = reader.Cursor;
        while (reader.CanRead() && Identifier.IsAllowedInIdentifier(reader.Peek()))
            reader.Skip();
        var raw = reader.String[start..reader.Cursor];
        try
        {
            return Identifier.Parse(raw);
        }
        catch (IdentifierException)
        {
            reader.SetCursor(start);
            throw ErrorInvalid.CreateWithContext(reader);
        }
    }

    //GetId gets the parse result
    public static Identifier GetId(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Identifier>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
