using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Registry;

//All stub game types, to be migrated to their projects incrementally as subsystems become ready

public interface Action { }
public interface Activity { }
public interface Advancement { }
public interface BannerPattern { }
//BiomeSource biome source interface, maps to vanilla net.minecraft.world.level.biome.BiomeSource
//Provides the ability to query a biome by coordinates; at the stub stage it only defines method signatures
//Concrete implementations FixedBiomeSource/MultiNoiseBiomeSource await Game layer integration
public interface BiomeSource
{
    //GetBiome placeholder signature to query a biome by world coordinates, awaiting concrete logic
    Biome GetBiome(int x, int y, int z);

    //PossibleBiomes all biomes this source can produce
    //Decoration uses it to filter out biomes found in the 3x3 range that do not belong to this source
    IReadOnlyList<Biome> PossibleBiomes { get; }
}
public interface BlockEntityType<T1> { }
//BlockPredicateType block predicate type marker interface, maps to vanilla BlockPredicateType<P>
//Vanilla is generic over the predicate type; de-genericized here, with the real implementation provided by the Game layer
public interface BlockPredicateType { }
public interface BlockStateProviderType<T1> { }
public interface CatSoundVariant { }
public interface CatVariant { }
public interface ChickenSoundVariant { }
public interface ChickenVariant { }
//ConfiguredFeature configured feature marker interface, maps to vanilla ConfiguredFeature<FC,F>
//Vanilla takes two type parameters; de-genericized here, with the real implementation provided by the Game layer and the registry and biome sides depending only on this marker
public interface ConfiguredFeature { }
//ConfiguredWorldCarver configured carver marker interface, maps to vanilla ConfiguredWorldCarver
//The real implementation is provided by the Game layer; the registry and biome generation settings sides depend only on this marker
public interface ConfiguredWorldCarver { }
public interface ConsumeEffectType<T1> { }
public interface Consumer<T1> { }
public interface CowSoundVariant { }
public interface CowVariant { }
public interface CreativeModeTab { }
public interface CriterionTrigger<T1> { }
public interface DataComponentPredicateType<T1> { }
public interface DebugSubscription<T1> { }
public interface DecoratedPotPattern { }
public interface Dialog { }
public interface DialogBody { }
public interface DimensionType { }
public interface Enchantment { }
public interface EnchantmentEntityEffect { }
public interface EnchantmentLocationBasedEffect { }
public interface EnchantmentProvider { }
public interface EnchantmentValueEffect { }
//ILevelReader the minimal level reading capability the predicate layer needs, corresponding to the set of ServerLevel methods used by predicates
//ServerLevel lives in the Storage layer and is out of reach for the Registry layer, so this capability set is defined in Registry and implemented by ServerLevel
public interface ILevelReader
{
    //Dimension dimension registry name
    Identifier Dimension { get; }

    //IsLoaded whether the chunk at this position is already in memory
    bool IsLoaded(BlockPos pos);

    //GetBlockState read the block state; returns null when the chunk is not loaded
    BlockState? GetBlockState(BlockPos pos);

    //GetBiome read the biome at this position; returns null when the chunk is not in memory
    Holder<Biome>? GetBiome(BlockPos pos);

    //GetMaxLocalRawBrightness the maximum local brightness at this position
    int GetMaxLocalRawBrightness(BlockPos pos);

    //CanSeeSky whether this position can see the sky directly
    bool CanSeeSky(BlockPos pos);
}
//EntitySubPredicate entity sub-predicate, maps to vanilla net.minecraft.advancements.predicates.entity.EntitySubPredicate
//The vanilla signature takes ServerLevel, which lives upstream and cannot be referenced from Registry, so ILevelReader abstracts the needed capability
public interface EntitySubPredicate
{
    //Matches determines whether the entity satisfies this predicate; level is the initiator's level and position is the initiator's position
    bool Matches(Entity entity, ILevelReader? level, Vec3? position);
}
//Feature feature marker interface, maps to vanilla Feature<FC>
//Vanilla is generic over the config type; de-genericized here, with subclasses using is-pattern matching to cast the config in Place
public interface Feature { }
public interface FeatureSizeType<T1> { }
public interface FlatLevelGeneratorPreset { }
public interface FloatProvider { }
public interface FoliagePlacerType<T1> { }
public interface FrogVariant { }
public interface GameEvent { }
public interface GameTestHelper { }
public interface GameTestInstance { }
public interface HeightProviderType<T1> { }
public interface IncomingRpcMethod<T1, T2> { }
public interface InputControl { }
public interface Instrument { }
public interface IntProvider { }
public interface JukeboxSong { }
public interface Level { }
public interface LevelBasedValue { }
public interface LevelStem { }
public interface LootItemCondition { }
public interface LootItemFunction { }
public interface LootPoolEntryContainer { }
public interface LootTable { }
public interface MapDecorationType { }
public interface MemoryModuleType<T1> { }
public interface MobEffect { }
public interface NbtProvider { }
public interface NumberFormatType<T1> { }
public interface NumberProvider { }
public interface OutgoingRpcMethod<T1, T2> { }
public interface PaintingVariant { }
public interface ParticleType<T1> { }
public interface PigSoundVariant { }
public interface PigVariant { }
public interface PlacedFeature { }
//PlacementModifierType placement modifier type marker interface, maps to vanilla PlacementModifierType<P>
//Vanilla is generic over the modifier type; de-genericized here, and the registry holds an "identifier + element codec" pair against it
public interface PlacementModifierType { }
//PoiType point of interest type interface, maps to vanilla net.minecraft.world.entity.ai.village.poi.PoiType
//The stub is upgraded to an interface holding an Identifier so SimplePoiManager can index it
public interface PoiType { Identifier Id { get; } }
public interface PoolAliasBinding { }
public interface PositionSourceType<T1> { }
public interface PosRuleTestType<T1> { }
public interface Potion { }
public interface Recipe<T1> { }
public interface RecipeBookCategory { }
public interface RecipeDisplayType<T1> { }
public interface RecipeSerializer<T1> { }
public interface RecipeType<T1> { }
public interface RootPlacerType<T1> { }
public interface RuleBlockEntityModifierType<T1> { }
public interface RuleTestType<T1> { }
public interface ScoreboardNameProvider { }
public interface SensorType<T1> { }
public interface SlotDisplayType<T1> { }
public interface SlotSource { }
public interface SpawnCondition { }
public interface StatType<T1> { }
public interface Structure { }
public interface StructurePieceType { }
public interface StructurePlacementType<T1> { }
public interface StructurePoolElementType<T1> { }
public interface StructureProcessor { }
public interface StructureProcessorList { }
public interface StructureSet { }
public interface StructureTemplatePool { }
public interface StructureType<T1> { }
public interface SulfurCubeArchetype { }
public interface TestEnvironmentDefinition<T1> { }
public interface TicketType { }
public interface TradeSet { }
public interface TreeDecoratorType<T1> { }
public interface TrialSpawnerConfig { }
public interface TrimMaterial { }
public interface TrimPattern { }
public interface TrunkPlacerType<T1> { }
public interface VillagerProfession { }
public interface VillagerTrade { }
public interface VillagerType { }
public interface WolfSoundVariant { }
public interface WolfVariant { }
//WorldCarver carver marker interface, maps to vanilla WorldCarver
//The real implementation is provided by the Game layer, and the carver registry holds elements against it
public interface WorldCarver { }
public interface WorldPreset { }
public interface ZombieNautilusVariant { }
