using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//EnvironmentScanPlacement environment scan placement, maps to vanilla EnvironmentScanPlacement
//Searches step by step along the vertical direction, keeping only positions that meet the target condition
public sealed class EnvironmentScanPlacement : PlacementModifier
{
    //VerticalDirection vertical direction validation, maps to vanilla Direction.VERTICAL_CODEC
    private static readonly Codec<Direction> VerticalDirection = DirectionCodec.Instance.ComapFlatMap(
        direction => direction.StepY != 0
            ? DataResult<Direction>.Success(direction)
            : DataResult<Direction>.Error(() => "direction_of_search must be up or down"),
        direction => direction);

    //MaxSteps step range validation, maps to vanilla Codec.intRange(1, 32)
    private static readonly Codec<int> MaxSteps = Codecs.Int.ComapFlatMap(
        value => value is >= 1 and <= 32
            ? DataResult<int>.Success(value)
            : DataResult<int>.Error(() => $"max_steps must be within 1..32: {value}"),
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

    //MaxStepsCount max step count; the property name avoids the MaxSteps codec field above
    public int MaxStepsCount { get; }

    private EnvironmentScanPlacement(Direction directionOfSearch, BlockPredicate targetCondition,
        BlockPredicate allowedSearchCondition, int maxSteps)
    {
        DirectionOfSearch = directionOfSearch;
        TargetCondition = targetCondition;
        AllowedSearchCondition = allowedSearchCondition;
        MaxStepsCount = maxSteps;
    }

    //ScanningFor construction entry, maps to vanilla scanningFor
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

//EnvironmentScanPlacementType, maps to vanilla PlacementModifierType.ENVIRONMENT_SCAN
public sealed class EnvironmentScanPlacementType : PlacementModifierType<EnvironmentScanPlacement>
{
    public static readonly EnvironmentScanPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("environment_scan"), new EnvironmentScanPlacementType());

    private EnvironmentScanPlacementType()
        : base(Identifier.WithDefaultNamespace("environment_scan"), EnvironmentScanPlacement.Codec) { }
}
