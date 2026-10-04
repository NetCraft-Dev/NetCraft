using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//MatchingBlocksPredicate 匹配方块集合对应原版 MatchingBlocksPredicate
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
        //标签未绑定(数据未装载)时按不匹配处理 免得 Holder.Is 抛未绑定标签异常
        if (!_blocks.IsBound) return false;
        return _blocks.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    public override BlockPredicateType Type => BlockPredicateType.MatchingBlocks;
}
