using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//BlockPredicateFilter 方块谓词过滤对应原版 BlockPredicateFilter
//谓词在该位置成立才保留
public sealed class BlockPredicateFilter : PlacementFilter
{
    public static readonly Codec<BlockPredicateFilter> Codec =
        new SingleFieldPlacementCodec<BlockPredicateFilter, BlockPredicate>(
            BlockPredicate.Codec.FieldOf("predicate"),
            predicate => new BlockPredicateFilter(predicate),
            filter => filter.Predicate);

    public BlockPredicate Predicate { get; }

    private BlockPredicateFilter(BlockPredicate predicate) => Predicate = predicate;

    //ForPredicate 构造入口对应原版 forPredicate
    public static BlockPredicateFilter ForPredicate(BlockPredicate predicate) => new(predicate);

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
        => Predicate.Test(context.Level, origin);

    public override PlacementModifierType Type => BlockPredicateFilterType.Instance;
}

//BlockPredicateFilterType 对应原版 PlacementModifierType.BLOCK_PREDICATE_FILTER
public sealed class BlockPredicateFilterType : PlacementModifierType<BlockPredicateFilter>
{
    public static readonly BlockPredicateFilterType Instance = Register(
        Identifier.WithDefaultNamespace("block_predicate_filter"), new BlockPredicateFilterType());

    private BlockPredicateFilterType()
        : base(Identifier.WithDefaultNamespace("block_predicate_filter"), BlockPredicateFilter.Codec) { }
}
