using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//InsideWorldBoundsPredicate 在世界高度范围内对应原版 InsideWorldBoundsPredicate
public class InsideWorldBoundsPredicate : BlockPredicate
{
    public static readonly Codec<InsideWorldBoundsPredicate> Codec =
        new SingleFieldMapCodec<InsideWorldBoundsPredicate, Vec3i>(
            Vec3iCodec.Offset16.OptionalFieldOf("offset", Vec3i.Zero),
            offset => new InsideWorldBoundsPredicate(offset), p => p.Offset);

    private readonly Vec3i _offset;

    public InsideWorldBoundsPredicate(Vec3i offset) => _offset = offset;

    public Vec3i Offset => _offset;

    public override bool Test(WorldGenRegion level, BlockPos origin)
    {
        var y = origin.Offset(_offset).Y;
        var heightAccessor = (LevelHeightAccessor)level;
        return y >= heightAccessor.MinBuildHeight && y < heightAccessor.MaxBuildHeight;
    }

    public override BlockPredicateType Type => BlockPredicateType.InsideWorldBounds;
}
