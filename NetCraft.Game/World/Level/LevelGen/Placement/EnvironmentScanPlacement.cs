using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//EnvironmentScanPlacement 环境扫描放置对应原版 EnvironmentScanPlacement
//沿竖直方向逐步搜索 命中目标条件的位置才保留
public sealed class EnvironmentScanPlacement : PlacementModifier
{
    //VerticalDirection 竖直方向校验对应原版 Direction.VERTICAL_CODEC
    private static readonly Codec<Direction> VerticalDirection = DirectionCodec.Instance.ComapFlatMap(
        direction => direction.StepY != 0
            ? DataResult<Direction>.Success(direction)
            : DataResult<Direction>.Error(() => "direction_of_search 只能是 up 或 down"),
        direction => direction);

    //MaxSteps 步数范围校验对应原版 Codec.intRange(1, 32)
    private static readonly Codec<int> MaxSteps = Codecs.Int.ComapFlatMap(
        value => value is >= 1 and <= 32
            ? DataResult<int>.Success(value)
            : DataResult<int>.Error(() => $"max_steps 必须在 1..32: {value}"),
        value => value);

    public static readonly Codec<EnvironmentScanPlacement> Codec =
        RecordCodecBuilder.Of4<EnvironmentScanPlacement, Direction, BlockPredicate, BlockPredicate, int>(
            VerticalDirection.FieldOf("direction_of_search")
                .ForGetter<EnvironmentScanPlacement, Direction>(placement => placement.DirectionOfSearch),
            BlockPredicate.Codec.FieldOf("target_condition")
                .ForGetter<EnvironmentScanPlacement, BlockPredicate>(placement => placement.TargetCondition),
            BlockPredicate.Codec.OptionalFieldOf("allowed_search_condition", BlockPredicate.AlwaysTrue())
                .ForGetter<EnvironmentScanPlacement, BlockPredicate>(placement => placement.AllowedSearchCondition),
            MaxSteps.FieldOf("max_steps")
                .ForGetter<EnvironmentScanPlacement, int>(placement => placement.MaxStepsCount),
            (direction, target, allowed, maxSteps) =>
                new EnvironmentScanPlacement(direction, target, allowed, maxSteps));

    public Direction DirectionOfSearch { get; }
    public BlockPredicate TargetCondition { get; }
    public BlockPredicate AllowedSearchCondition { get; }

    //MaxStepsCount 最大步数 属性名避开上面的 MaxSteps codec 字段
    public int MaxStepsCount { get; }

    private EnvironmentScanPlacement(Direction directionOfSearch, BlockPredicate targetCondition,
        BlockPredicate allowedSearchCondition, int maxSteps)
    {
        DirectionOfSearch = directionOfSearch;
        TargetCondition = targetCondition;
        AllowedSearchCondition = allowedSearchCondition;
        MaxStepsCount = maxSteps;
    }

    //ScanningFor 构造入口对应原版 scanningFor
    public static EnvironmentScanPlacement ScanningFor(Direction directionOfSearch, BlockPredicate targetCondition,
        BlockPredicate allowedSearchCondition, int maxSteps)
        => new(directionOfSearch, targetCondition, allowedSearchCondition, maxSteps);

    public static EnvironmentScanPlacement ScanningFor(Direction directionOfSearch, BlockPredicate targetCondition,
        int maxSteps)
        => new(directionOfSearch, targetCondition, BlockPredicate.AlwaysTrue(), maxSteps);

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var level = context.Level;
        var heightAccessor = (LevelHeightAccessor)level;
        var pos = origin;
        if (!AllowedSearchCondition.Test(level, pos)) return Array.Empty<BlockPos>();
        for (var i = 0; i < MaxStepsCount; i++)
        {
            if (TargetCondition.Test(level, pos)) return new[] { pos };
            pos = pos.Offset(DirectionOfSearch);
            if (pos.Y < heightAccessor.MinBuildHeight || pos.Y >= heightAccessor.MaxBuildHeight)
                return Array.Empty<BlockPos>();
            if (!AllowedSearchCondition.Test(level, pos)) break;
        }
        return TargetCondition.Test(level, pos) ? new[] { pos } : Array.Empty<BlockPos>();
    }

    public override PlacementModifierType Type => EnvironmentScanPlacementType.Instance;
}

//EnvironmentScanPlacementType 对应原版 PlacementModifierType.ENVIRONMENT_SCAN
public sealed class EnvironmentScanPlacementType : PlacementModifierType<EnvironmentScanPlacement>
{
    public static readonly EnvironmentScanPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("environment_scan"), new EnvironmentScanPlacementType());

    private EnvironmentScanPlacementType()
        : base(Identifier.WithDefaultNamespace("environment_scan"), EnvironmentScanPlacement.Codec) { }
}
