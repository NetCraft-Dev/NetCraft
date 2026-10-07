using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Registry;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ResourceArgument registry resource argument, maps to vanilla ResourceArgument
//Holds the target registry key and parses the identifier; validity is checked against the registry at command execution
public sealed class ResourceArgument : ArgumentType<Identifier>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "012" };

    public static readonly DynamicCommandExceptionType ErrorResourceNotFound =
        new(id => new LiteralMessage($"unknown resource {id}"));

    //RegistryKey target registry identifier, such as minecraft:world_clock
    public Identifier RegistryKey { get; }

    public ResourceArgument(Identifier registryKey)
    {
        RegistryKey = registryKey;
    }

    public Identifier Parse(StringReader reader) => IdentifierArgument.ReadIdentifier(reader);

    //GetResource gets the parsed resource identifier
    public static Identifier GetResource(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Identifier>(name);

    //GetClock fetches the Holder from the world_clock registry; throws resource-not-found like vanilla when not found
    public static Holder<WorldClock> GetClock(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.WORLD_CLOCK.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    //GetTimeline fetches the Holder from the timeline registry; throws resource-not-found like vanilla when not found
    public static Holder<Timeline> GetTimeline(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.TIMELINE.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    //GetBiome fetches the Holder from the biome registry; throws resource-not-found like vanilla when not found
    public static Holder<Biome> GetBiome(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.BIOME.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    //GetMobEffect fetches the Holder from the mob_effect registry; throws resource-not-found like vanilla when not found
    public static Holder<NetCraft.Registry.MobEffect> GetMobEffect(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.MOB_EFFECT.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
