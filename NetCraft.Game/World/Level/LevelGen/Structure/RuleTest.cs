using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RuleTest 规则输入判定 对应原版 RuleTest
//一条处理器规则先用它判输入方块 再判世界位置 位置判定另见 PosRuleTest
public abstract class RuleTest
{
    //Codec 多态入口 按 predicate_type 派发到 RULE_TEST 注册表里的具体类型
    public static readonly Codec<RuleTest> Codec = new RuleTestDispatchCodec();

    //TestAgainstWorldState 拿世界里的方块状态做判定 对应原版 testAgainstWorldState
    //没有世界视图时判定不成立 原版这条路径必定有 level
    public virtual bool TestAgainstWorldState(WorldGenRegion? level, BlockPos pos, RandomSource random)
        => level is not null && Test(level.GetBlockState(pos.X, pos.Y, pos.Z), random);

    //Test 判定输入方块 对应原版 test
    public abstract bool Test(BlockState state, RandomSource random);

    //Type 所属类型单例 序列化时取它的注册名
    public abstract RuleTestType Type { get; }
}

//RuleTestType 规则测试类型 对应原版 RuleTestType
//持注册名与元素 codec 注册进 RULE_TEST 供 predicate_type 派发
public abstract class RuleTestType : NetCraft.Registry.RuleTestType<object>
{
    public Identifier Id { get; }

    protected RuleTestType(Identifier id) => Id = id;

    //DecodeTest 从 map 解出一条规则测试
    public abstract DataResult<RuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input);

    public override string ToString() => $"RuleTestType[{Id}]";
}

//RuleTestType<T> 强类型规则测试类型
public sealed class RuleTestType<T> : RuleTestType where T : RuleTest
{
    private readonly MapCodec<T> _codec;

    public RuleTestType(Identifier id, MapCodec<T> codec) : base(id) => _codec = codec;

    public override DataResult<RuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(v => (RuleTest)v);
}

//RuleTestTypes 规则测试类型登记 对应原版 RuleTestType 的静态字段
public static class RuleTestTypes
{
    public static readonly RuleTestType<AlwaysTrueTest> AlwaysTrue =
        Register("always_true", AlwaysTrueTest.MapCodec);

    public static readonly RuleTestType<BlockMatchTest> BlockMatch =
        Register("block_match", BlockMatchTest.MapCodec);

    public static readonly RuleTestType<BlockStateMatchTest> BlockStateMatch =
        Register("blockstate_match", BlockStateMatchTest.MapCodec);

    public static readonly RuleTestType<TagMatchTest> TagMatch =
        Register("tag_match", TagMatchTest.MapCodec);

    public static readonly RuleTestType<RandomBlockMatchTest> RandomBlockMatch =
        Register("random_block_match", RandomBlockMatchTest.MapCodec);

    public static readonly RuleTestType<RandomBlockStateMatchTest> RandomBlockStateMatch =
        Register("random_blockstate_match", RandomBlockStateMatchTest.MapCodec);

    //Register 登记进 RULE_TEST 并返回类型实例
    private static RuleTestType<T> Register<T>(string path, MapCodec<T> codec) where T : RuleTest
    {
        var type = new RuleTestType<T>(Identifier.WithDefaultNamespace(path), codec);
        Registry<NetCraft.Registry.RuleTestType<object>>.Register(BuiltInRegistries.RULE_TEST, path, type);
        return type;
    }
}

//AlwaysTrueTest 恒真测试 对应原版 AlwaysTrueTest
public sealed class AlwaysTrueTest : RuleTest
{
    public static readonly AlwaysTrueTest Instance = new();

    public static readonly MapCodec<AlwaysTrueTest> MapCodec = new StructureUnitMapCodec<AlwaysTrueTest>(() => Instance);

    private AlwaysTrueTest() { }

    public override bool TestAgainstWorldState(WorldGenRegion? level, BlockPos pos, RandomSource random) => true;

    public override bool Test(BlockState state, RandomSource random) => true;

    public override RuleTestType Type => RuleTestTypes.AlwaysTrue;
}

//BlockMatchTest 方块匹配 对应原版 BlockMatchTest 只比方块不比状态
public sealed class BlockMatchTest : RuleTest
{
    public static readonly MapCodec<BlockMatchTest> MapCodec =
        new StructureSingleFieldMapCodec<BlockMatchTest, RegBlock>(
            StructureBlockCodec.Instance.FieldOf("block"), block => new BlockMatchTest(block), t => t.Block);

    public RegBlock Block { get; }

    public BlockMatchTest(RegBlock block) => Block = block;

    public override bool Test(BlockState state, RandomSource random) => state.Owner == Block;

    public override RuleTestType Type => RuleTestTypes.BlockMatch;
}

