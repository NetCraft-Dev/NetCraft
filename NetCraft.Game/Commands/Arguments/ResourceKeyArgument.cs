using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Registry;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ResourceKeyArgument registry key argument, maps to vanilla net.minecraft.commands.arguments.ResourceKeyArgument
//Key difference from ResourceArgument: the network descriptor only writes the registry id, and the client builds the key from the identifier without looking up the registry
//So it can reference registries the client lacks at the play stage (e.g. recipe is data-driven and not in the sync list)
//ResourceArgument is the opposite: the client instantiates with lookupOrThrow on that registry and crashes when the registry is missing
public sealed class ResourceKeyArgument : ArgumentType<Identifier>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "012" };

    //RegistryKey target registry identifier, used only for network transfer and suggestions; server parsing itself does not validate it
    public Identifier RegistryKey { get; }

    public ResourceKeyArgument(Identifier registryKey)
    {
        RegistryKey = registryKey;
    }

    public Identifier Parse(StringReader reader) => IdentifierArgument.ReadIdentifier(reader);

    //GetResource gets the parsed resource identifier
    public static Identifier GetResource(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Identifier>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
