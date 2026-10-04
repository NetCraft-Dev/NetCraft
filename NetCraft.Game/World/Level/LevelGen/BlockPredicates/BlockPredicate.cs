using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//BlockPredicate 方块谓词对应原版 net.minecraft.world.level.levelgen.blockpredicates.BlockPredicate
//判定某个位置是否满足条件 放置修饰器与特征靠它做位置过滤
public abstract class BlockPredicate
{
    //Codec 多态编解码入口 先读 type 字段再分发到具体类型
    public static readonly Codec<BlockPredicate> Codec = BlockPredicateCodec.Instance;

    //AirTag 空气方块标签 对应原版 BlockTags.AIR
    public static readonly TagKey<RegBlock> AirTag =
        TagKey<RegBlock>.Create(Registries.BLOCK, Identifier.WithDefaultNamespace("air"));

    //OnlyInAir 只在空气里对应原版 ONLY_IN_AIR_PREDICATE
    public static readonly BlockPredicate OnlyInAir = MatchesTag(AirTag);

    //OnlyInAirOrWater 只在空气或水里对应原版 ONLY_IN_AIR_OR_WATER_PREDICATE
    public static readonly BlockPredicate OnlyInAirOrWater = AnyOf(OnlyInAir, MatchesBlocks(Blocks.WATER));

    //Type 所属类型单例 编码与注册表解析靠它拿 id
    public abstract BlockPredicateType Type { get; }

    //Test 判定该位置是否满足条件对应原版 test(WorldGenLevel, BlockPos)
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

    //DirectBlocks 把方块列表包成直接集合 未注册方块退化成直接持有者
    private static DirectHolderSet<RegBlock> DirectBlocks(IReadOnlyList<RegBlock> blocks)
        => new(blocks.Select(BuiltInRegistries.BLOCK.WrapAsHolder).ToList());

    //DirectFluids 把流体列表包成直接集合 未注册流体退化成直接持有者
    private static DirectHolderSet<Fluid> DirectFluids(IReadOnlyList<Fluid> fluids)
        => new(fluids.Select(BuiltInRegistries.FLUID.WrapAsHolder).ToList());
}

//BlockPredicateCodec 按 type 字段查 BLOCK_PREDICATE_TYPE 再委派给该类型解码
//对应原版 BuiltInRegistries.BLOCK_PREDICATE_TYPE.byNameCodec().dispatch(...)
internal sealed class BlockPredicateCodec : ScalarCodec<BlockPredicate>
{
    public static readonly BlockPredicateCodec Instance = new();

    public override DataResult<BlockPredicate> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePredicate(ops, map));

    private static DataResult<BlockPredicate> DecodePredicate<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("type");
        if (!typeTag.IsPresent) return DataResult<BlockPredicate>.Error(() => "方块谓词缺 type 字段");
        var typeText = ops.GetStringValue(typeTag.Get());
        if (!typeText.Result().IsPresent)
            return DataResult<BlockPredicate>.Error(() => "方块谓词的 type 必须是字符串");
        var typeId = Identifier.TryParse(typeText.GetOrThrow());
        if (typeId is null)
            return DataResult<BlockPredicate>.Error(() => $"非法的谓词类型: {typeText.GetOrThrow()}");
        if (BuiltInRegistries.BLOCK_PREDICATE_TYPE.GetValue(typeId.Value) is not BlockPredicateType type)
            return DataResult<BlockPredicate>.Error(() => $"未知的谓词类型: {typeId}");
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