//BlockStateMatchTest 方块状态匹配 对应原版 BlockStateMatchTest 状态必须完全相同
public sealed class BlockStateMatchTest : RuleTest
{
    public static readonly MapCodec<BlockStateMatchTest> MapCodec =
        new StructureSingleFieldMapCodec<BlockStateMatchTest, BlockState>(
            BlockStateCodec.Instance.FieldOf("block_state"), state => new BlockStateMatchTest(state),
            t => t.BlockState);

    public BlockState BlockState { get; }

    public BlockStateMatchTest(BlockState blockState) => BlockState = blockState;

    public override bool Test(BlockState state, RandomSource random) => state == BlockState;

    public override RuleTestType Type => RuleTestTypes.BlockStateMatch;
}

//TagMatchTest 方块标签匹配 对应原版 TagMatchTest
public sealed class TagMatchTest : RuleTest
{
    public static readonly MapCodec<TagMatchTest> MapCodec =
        new StructureSingleFieldMapCodec<TagMatchTest, TagKey<RegBlock>>(
            StructureBlockTagCodec.Instance.FieldOf("tag"), tag => new TagMatchTest(tag), t => t.Tag);

    public TagKey<RegBlock> Tag { get; }

    public TagMatchTest(TagKey<RegBlock> tag) => Tag = tag;

    public override bool Test(BlockState state, RandomSource random) => ProcessorBlockHelper.InTag(state, Tag);

    public override RuleTestType Type => RuleTestTypes.TagMatch;
}

//RandomBlockMatchTest 带概率的方块匹配 对应原版 RandomBlockMatchTest
public sealed class RandomBlockMatchTest : RuleTest
{
    public static readonly MapCodec<RandomBlockMatchTest> MapCodec =
        RecordCodecBuilder.Of2<RandomBlockMatchTest, RegBlock, float>(
            StructureBlockCodec.Instance.FieldOf("block").ForGetter<RandomBlockMatchTest, RegBlock>(t => t.Block),
            Codecs.Float.FieldOf("probability").ForGetter<RandomBlockMatchTest, float>(t => t.Probability),
            (block, probability) => new RandomBlockMatchTest(block, probability));

    public RegBlock Block { get; }

    public float Probability { get; }

    public RandomBlockMatchTest(RegBlock block, float probability)
    {
        Block = block;
        Probability = probability;
    }

    public override bool Test(BlockState state, RandomSource random)
        => state.Owner == Block && random.NextFloat() < Probability;

    public override RuleTestType Type => RuleTestTypes.RandomBlockMatch;
}

//RandomBlockStateMatchTest 带概率的状态匹配 对应原版 RandomBlockStateMatchTest
public sealed class RandomBlockStateMatchTest : RuleTest
{
    public static readonly MapCodec<RandomBlockStateMatchTest> MapCodec =
        RecordCodecBuilder.Of2<RandomBlockStateMatchTest, BlockState, float>(
            BlockStateCodec.Instance.FieldOf("block_state")
                .ForGetter<RandomBlockStateMatchTest, BlockState>(t => t.BlockState),
            Codecs.Float.FieldOf("probability").ForGetter<RandomBlockStateMatchTest, float>(t => t.Probability),
            (state, probability) => new RandomBlockStateMatchTest(state, probability));

    public BlockState BlockState { get; }

    public float Probability { get; }

    public RandomBlockStateMatchTest(BlockState blockState, float probability)
    {
        BlockState = blockState;
        Probability = probability;
    }

    public override bool Test(BlockState state, RandomSource random)
        => state == BlockState && random.NextFloat() < Probability;

    public override RuleTestType Type => RuleTestTypes.RandomBlockStateMatch;
}

//RuleTestDispatchCodec 规则测试多态 codec 对应原版 RuleTest.CODEC 的 dispatch
internal sealed class RuleTestDispatchCodec : ScalarCodec<RuleTest>
{
    public override DataResult<RuleTest> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeTest(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RuleTest value)
        => DataResult<U>.Error(() => "规则测试编码暂未实现");

    //DecodeTest 读 predicate_type 查表再交给该类型的 codec
    internal static DataResult<RuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("predicate_type");
        if (!typeTag.IsPresent) return DataResult<RuleTest>.Error(() => "规则测试缺少 predicate_type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<RuleTest>.Error(() => "predicate_type 必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<RuleTest>.Error(() => $"非法的规则测试类型: {text.GetOrThrow()}");
        if (!BuiltInRegistries.RULE_TEST.ContainsKey(id.Value))
            return DataResult<RuleTest>.Error(() => $"未注册的规则测试类型: {id}");
        var type = BuiltInRegistries.RULE_TEST.GetValue(id.Value) as RuleTestType;
        return type is null
            ? DataResult<RuleTest>.Error(() => $"规则测试类型 {id} 无法解析")
            : type.DecodeTest(ops, input);
    }
}
