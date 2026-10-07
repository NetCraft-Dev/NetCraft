using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Particle;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ParticleArgument particle argument, maps to vanilla net.minecraft.commands.arguments.ParticleArgument
//Syntax: <particle name>[{parameters}]; the parameter section uses SNBT with field names matching the network encoding
//Particles whose parameters depend on unported subsystems are rejected outright, so no unencodable option is constructed
public sealed class ParticleArgument : ArgumentType<ParticleOptions>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "particle{foo:bar}" };

    public static readonly DynamicCommandExceptionType ErrorUnknownParticle =
        new(name => new LiteralMessage($"unknown particle {name}"));

    public static readonly DynamicCommandExceptionType ErrorUnsupportedParticle =
        new(name => new LiteralMessage($"particle not supported yet: {name}"));

    public static readonly DynamicCommandExceptionType ErrorMissingOptions =
        new(name => new LiteralMessage($"particle {name} is missing a required parameter"));

    public static readonly DynamicCommandExceptionType ErrorInvalidOptions =
        new(name => new LiteralMessage($"invalid parameter for particle {name}"));

    public static ParticleArgument Particle() => new();

    public ParticleOptions Parse(StringReader reader)
    {
        var start = reader.Cursor;
        try
        {
            return ParseInternal(reader);
        }
        catch (CommandSyntaxException)
        {
            reader.SetCursor(start);
            throw;
        }
    }

    private static ParticleOptions ParseInternal(StringReader reader)
    {
        var id = IdentifierArgument.ReadIdentifier(reader);
        var type = ParticleTypes.Find(id);
        if (type is null) throw ErrorUnknownParticle.Create(id);
        if (type is UnsupportedParticleType) throw ErrorUnsupportedParticle.Create(id);

        CompoundTag? options = null;
        if (reader.CanRead() && reader.Peek() == '{')
        {
            var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
            try
            {
                options = TagParser<Tag>.ParseCompoundAsArgument(nbtReader);
            }
            catch (CommandSyntaxException)
            {
                throw ErrorInvalidOptions.Create(id);
            }
            reader.SetCursor(nbtReader.Cursor);
        }
        return CreateOptions(type, options, id);
    }

    //CreateOptions assembles the options by particle type; parameterless types ignore the parameter section
    private static ParticleOptions CreateOptions(ParticleType type, CompoundTag? options, Identifier id)
    {
        switch (type)
        {
            case SimpleParticleType simple:
                return simple.Options;
            case BlockParticleType block:
                return new BlockParticleOption(block, ReadBlockState(Require(options, id), id));
            case ItemParticleType item:
                return new ItemParticleOption(item, ReadItem(Require(options, id), id));
            case DustParticleType dust:
                return new DustParticleOptions(dust,
                    (int)ReadNumber(Require(options, id), "color", id),
                    (float)ReadNumber(Require(options, id), "scale", id));
            case DustColorTransitionParticleType transition:
                var nbt = Require(options, id);
                return new DustColorTransitionOptions(transition,
                    (int)ReadNumber(nbt, "from_color", id),
                    (int)ReadNumber(nbt, "to_color", id),
                    (float)ReadNumber(nbt, "scale", id));
            case ColorParticleType color:
                return new ColorParticleOption(color, (int)ReadNumber(Require(options, id), "color", id));
            default:
                throw ErrorUnsupportedParticle.Create(id);
        }
    }

    //Require particles with parameters must write the parameter section, maps to the required fields without defaults in the vanilla codec
    private static CompoundTag Require(CompoundTag? options, Identifier id)
        => options ?? throw ErrorMissingOptions.Create(id);

    //ReadBlockState the block_state field of the parameter section is block state text; reuses block state argument parsing
    private static BlockState ReadBlockState(CompoundTag options, Identifier id)
    {
        if (options.GetString("block_state") is not { } tag) throw ErrorMissingOptions.Create(id);
        try
        {
            return new BlockStateArgument().Parse(new StringReader(tag.Value)).State;
        }
        catch (CommandSyntaxException)
        {
            throw ErrorInvalidOptions.Create(id);
        }
    }

    //ReadItem the item field of the parameter section is item text; reuses item argument parsing
    private static ItemStack ReadItem(CompoundTag options, Identifier id)
    {
        if (options.GetString("item") is not { } tag) throw ErrorMissingOptions.Create(id);
        try
        {
            var input = new ItemArgument().Parse(new StringReader(tag.Value));
            return new ItemStack(input.Item.BuiltInRegistryHolder, 1, input.Components);
        }
        catch (CommandSyntaxException)
        {
            throw ErrorInvalidOptions.Create(id);
        }
    }

    //ReadNumber reads a numeric field; in SNBT 1.0 is a double and 1 is an int, both are accepted
    private static double ReadNumber(CompoundTag options, string key, Identifier id)
    {
        if (options.GetDouble(key) is { } d) return d.Value;
        if (options.GetFloat(key) is { } f) return f.Value;
        if (options.GetInt(key) is { } i) return i.Value;
        if (options.GetLong(key) is { } l) return l.Value;
        throw ErrorMissingOptions.Create(id);
    }

    //GetParticle gets the parsed particle option
    public static ParticleOptions GetParticle(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<ParticleOptions>(name);

    //ListSuggestions suggests all supported particle names, skipping types whose parameters are not ported
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        foreach (var id in BuiltInRegistries.PARTICLE_TYPE.KeySet)
        {
            if (BuiltInRegistries.PARTICLE_TYPE.GetValue(id) is not ParticleType type) continue;
            if (type is UnsupportedParticleType) continue;
            var text = id.ToShortString();
            if (text.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(text);
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
