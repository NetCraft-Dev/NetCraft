using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//InSquarePlacement 区块内散布对应原版 InSquarePlacement
//在原点所在区块的 16x16 内随机取一点
public sealed class InSquarePlacement : PlacementModifier
{
    public static readonly InSquarePlacement Instance = new();

    public static readonly Codec<InSquarePlacement> Codec = new UnitPlacementCodec<InSquarePlacement>(() => Instance);

    private InSquarePlacement() { }

    //Spread 构造入口对应原版 spread
    public static InSquarePlacement Spread() => Instance;

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
        => new[] { new BlockPos(random.NextInt(16) + origin.X, origin.Y, random.NextInt(16) + origin.Z) };

    public override PlacementModifierType Type => InSquarePlacementType.Instance;
}

//InSquarePlacementType 对应原版 PlacementModifierType.IN_SQUARE
public sealed class InSquarePlacementType : PlacementModifierType<InSquarePlacement>
{
    public static readonly InSquarePlacementType Instance = Register(
        Identifier.WithDefaultNamespace("in_square"), new InSquarePlacementType());

    private InSquarePlacementType()
        : base(Identifier.WithDefaultNamespace("in_square"), InSquarePlacement.Codec) { }
}
