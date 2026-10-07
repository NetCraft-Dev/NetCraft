using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//MatchingBlocksPredicate matches a block set, maps to vanilla MatchingBlocksPredicate
public class MatchingBlocksPredicate : StateTestingPredicate
{
    public static readonly Codec<MatchingBlocksPredicate> Codec =
        RecordCodecBuilder.Of2<MatchingBlocksPredicate, Vec3i, HolderSet<RegBlock>>(
            StateTestingCodec().ForGetter<MatchingBlocksPredicate, Vec3i>(p => p.Offset),
            HolderSetCodecs.BlockSet.FieldOf("blocks")
                .ForGetter<MatchingBlocksPredicate, HolderSet<RegBlock>>(p => p._blocks),
            (offset, blocks) => new MatchingBlocksPredicate(offset, blocks));

    private readonly HolderSet<RegBlock> _blocks;

    public MatchingBlocksPredicate(Vec3i offset, HolderSet<RegBlock> blocks) : base(offset) => _blocks = blocks;

    public HolderSet<RegBlock> Blocks => _blocks;

    protected override bool Test(BlockState state)
    {
        //While the tag is unbound (data not loaded) treat it as no match, so Holder.Is does not throw on an unbound tag
        if (!_blocks.IsBound) return false;
        return _blocks.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    public override BlockPredicateType Type => BlockPredicateType.MatchingBlocks;
}
