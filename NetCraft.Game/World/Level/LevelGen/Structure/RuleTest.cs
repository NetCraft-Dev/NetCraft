using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//RuleTest rule input test, maps to vanilla RuleTest
//A processor rule uses it first for the input block, then checks the world position; see PosRuleTest for position tests
public abstract class RuleTest
{
    //Codec polymorphic entry, dispatches by predicate_type to a concrete type in the RULE_TEST registry
    public static readonly Codec<RuleTest> Codec = new RuleTestDispatchCodec();

    //TestAgainstWorldState tests against the block state in the world, maps to vanilla testAgainstWorldState
    //The test fails without a world view; vanilla always has a level on this path
    public virtual bool TestAgainstWorldState(WorldGenRegion? level, BlockPos pos, RandomSource random)
        => level is not null && Test(level.GetBlockState(pos.X, pos.Y, pos.Z), random);

    //Test evaluates the input block, maps to vanilla test
    public abstract bool Test(BlockState state, RandomSource random);

    //Type the owning type singleton; serialization uses its registry name
    public abstract RuleTestType Type { get; }
}

//RuleTestType rule test type, maps to vanilla RuleTestType
//Holds the registry name and element codec, registered into RULE_TEST for predicate_type dispatch
public abstract class RuleTestType : NetCraft.Registry.RuleTestType<object>
{
    public Identifier Id { get; }

    protected RuleTestType(Identifier id) => Id = id;

    //DecodeTest decodes a rule test from a map
    public abstract DataResult<RuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input);

    public override string ToString() => $"RuleTestType[{Id}]";
}

//RuleTestType<T> strongly typed rule test type
public sealed class RuleTestType<T> : RuleTestType where T : RuleTest
{
    private readonly MapCodec<T> _codec;

    public RuleTestType(Identifier id, MapCodec<T> codec) : base(id) => _codec = codec;

    public override DataResult<RuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
        => _codec.Decode(ops, input).Map(v => (RuleTest)v);
}

//RuleTestTypes rule test type registration, maps to the static fields of vanilla RuleTestType
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

    //Register registers into RULE_TEST and returns the type instance
    private static RuleTestType<T> Register<T>(string path, MapCodec<T> codec) where T : RuleTest
    {
        var type = new RuleTestType<T>(Identifier.WithDefaultNamespace(path), codec);
        Registry<NetCraft.Registry.RuleTestType<object>>.Register(BuiltInRegistries.RULE_TEST, path, type);
        return type;
    }
}

//AlwaysTrueTest always-true test, maps to vanilla AlwaysTrueTest
public sealed class AlwaysTrueTest : RuleTest
{
    public static readonly AlwaysTrueTest Instance = new();

    public static readonly MapCodec<AlwaysTrueTest> MapCodec = new StructureUnitMapCodec<AlwaysTrueTest>(() => Instance);

    private AlwaysTrueTest() { }

    public override bool TestAgainstWorldState(WorldGenRegion? level, BlockPos pos, RandomSource random) => true;

    public override bool Test(BlockState state, RandomSource random) => true;

    public override RuleTestType Type => RuleTestTypes.AlwaysTrue;
}

//BlockMatchTest block match, maps to vanilla BlockMatchTest; compares only the block, not the state
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

//BlockStateMatchTest block state match, maps to vanilla BlockStateMatchTest; states must be exactly equal
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

//TagMatchTest block tag match, maps to vanilla TagMatchTest
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

//RandomBlockMatchTest probabilistic block match, maps to vanilla RandomBlockMatchTest
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

//RandomBlockStateMatchTest probabilistic state match, maps to vanilla RandomBlockStateMatchTest
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

//RuleTestDispatchCodec rule test polymorphic codec, maps to the dispatch of vanilla RuleTest.CODEC
internal sealed class RuleTestDispatchCodec : ScalarCodec<RuleTest>
{
    public override DataResult<RuleTest> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeTest(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, RuleTest value)
        => DataResult<U>.Error(() => "rule test encoding not implemented yet");

    //DecodeTest reads predicate_type, looks it up, then hands off to that type's codec
    internal static DataResult<RuleTest> DecodeTest<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var typeTag = input.Get("predicate_type");
        if (!typeTag.IsPresent) return DataResult<RuleTest>.Error(() => "rule test is missing predicate_type");
        var text = ops.GetStringValue(typeTag.Get());
        if (!text.Result().IsPresent) return DataResult<RuleTest>.Error(() => "predicate_type must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null) return DataResult<RuleTest>.Error(() => $"invalid rule test type: {text.GetOrThrow()}");
        if (!BuiltInRegistries.RULE_TEST.ContainsKey(id.Value))
            return DataResult<RuleTest>.Error(() => $"unregistered rule test type: {id}");
        var type = BuiltInRegistries.RULE_TEST.GetValue(id.Value) as RuleTestType;
        return type is null
            ? DataResult<RuleTest>.Error(() => $"rule test type {id} cannot be parsed")
            : type.DecodeTest(ops, input);
    }
}
