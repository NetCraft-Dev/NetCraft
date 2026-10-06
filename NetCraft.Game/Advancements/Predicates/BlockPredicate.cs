using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.Advancements.Predicates;

//BlockPredicate block predicate, checks block type, state properties and block entity data
//maps to vanilla net.minecraft.advancements.predicates.BlockPredicate
public sealed record BlockPredicate(
    Optional<HolderSet<NetCraft.Registry.Block>> Blocks,
    Optional<StatePropertiesPredicate> Properties,
    Optional<NbtPredicate> Nbt,
    DataComponentMatchers Components)
{
    //Codec persistence codec, field names blocks/state/nbt/components, maps to vanilla CODEC
    public static readonly Codec<BlockPredicate> Codec = RecordCodecBuilder.Of4(
        HolderSetCodecs.BlockSet.OptionalFieldOf("blocks")
            .ForGetter((BlockPredicate predicate) => predicate.Blocks),
        StatePropertiesPredicate.Codec.OptionalFieldOf("state")
            .ForGetter((BlockPredicate predicate) => predicate.Properties),
        NbtPredicate.Codec.OptionalFieldOf("nbt")
            .ForGetter((BlockPredicate predicate) => predicate.Nbt),
        DataComponentMatchers.Codec.FieldOf("components")
            .ForGetter((BlockPredicate predicate) => predicate.Components),
        (blocks, properties, nbt, components) => new BlockPredicate(blocks, properties, nbt, components));

    //RequiresNbt whether this predicate needs block entity data, maps to vanilla requiresNbt
    public bool RequiresNbt => Nbt.IsPresent;

    //MatchesState both the block set and the state properties must hit, maps to vanilla matchesState
    public bool MatchesState(BlockState state)
    {
        if (Blocks.IsPresent && !Blocks.Get().Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner)))
            return false;
        if (Properties.IsPresent && !Properties.Get().Matches(state)) return false;
        return true;
    }

    //Matches the position is loaded and the state hits, maps to vanilla matches
    //nbt and component matching need block entities; the block entity system is not wired up, so such requirements are treated as unverifiable
    public bool Matches(ILevelReader level, BlockPos pos)
    {
        if (!level.IsLoaded(pos)) return false;
        var state = level.GetBlockState(pos);
        if (state is null || !MatchesState(state.Value)) return false;
        return !Nbt.IsPresent && Components.IsEmpty;
    }
}
