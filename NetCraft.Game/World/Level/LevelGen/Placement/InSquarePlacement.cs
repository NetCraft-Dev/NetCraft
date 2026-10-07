using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//InSquarePlacement scatter within the chunk, maps to vanilla InSquarePlacement
//Picks a random point inside the 16x16 of the origin's chunk
public sealed class InSquarePlacement : PlacementModifier
{
    public static readonly InSquarePlacement Instance = new();

    public static readonly Codec<InSquarePlacement> Codec = new UnitPlacementCodec<InSquarePlacement>(() => Instance);

    private InSquarePlacement() { }

    //Spread construction entry, maps to vanilla spread
    public static InSquarePlacement Spread() => Instance;

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
        => new[] { new BlockPos(random.NextInt(16) + origin.X, origin.Y, random.NextInt(16) + origin.Z) };

    public override PlacementModifierType Type => InSquarePlacementType.Instance;
}

//InSquarePlacementType, maps to vanilla PlacementModifierType.IN_SQUARE
public sealed class InSquarePlacementType : PlacementModifierType<InSquarePlacement>
{
    public static readonly InSquarePlacementType Instance = Register(
        Identifier.WithDefaultNamespace("in_square"), new InSquarePlacementType());

    private InSquarePlacementType()
        : base(Identifier.WithDefaultNamespace("in_square"), InSquarePlacement.Codec) { }
}
