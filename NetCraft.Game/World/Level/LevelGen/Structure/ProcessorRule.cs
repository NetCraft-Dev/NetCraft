using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//ProcessorRule 处理器的一条规则 对应原版 ProcessorRule
//输入测试 + 位置测试 + 输出方块状态 + 方块实体修改器 四项判定通过即整条替换
public sealed class ProcessorRule
{
    //DefaultBlockEntityModifier 缺省修改器 对应原版 DEFAULT_BLOCK_ENTITY_MODIFIER
    public static readonly RuleBlockEntityModifier DefaultBlockEntityModifier = PassthroughModifier.Instance;

    //Codec 规则编解码 字段名与顺序逐条对齐原版
    public static readonly Codec<ProcessorRule> Codec =
        RecordCodecBuilder.Of5<ProcessorRule, RuleTest, RuleTest, PosRuleTest, BlockState, RuleBlockEntityModifier>(
            RuleTest.Codec.FieldOf("input_predicate").ForGetter<ProcessorRule, RuleTest>(r => r.InputPredicate),
            RuleTest.Codec.FieldOf("location_predicate").ForGetter<ProcessorRule, RuleTest>(r => r.LocationPredicate),
            PosRuleTest.Codec.OptionalFieldOf("position_predicate", PosAlwaysTrueTest.Instance)
                .ForGetter<ProcessorRule, PosRuleTest>(r => r.PosPredicate),
            BlockStateCodec.Instance.FieldOf("output_state")
                .ForGetter<ProcessorRule, BlockState>(r => r.OutputState),
            RuleBlockEntityModifier.Codec.OptionalFieldOf("block_entity_modifier", DefaultBlockEntityModifier)
                .ForGetter<ProcessorRule, RuleBlockEntityModifier>(r => r.BlockEntityModifier),
            (input, location, position, output, modifier) =>
                new ProcessorRule(input, location, position, output, modifier));

    public RuleTest InputPredicate { get; }
    public RuleTest LocationPredicate { get; }
    public PosRuleTest PosPredicate { get; }
    public BlockState OutputState { get; }
    public RuleBlockEntityModifier BlockEntityModifier { get; }

    public ProcessorRule(RuleTest inputPredicate, RuleTest locationPredicate, BlockState outputState)
        : this(inputPredicate, locationPredicate, PosAlwaysTrueTest.Instance, outputState)
    {
    }

    public ProcessorRule(RuleTest inputPredicate, RuleTest locationPredicate, PosRuleTest posPredicate,
        BlockState outputState)
        : this(inputPredicate, locationPredicate, posPredicate, outputState, DefaultBlockEntityModifier)
    {
    }

    public ProcessorRule(RuleTest inputPredicate, RuleTest locationPredicate, PosRuleTest posPredicate,
        BlockState outputState, RuleBlockEntityModifier blockEntityModifier)
    {
        InputPredicate = inputPredicate;
        LocationPredicate = locationPredicate;
        PosPredicate = posPredicate;
        OutputState = outputState;
        BlockEntityModifier = blockEntityModifier;
    }

    //Test 三项判定依次通过才命中 对应原版 test
    public bool Test(WorldGenRegion? level, BlockState inputState, BlockPos inTemplatePos, BlockPos worldPos,
        BlockPos reference, RandomSource random)
        => InputPredicate.Test(inputState, random)
            && LocationPredicate.TestAgainstWorldState(level, worldPos, random)
            && PosPredicate.Test(inTemplatePos, worldPos, reference, random);

    //GetOutputTag 按修改器产出方块实体数据 对应原版 getOutputTag
    public CompoundTag? GetOutputTag(RandomSource random, CompoundTag? existingTag)
        => BlockEntityModifier.Apply(random, existingTag);
}
