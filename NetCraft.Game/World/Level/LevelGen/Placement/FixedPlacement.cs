using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//FixedPlacement 固定位置放置对应原版 FixedPlacement
//只保留与原点同一区块的预设位置
public sealed class FixedPlacement : PlacementModifier
{
    public static readonly Codec<FixedPlacement> Codec =
        new SingleFieldPlacementCodec<FixedPlacement, IReadOnlyList<BlockPos>>(
            BlockPosCodec.Instance.ListOf().FieldOf("positions"),
            positions => new FixedPlacement(positions),
            placement => placement.Positions);

    public IReadOnlyList<BlockPos> Positions { get; }

    private FixedPlacement(IReadOnlyList<BlockPos> positions) => Positions = positions;

    //Of 构造入口对应原版 of
    public static FixedPlacement Of(params BlockPos[] positions) => new(positions);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var chunkX = origin.X >> 4;
        var chunkZ = origin.Z >> 4;
        return Positions.Where(pos => (pos.X >> 4) == chunkX && (pos.Z >> 4) == chunkZ);
    }

    public override PlacementModifierType Type => FixedPlacementType.Instance;
}

//FixedPlacementType 对应原版 PlacementModifierType.FIXED_PLACEMENT
public sealed class FixedPlacementType : PlacementModifierType<FixedPlacement>
{
    public static readonly FixedPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("fixed_placement"), new FixedPlacementType());

    private FixedPlacementType()
        : base(Identifier.WithDefaultNamespace("fixed_placement"), FixedPlacement.Codec) { }
}

//BlockPosCodec 方块位置编解码对应原版 BlockPos.CODEC
//JSON 形态是 [x, y, z] 三个整数
internal sealed class BlockPosCodec : ScalarCodec<BlockPos>
{
    public static readonly BlockPosCodec Instance = new();

    public override DataResult<BlockPos> Parse<U>(DynamicOps<U> ops, U input)
    {
        var streamResult = ops.GetStream(input);
        if (!streamResult.Result().IsPresent)
            return DataResult<BlockPos>.Error(() => "方块位置必须是 [x, y, z] 数组");
        var components = new List<int>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var numberResult = ops.GetNumberValue(element);
            if (!numberResult.Result().IsPresent)
                return DataResult<BlockPos>.Error(() => "方块位置分量必须是整数");
            components.Add((int)numberResult.GetOrThrow());
        }
        if (components.Count != 3)
            return DataResult<BlockPos>.Error(() => $"方块位置需要 3 个分量 实际 {components.Count}");
        return DataResult<BlockPos>.Success(new BlockPos(components[0], components[1], components[2]));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, BlockPos value)
        => DataResult<U>.Success(ops.CreateIntList(new[] { value.X, value.Y, value.Z }));
}
