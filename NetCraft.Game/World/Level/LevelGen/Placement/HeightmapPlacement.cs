using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//HeightmapPlacement heightmap placement, maps to vanilla HeightmapPlacement
//Raises the position to the height of the given heightmap
public sealed class HeightmapPlacement : PlacementModifier
{
    public static readonly Codec<HeightmapPlacement> Codec =
        new SingleFieldPlacementCodec<HeightmapPlacement, Heightmap.Types>(
            HeightmapTypesCodec.Instance.FieldOf("heightmap"),
            heightmap => new HeightmapPlacement(heightmap),
            placement => placement.Heightmap);

    public Heightmap.Types Heightmap { get; }

    private HeightmapPlacement(Heightmap.Types heightmap) => Heightmap = heightmap;

    //OnHeightmap construction entry, maps to vanilla onHeightmap
    public static HeightmapPlacement OnHeightmap(Heightmap.Types heightmap) => new(heightmap);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var height = context.Level.GetHeight(Heightmap, origin.X, origin.Z);
        return height > context.GetMinGenY()
            ? new[] { new BlockPos(origin.X, height, origin.Z) }
            : Array.Empty<BlockPos>();
    }

    public override PlacementModifierType Type => HeightmapPlacementType.Instance;
}

//HeightmapPlacementType, maps to vanilla PlacementModifierType.HEIGHTMAP
public sealed class HeightmapPlacementType : PlacementModifierType<HeightmapPlacement>
{
    public static readonly HeightmapPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("heightmap"), new HeightmapPlacementType());

    private HeightmapPlacementType()
        : base(Identifier.WithDefaultNamespace("heightmap"), HeightmapPlacement.Codec) { }
}
