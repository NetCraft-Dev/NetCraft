using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//HeightmapPlacement 高度图放置对应原版 HeightmapPlacement
//把位置抬到指定高度图的高度
public sealed class HeightmapPlacement : PlacementModifier
{
    public static readonly Codec<HeightmapPlacement> Codec =
        new SingleFieldPlacementCodec<HeightmapPlacement, Heightmap.Types>(
            HeightmapTypesCodec.Instance.FieldOf("heightmap"),
            heightmap => new HeightmapPlacement(heightmap),
            placement => placement.Heightmap);

    public Heightmap.Types Heightmap { get; }

    private HeightmapPlacement(Heightmap.Types heightmap) => Heightmap = heightmap;

    //OnHeightmap 构造入口对应原版 onHeightmap
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

//HeightmapPlacementType 对应原版 PlacementModifierType.HEIGHTMAP
public sealed class HeightmapPlacementType : PlacementModifierType<HeightmapPlacement>
{
    public static readonly HeightmapPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("heightmap"), new HeightmapPlacementType());

    private HeightmapPlacementType()
        : base(Identifier.WithDefaultNamespace("heightmap"), HeightmapPlacement.Codec) { }
}
