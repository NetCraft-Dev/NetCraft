using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//FixedPlacement fixed placement, maps to vanilla FixedPlacement
//Keeps only the preset positions in the same chunk as the origin
public sealed class FixedPlacement : PlacementModifier
{
    public static readonly Codec<FixedPlacement> Codec =
        new SingleFieldPlacementCodec<FixedPlacement, IReadOnlyList<BlockPos>>(
            BlockPosCodec.Instance.ListOf().FieldOf("positions"),
            positions => new FixedPlacement(positions),
            placement => placement.Positions);

    public IReadOnlyList<BlockPos> Positions { get; }

    private FixedPlacement(IReadOnlyList<BlockPos> positions) => Positions = positions;

    //Of construction entry, maps to vanilla of
    public static FixedPlacement Of(params BlockPos[] positions) => new(positions);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var chunkX = origin.X >> 4;
        var chunkZ = origin.Z >> 4;
        return Positions.Where(pos => (pos.X >> 4) == chunkX && (pos.Z >> 4) == chunkZ);
    }

    public override PlacementModifierType Type => FixedPlacementType.Instance;
}

//FixedPlacementType, maps to vanilla PlacementModifierType.FIXED_PLACEMENT
public sealed class FixedPlacementType : PlacementModifierType<FixedPlacement>
{
    public static readonly FixedPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("fixed_placement"), new FixedPlacementType());

    private FixedPlacementType()
        : base(Identifier.WithDefaultNamespace("fixed_placement"), FixedPlacement.Codec) { }
}

//BlockPosCodec block position codec, maps to vanilla BlockPos.CODEC
//JSON form is three ints [x, y, z]
internal sealed class BlockPosCodec : ScalarCodec<BlockPos>
{
    public static readonly BlockPosCodec Instance = new();

    public override DataResult<BlockPos> Parse<U>(DynamicOps<U> ops, U input)
    {
        var streamResult = ops.GetStream(input);
        if (!streamResult.Result().IsPresent)
            return DataResult<BlockPos>.Error(() => "block position must be an [x, y, z] array");
        var components = new List<int>();
        foreach (var element in streamResult.GetOrThrow())
        {
            var numberResult = ops.GetNumberValue(element);
            if (!numberResult.Result().IsPresent)
                return DataResult<BlockPos>.Error(() => "block position components must be integers");
            components.Add((int)numberResult.GetOrThrow());
        }
        if (components.Count != 3)
            return DataResult<BlockPos>.Error(() => $"block position needs 3 components, got {components.Count}");
        return DataResult<BlockPos>.Success(new BlockPos(components[0], components[1], components[2]));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, BlockPos value)
        => DataResult<U>.Success(ops.CreateIntList(new[] { value.X, value.Y, value.Z }));
}
