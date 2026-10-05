using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Registry;

//所有 stub 游戏类型，待具体子系统就绪后逐步迁移到对应项目

public interface Action { }
public interface Activity { }
public interface Advancement { }
public interface BannerPattern { }
//BiomeSource 生物群系源接口对应原版 net.minecraft.world.level.biome.BiomeSource
//提供按坐标查询生物群系的能力stub 阶段仅定义方法签名
//具体实现 FixedBiomeSource/MultiNoiseBiomeSource 待 Game 层接入
public interface BiomeSource
{
    //GetBiome 按世界坐标查询生物群系占位签名待子接入具体逻辑
    Biome GetBiome(int x, int y, int z);

    //PossibleBiomes 本源可能产出的全部群系
    //装饰期要用它过滤掉 3x3 范围内捡到的、不属于本源的群系
    IReadOnlyList<Biome> PossibleBiomes { get; }
}
public interface BlockEntityType<T1> { }
//BlockPredicateType 方块谓词类型标记接口对应原版 BlockPredicateType<P>
//原版按谓词类型泛型 这里非泛型化 真实实现由 Game 层提供
public interface BlockPredicateType { }
public interface BlockStateProviderType<T1> { }
public interface CatSoundVariant { }
public interface CatVariant { }
public interface ChickenSoundVariant { }
public interface ChickenVariant { }
//ConfiguredFeature 配置化特征标记接口对应原版 ConfiguredFeature<FC,F>
//原版带两个泛型参数 这里非泛型化 真实实现由 Game 层提供 注册表与群系侧只依赖这个标记
public interface ConfiguredFeature { }
//ConfiguredWorldCarver 配置化雕刻器标记接口对应原版 ConfiguredWorldCarver
//真实实现由 Game 层提供 注册表与群系生成设置侧只依赖这个标记
public interface ConfiguredWorldCarver { }
public interface ConsumeEffectType<T1> { }
public interface Consumer<T1> { }
public interface CowSoundVariant { }
public interface CowVariant { }
public interface CreativeModeTab { }
public interface CriterionTrigger<T1> { }
public interface DamageType { }
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
//EntitySubPredicate 实体子谓词对应原版 net.minecraft.advancements.predicates.entity.EntitySubPredicate
//原版签名带 ServerLevel 该层在上游 Registry 不能反向引用 故去掉 level 只留实体与参照位置
public interface EntitySubPredicate
{
    //Matches 判定实体是否满足本谓词 position 为发起方位置 距离类谓词据此比对
    bool Matches(Entity entity, Vec3? position);
}
//Feature 特征标记接口对应原版 Feature<FC>
//原版按配置类型泛型 这里非泛型化 子类在 Place 里 is 模式匹配转配置
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
public interface Permission { }
public interface PermissionCheck { }
public interface PigSoundVariant { }
public interface PigVariant { }
public interface PlacedFeature { }
//PlacementModifierType 放置修饰器类型标记接口对应原版 PlacementModifierType<P>
//原版按修饰器类型泛型 这里非泛型化 注册表按它持有「标识 + 元素 codec」对
public interface PlacementModifierType { }
//PoiType 兴趣点类型接口对应原版 net.minecraft.world.entity.ai.village.poi.PoiType
//stub 升级为持 Identifier 的接口供 SimplePoiManager 索引
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
//WorldCarver 雕刻器标记接口对应原版 WorldCarver
//真实实现由 Game 层提供 雕刻器注册表按它持有元素
public interface WorldCarver { }
public interface WorldPreset { }
public interface ZombieNautilusVariant { }
