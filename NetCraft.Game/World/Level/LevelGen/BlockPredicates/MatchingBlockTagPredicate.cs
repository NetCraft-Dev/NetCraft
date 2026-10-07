using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//MatchingBlockTagPredicate matches a block tag, maps to vanilla MatchingBlockTagPredicate
public class MatchingBlockTagPredicate : StateTestingPredicate
{
    public static readonly Codec<MatchingBlockTagPredicate> Codec =
        RecordCodecBuilder.Of2<MatchingBlockTagPredicate, Vec3i, TagKey<RegBlock>>(
            StateTestingCodec().ForGetter<MatchingBlockTagPredicate, Vec3i>(p => p.Offset),
            new TagKeyCodec<RegBlock>(BuiltInRegistries.BLOCK).FieldOf("tag")
                .ForGetter<MatchingBlockTagPredicate, TagKey<RegBlock>>(p => p._tag),
            (offset, tag) => new MatchingBlockTagPredicate(offset, tag));

    private readonly TagKey<RegBlock> _tag;

    public MatchingBlockTagPredicate(Vec3i offset, TagKey<RegBlock> tag) : base(offset) => _tag = tag;

    public TagKey<RegBlock> Tag => _tag;

    protected override bool Test(BlockState state)
    {
        //While the tag is unbound (data not loaded) treat it as no match, so Holder.Is does not throw on an unbound tag
        var set = BuiltInRegistries.BLOCK.Get(_tag);
        if (set is null || !set.IsBound) return false;
        return set.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    public override BlockPredicateType Type => BlockPredicateType.MatchingBlockTag;
}
