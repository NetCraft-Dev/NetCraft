using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//MatchingBlockTagPredicate 匹配方块标签对应原版 MatchingBlockTagPredicate
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
        //标签未绑定(数据未装载)时按不匹配处理 免得 Holder.Is 抛未绑定标签异常
        var set = BuiltInRegistries.BLOCK.Get(_tag);
        if (set is null || !set.IsBound) return false;
        return set.Contains(BuiltInRegistries.BLOCK.WrapAsHolder(state.Owner));
    }

    public override BlockPredicateType Type => BlockPredicateType.MatchingBlockTag;
}
