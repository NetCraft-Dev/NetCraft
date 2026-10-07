using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//BlockPredicate block predicate, maps to vanilla net.minecraft.world.level.levelgen.blockpredicates.BlockPredicate
//Tests whether a position satisfies a condition; placement modifiers and features use it to filter positions
public abstract class BlockPredicate
{
    //Codec polymorphic codec entry: read the type field, then dispatch to the concrete type
    public static readonly Codec<BlockPredicate> Codec = BlockPredicateCodec.Instance;

    //AirTag the air block tag, maps to vanilla BlockTags.AIR
    public static readonly TagKey<RegBlock> AirTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("air"));

    //OnlyInAir only inside air, maps to vanilla ONLY_IN_AIR_PREDICATE
    public static readonly BlockPredicate OnlyInAir = MatchesTag(AirTag);

    //OnlyInAirOrWater only inside air or water, maps to vanilla ONLY_IN_AIR_OR_WATER_PREDICATE
    public static readonly BlockPredicate OnlyInAirOrWater = AnyOf(OnlyInAir, MatchesBlocks(Blocks.WATER));

    //Type owning type singleton; encoding and registry resolution use it to get the id
    public abstract BlockPredicateType Type { get; }

    //Test whether the position satisfies the condition, maps to vanilla test(WorldGenLevel, BlockPos)
    public abstract bool Test(WorldGenRegion level, BlockPos origin);

    public static BlockPredicate AllOf(IReadOnlyList<BlockPredicate> predicates) => new AllOfPredicate(predicates);

    public static BlockPredicate AllOf(params BlockPredicate[] predicates)
        => AllOf((IReadOnlyList<BlockPredicate>)predicates);

    public static BlockPredicate AllOf(BlockPredicate a, BlockPredicate b) => AllOf(new[] { a, b });

    public static BlockPredicate AnyOf(IReadOnlyList<BlockPredicate> predicates) => new AnyOfPredicate(predicates);

    public static BlockPredicate AnyOf(params BlockPredicate[] predicates)
        => AnyOf((IReadOnlyList<BlockPredicate>)predicates);

    public static BlockPredicate AnyOf(BlockPredicate a, BlockPredicate b) => AnyOf(new[] { a, b });

    public static BlockPredicate MatchesBlocks(Vec3i offset, IReadOnlyList<RegBlock> blocks)
        => new MatchingBlocksPredicate(offset, DirectBlocks(blocks));

    public static BlockPredicate MatchesBlocks(IReadOnlyList<RegBlock> blocks) => MatchesBlocks(Vec3i.Zero, blocks);

    public static BlockPredicate MatchesBlocks(Vec3i offset, params RegBlock[] blocks)
        => MatchesBlocks(offset, (IReadOnlyList<RegBlock>)blocks);

    public static BlockPredicate MatchesBlocks(params RegBlock[] blocks) => MatchesBlocks(Vec3i.Zero, blocks);

    public static BlockPredicate MatchesTag(Vec3i offset, TagKey<RegBlock> tag)
        => new MatchingBlockTagPredicate(offset, tag);

    public static BlockPredicate MatchesTag(TagKey<RegBlock> tag) => MatchesTag(Vec3i.Zero, tag);

    public static BlockPredicate MatchesFluids(Vec3i offset, IReadOnlyList<Fluid> fluids)
        => new MatchingFluidsPredicate(offset, DirectFluids(fluids));

    public static BlockPredicate MatchesFluids(Vec3i offset, params Fluid[] fluids)
        => MatchesFluids(offset, (IReadOnlyList<Fluid>)fluids);

    public static BlockPredicate MatchesFluids(params Fluid[] fluids) => MatchesFluids(Vec3i.Zero, fluids);

    public static BlockPredicate MatchesBiomes(HolderSet<Biome> biomes) => new MatchingBiomesPredicate(biomes);

    public static BlockPredicate Not(BlockPredicate predicate) => new NotPredicate(predicate);

    public static BlockPredicate Replaceable(Vec3i offset) => new ReplaceablePredicate(offset);

    public static BlockPredicate Replaceable() => Replaceable(Vec3i.Zero);

    public static BlockPredicate WouldSurvive(BlockState state, Vec3i offset) => new WouldSurvivePredicate(offset, state);

    public static BlockPredicate HasSturdyFace(Vec3i offset, Direction direction)
        => new HasSturdyFacePredicate(offset, direction);

    public static BlockPredicate HasSturdyFace(Direction direction) => HasSturdyFace(Vec3i.Zero, direction);

    public static BlockPredicate Solid(Vec3i offset) => new SolidPredicate(offset);

    public static BlockPredicate Solid() => Solid(Vec3i.Zero);

    public static BlockPredicate NoFluid() => NoFluid(Vec3i.Zero);

    public static BlockPredicate NoFluid(Vec3i offset) => MatchesFluids(offset, MatchingFluidsPredicate.EmptyFluid);

    public static BlockPredicate InsideWorld(Vec3i offset) => new InsideWorldBoundsPredicate(offset);

    public static BlockPredicate AlwaysTrue() => TrueBlockPredicate.Instance;

    public static BlockPredicate Unobstructed(Vec3i offset) => new UnobstructedPredicate(offset);

    public static BlockPredicate Unobstructed() => Unobstructed(Vec3i.Zero);

    //DirectBlocks wrap a block list as a direct set; unregistered blocks degrade to direct holders
    private static DirectHolderSet<RegBlock> DirectBlocks(IReadOnlyList<RegBlock> blocks)
        => new(blocks.Select(BuiltInRegistries.BLOCK.WrapAsHolder).ToList());

    //DirectFluids wrap a fluid list as a direct set; unregistered fluids degrade to direct holders
    private static DirectHolderSet<Fluid> DirectFluids(IReadOnlyList<Fluid> fluids)
        => new(fluids.Select(BuiltInRegistries.FLUID.WrapAsHolder).ToList());
}

//BlockPredicateCodec look up BLOCK_PREDICATE_TYPE by the type field then delegate decoding to that type
//Maps to vanilla BuiltInRegistries.BLOCK_PREDICATE_TYPE.byNameCodec().dispatch(...)
internal sealed class BlockPredicateCodec : ScalarCodec<BlockPredicate>
{
    public static readonly BlockPredicateCodec Instance = new();

    public override DataResult<BlockPredicate> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePredicate(ops, map));

    private static DataResult<BlockPredicate> DecodePredicate<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<BlockPredicate>.Error(() => "block predicate is missing the type field");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<BlockPredicate>.Error(() => "block predicate type must be a string");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<BlockPredicate>.Error(() => $"invalid predicate type: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.BLOCK_PREDICATE_TYPE.GetValue(typeId.Value) is not BlockPredicateType type)
            return DataResult<BlockPredicate>.Error(() => $"unknown predicate type: {typeId}");
        return type.Decode(ops, input);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, BlockPredicate value)
    {
        var builder = ops.MapBuilder();
        builder.Add("type", ops.CreateString(value.Type.Id.ToString()));
        value.Type.EncodeFields(ops, value, builder);
        return builder.Build(ops.Empty());
    }
}
