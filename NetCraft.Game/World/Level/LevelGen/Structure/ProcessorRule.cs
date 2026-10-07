using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//ProcessorRule one rule of a processor, maps to vanilla ProcessorRule
//Input test + location test + output block state + block entity modifier; all four passing means a full replacement
public sealed class ProcessorRule
{
    //DefaultBlockEntityModifier default modifier, maps to vanilla DEFAULT_BLOCK_ENTITY_MODIFIER
    public static readonly RuleBlockEntityModifier DefaultBlockEntityModifier = PassthroughModifier.Instance;

    //Codec rule codec; field names and order match vanilla one by one
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

    //Test hits only when the three checks pass in order, maps to vanilla test
    public bool Test(WorldGenRegion? level, BlockState inputState, BlockPos inTemplatePos, BlockPos worldPos,
        BlockPos reference, RandomSource random)
        => InputPredicate.Test(inputState, random)
            && LocationPredicate.TestAgainstWorldState(level, worldPos, random)
            && PosPredicate.Test(inTemplatePos, worldPos, reference, random);

    //GetOutputTag produces block entity data through the modifier, maps to vanilla getOutputTag
    public CompoundTag? GetOutputTag(RandomSource random, CompoundTag? existingTag)
        => BlockEntityModifier.Apply(random, existingTag);
}
