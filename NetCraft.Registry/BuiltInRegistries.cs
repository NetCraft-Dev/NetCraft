using System.Reflection;
using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Logging;
using NetCraft.Registry.Environment;

namespace NetCraft.Registry;

//All built-in registry instances, maps to vanilla net.minecraft.core.registries.BuiltInRegistries
//Stub type; bootstrap callbacks are not implemented for now and every registry is empty
public static class BuiltInRegistries
{
    //All registries are managed through a root WritableRegistry; simplified here without a container
    public static readonly DefaultedRegistry<GameEvent> GAME_EVENT = RegisterDefaulted<GameEvent>(Registries.GAME_EVENT, "step");
    public static readonly Registry<SoundEvent> SOUND_EVENT = RegisterSimple<SoundEvent>(Registries.SOUND_EVENT);
    public static readonly DefaultedRegistry<Fluid> FLUID = RegisterDefaulted<Fluid>(Registries.FLUID, "empty");
    public static readonly Registry<MobEffect> MOB_EFFECT = RegisterSimple<MobEffect>(Registries.MOB_EFFECT);
    public static readonly DefaultedRegistry<Block> BLOCK = RegisterDefaulted<Block>(Registries.BLOCK, "air");
    public static readonly DefaultedRegistry<Biome> BIOME = RegisterDefaulted<Biome>(Registries.BIOME, "plains");
    public static readonly Registry<DebugSubscription<object>> DEBUG_SUBSCRIPTION = RegisterSimple<DebugSubscription<object>>(Registries.DEBUG_SUBSCRIPTION);
    public static readonly DefaultedRegistry<EntityType<object>> ENTITY_TYPE = RegisterDefaulted<EntityType<object>>(Registries.ENTITY_TYPE, "pig");
    public static readonly DefaultedRegistry<Item> ITEM = RegisterDefaulted<Item>(Registries.ITEM, "air");
    public static readonly Registry<Potion> POTION = RegisterSimple<Potion>(Registries.POTION);
    //PARTICLE_TYPE has no default value; declared as a writable registry so built-in particle types can be registered in vanilla order at startup
    public static readonly MappedRegistry<ParticleType<object>> PARTICLE_TYPE = RegisterSimple<ParticleType<object>>(Registries.PARTICLE_TYPE);
    public static readonly Registry<BlockEntityType<object>> BLOCK_ENTITY_TYPE = RegisterSimple<BlockEntityType<object>>(Registries.BLOCK_ENTITY_TYPE);
    public static readonly Registry<object> CUSTOM_STAT = RegisterSimple<object>(Registries.CUSTOM_STAT);
    public static readonly DefaultedRegistry<ChunkStatus> CHUNK_STATUS = RegisterDefaulted<ChunkStatus>(Registries.CHUNK_STATUS, "empty");
    //NOISE noise parameter registry, used by Noises.Bootstrap to register 64 built-in NoiseParameters
    public static readonly Registry<NoiseParameters> NOISE = RegisterSimple<NoiseParameters>(Registries.NOISE);
    public static readonly Registry<RuleTestType<object>> RULE_TEST = RegisterSimple<RuleTestType<object>>(Registries.RULE_TEST);
    public static readonly Registry<RuleBlockEntityModifierType<object>> RULE_BLOCK_ENTITY_MODIFIER = RegisterSimple<RuleBlockEntityModifierType<object>>(Registries.RULE_BLOCK_ENTITY_MODIFIER);
    public static readonly Registry<PosRuleTestType<object>> POS_RULE_TEST = RegisterSimple<PosRuleTestType<object>>(Registries.POS_RULE_TEST);
    public static readonly Registry<object> MENU = RegisterSimple<object>(Registries.MENU);
    public static readonly Registry<object> CHAT_TYPE = RegisterSimple<object>(Registries.CHAT_TYPE);
    public static readonly Registry<RecipeType<object>> RECIPE_TYPE = RegisterSimple<RecipeType<object>>(Registries.RECIPE_TYPE);
    public static readonly Registry<RecipeSerializer<object>> RECIPE_SERIALIZER = RegisterSimple<RecipeSerializer<object>>(Registries.RECIPE_SERIALIZER);
    //The entity attribute registry's entry type lives under NetCraft.Registry.EntityAttribute
    public static readonly Registry<EntityAttribute.Attribute> ATTRIBUTE =
        RegisterSimple<EntityAttribute.Attribute>(Registries.ATTRIBUTE);
    public static readonly Registry<PositionSourceType<object>> POSITION_SOURCE_TYPE = RegisterSimple<PositionSourceType<object>>(Registries.POSITION_SOURCE_TYPE);
    public static readonly Registry<object> COMMAND_ARGUMENT_TYPE = RegisterSimple<object>(Registries.COMMAND_ARGUMENT_TYPE);
    public static readonly Registry<StatType<object>> STAT_TYPE = RegisterSimple<StatType<object>>(Registries.STAT_TYPE);
    public static readonly DefaultedRegistry<VillagerType> VILLAGER_TYPE = RegisterDefaulted<VillagerType>(Registries.VILLAGER_TYPE, "plains");
    public static readonly DefaultedRegistry<VillagerProfession> VILLAGER_PROFESSION = RegisterDefaulted<VillagerProfession>(Registries.VILLAGER_PROFESSION, "none");
    public static readonly Registry<PoiType> POINT_OF_INTEREST_TYPE = RegisterSimple<PoiType>(Registries.POINT_OF_INTEREST_TYPE);
    public static readonly DefaultedRegistry<MemoryModuleType<object>> MEMORY_MODULE_TYPE = RegisterDefaulted<MemoryModuleType<object>>(Registries.MEMORY_MODULE_TYPE, "dummy");
    public static readonly DefaultedRegistry<SensorType<object>> SENSOR_TYPE = RegisterDefaulted<SensorType<object>>(Registries.SENSOR_TYPE, "dummy");
    public static readonly Registry<Activity> ACTIVITY = RegisterSimple<Activity>(Registries.ACTIVITY);
    public static readonly Registry<MapCodec<LootPoolEntryContainer>> LOOT_POOL_ENTRY_TYPE = RegisterSimple<MapCodec<LootPoolEntryContainer>>(Registries.LOOT_POOL_ENTRY_TYPE);
    public static readonly Registry<MapCodec<LootItemFunction>> LOOT_FUNCTION_TYPE = RegisterSimple<MapCodec<LootItemFunction>>(Registries.LOOT_FUNCTION_TYPE);
    public static readonly Registry<MapCodec<LootItemCondition>> LOOT_CONDITION_TYPE = RegisterSimple<MapCodec<LootItemCondition>>(Registries.LOOT_CONDITION_TYPE);
    public static readonly Registry<MapCodec<NumberProvider>> LOOT_NUMBER_PROVIDER_TYPE = RegisterSimple<MapCodec<NumberProvider>>(Registries.LOOT_NUMBER_PROVIDER_TYPE);
    public static readonly Registry<MapCodec<NbtProvider>> LOOT_NBT_PROVIDER_TYPE = RegisterSimple<MapCodec<NbtProvider>>(Registries.LOOT_NBT_PROVIDER_TYPE);
    public static readonly Registry<MapCodec<ScoreboardNameProvider>> LOOT_SCORE_PROVIDER_TYPE = RegisterSimple<MapCodec<ScoreboardNameProvider>>(Registries.LOOT_SCORE_PROVIDER_TYPE);
    public static readonly Registry<MapCodec<FloatProvider>> FLOAT_PROVIDER_TYPE = RegisterSimple<MapCodec<FloatProvider>>(Registries.FLOAT_PROVIDER_TYPE);
    public static readonly Registry<MapCodec<IntProvider>> INT_PROVIDER_TYPE = RegisterSimple<MapCodec<IntProvider>>(Registries.INT_PROVIDER_TYPE);
    public static readonly Registry<HeightProviderType<object>> HEIGHT_PROVIDER_TYPE = RegisterSimple<HeightProviderType<object>>(Registries.HEIGHT_PROVIDER_TYPE);
    public static readonly Registry<BlockPredicateType> BLOCK_PREDICATE_TYPE = RegisterSimple<BlockPredicateType>(Registries.BLOCK_PREDICATE_TYPE);
    public static readonly Registry<WorldCarver> CARVER = RegisterSimple<WorldCarver>(Registries.CARVER);
    //CONFIGURED_CARVER configured carver registry; elements are loaded from worldgen/configured_carver/*.json
    public static readonly Registry<ConfiguredWorldCarver> CONFIGURED_CARVER = RegisterSimple<ConfiguredWorldCarver>(Registries.CONFIGURED_CARVER);
    //FEATURE feature type registry; elements are the feature singletons, registered by FeatureBootstrap
    public static readonly Registry<Feature> FEATURE = RegisterSimple<Feature>(Registries.FEATURE);
    //CONFIGURED_FEATURE configured feature registry; elements are loaded from worldgen/configured_feature/*.json
    public static readonly Registry<ConfiguredFeature> CONFIGURED_FEATURE = RegisterSimple<ConfiguredFeature>(Registries.CONFIGURED_FEATURE);
    //PLACED_FEATURE placed feature registry; elements are loaded from worldgen/placed_feature/*.json
    public static readonly Registry<PlacedFeature> PLACED_FEATURE = RegisterSimple<PlacedFeature>(Registries.PLACED_FEATURE);
    public static readonly Registry<StructurePlacementType<object>> STRUCTURE_PLACEMENT = RegisterSimple<StructurePlacementType<object>>(Registries.STRUCTURE_PLACEMENT);
    public static readonly Registry<StructurePieceType> STRUCTURE_PIECE = RegisterSimple<StructurePieceType>(Registries.STRUCTURE_PIECE);
    public static readonly Registry<StructureType<object>> STRUCTURE_TYPE = RegisterSimple<StructureType<object>>(Registries.STRUCTURE_TYPE);
    //STRUCTURE structure registry; elements are loaded from worldgen/structure/*.json
    //The Registry project cannot reference the Game layer, so the key uses a stub interface and the actual elements are Game-layer Structure implementations
    public static readonly Registry<Structure> STRUCTURE = RegisterSimple<Structure>(Registries.STRUCTURE);
    //STRUCTURE_SET structure set registry; elements are loaded from worldgen/structure_set/*.json
    public static readonly Registry<StructureSet> STRUCTURE_SET = RegisterSimple<StructureSet>(Registries.STRUCTURE_SET);
    //PROCESSOR_LIST processor list registry; elements are loaded from worldgen/processor_list/*.json
    public static readonly Registry<StructureProcessorList> PROCESSOR_LIST = RegisterSimple<StructureProcessorList>(Registries.PROCESSOR_LIST);
    //TEMPLATE_POOL template pool registry; elements are loaded from worldgen/template_pool/*.json
    public static readonly Registry<StructureTemplatePool> TEMPLATE_POOL = RegisterSimple<StructureTemplatePool>(Registries.TEMPLATE_POOL);
    public static readonly Registry<PlacementModifierType> PLACEMENT_MODIFIER_TYPE = RegisterSimple<PlacementModifierType>(Registries.PLACEMENT_MODIFIER_TYPE);
    public static readonly Registry<BlockStateProviderType<object>> BLOCKSTATE_PROVIDER_TYPE = RegisterSimple<BlockStateProviderType<object>>(Registries.BLOCK_STATE_PROVIDER_TYPE);
    public static readonly Registry<FoliagePlacerType<object>> FOLIAGE_PLACER_TYPE = RegisterSimple<FoliagePlacerType<object>>(Registries.FOLIAGE_PLACER_TYPE);
    public static readonly Registry<TrunkPlacerType<object>> TRUNK_PLACER_TYPE = RegisterSimple<TrunkPlacerType<object>>(Registries.TRUNK_PLACER_TYPE);
    public static readonly Registry<RootPlacerType<object>> ROOT_PLACER_TYPE = RegisterSimple<RootPlacerType<object>>(Registries.ROOT_PLACER_TYPE);
    public static readonly Registry<TreeDecoratorType<object>> TREE_DECORATOR_TYPE = RegisterSimple<TreeDecoratorType<object>>(Registries.TREE_DECORATOR_TYPE);
    public static readonly Registry<FeatureSizeType<object>> FEATURE_SIZE_TYPE = RegisterSimple<FeatureSizeType<object>>(Registries.FEATURE_SIZE_TYPE);
    public static readonly Registry<MapCodec<BiomeSource>> BIOME_SOURCE = RegisterSimple<MapCodec<BiomeSource>>(Registries.BIOME_SOURCE);
    public static readonly Registry<MapCodec<object>> CHUNK_GENERATOR = RegisterSimple<MapCodec<object>>(Registries.CHUNK_GENERATOR);
    public static readonly Registry<MapCodec<object>> MATERIAL_CONDITION = RegisterSimple<MapCodec<object>>(Registries.MATERIAL_CONDITION);
    public static readonly Registry<MapCodec<object>> MATERIAL_RULE = RegisterSimple<MapCodec<object>>(Registries.MATERIAL_RULE);
    public static readonly Registry<MapCodec<object>> DENSITY_FUNCTION_TYPE = RegisterSimple<MapCodec<object>>(Registries.DENSITY_FUNCTION_TYPE);
    //DENSITY_FUNCTION density function registry, letting density function JSON reference each other
    //Elements are DensityFunction; using the weak object type is a compromise because the Registry project cannot reference the Game layer
    public static readonly Registry<object> DENSITY_FUNCTION = RegisterSimple<object>(Registries.DENSITY_FUNCTION);
    //MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST multi-noise biome source parameter list, data/minecraft/worldgen/multi_noise_biome_source_parameter_list/*.json
    //Elements are MultiNoiseBiomeSourceParameterList, also keyed by object
    public static readonly Registry<object> MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST = RegisterSimple<object>(Registries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST);
    //NOISE_SETTINGS noise generator settings, data/minecraft/worldgen/noise_settings/*.json
    //Elements are NoiseGeneratorSettings, also keyed by object
    public static readonly Registry<object> NOISE_SETTINGS = RegisterSimple<object>(Registries.NOISE_SETTINGS);
    //DIMENSION_TYPE dimension type, data/minecraft/dimension_type/*.json
    public static readonly Registry<DimensionType> DIMENSION_TYPE =
        RegisterSimple<DimensionType>(Registries.DIMENSION_TYPE);
    //LEVEL_STEM level stem definition; elements come from the dimensions section of world_preset, not a separate directory
    public static readonly Registry<LevelStem> LEVEL_STEM = RegisterSimple<LevelStem>(Registries.LEVEL_STEM);
    public static readonly Registry<MapCodec<Block>> BLOCK_TYPE = RegisterSimple<MapCodec<Block>>(Registries.BLOCK_TYPE);
    public static readonly Registry<MapCodec<StructureProcessor>> STRUCTURE_PROCESSOR = RegisterSimple<MapCodec<StructureProcessor>>(Registries.STRUCTURE_PROCESSOR);
    public static readonly Registry<StructurePoolElementType<object>> STRUCTURE_POOL_ELEMENT = RegisterSimple<StructurePoolElementType<object>>(Registries.STRUCTURE_POOL_ELEMENT);
    public static readonly Registry<MapCodec<PoolAliasBinding>> POOL_ALIAS_BINDING_TYPE = RegisterSimple<MapCodec<PoolAliasBinding>>(Registries.POOL_ALIAS_BINDING);
    public static readonly Registry<DecoratedPotPattern> DECORATED_POT_PATTERN = RegisterSimple<DecoratedPotPattern>(Registries.DECORATED_POT_PATTERN);
    public static readonly Registry<CreativeModeTab> CREATIVE_MODE_TAB = RegisterSimple<CreativeModeTab>(Registries.CREATIVE_MODE_TAB);
    public static readonly Registry<CriterionTrigger<object>> TRIGGER_TYPES = RegisterSimple<CriterionTrigger<object>>(Registries.TRIGGER_TYPE);
    public static readonly Registry<NumberFormatType<object>> NUMBER_FORMAT_TYPE = RegisterSimple<NumberFormatType<object>>(Registries.NUMBER_FORMAT_TYPE);
    public static readonly Registry<object> DATA_COMPONENT_TYPE = RegisterSimple<object>(Registries.DATA_COMPONENT_TYPE);
    public static readonly Registry<GameRule<object>> GAME_RULE = RegisterSimple<GameRule<object>>(Registries.GAME_RULE);
    public static readonly Registry<Codec<EntitySubPredicate>> ENTITY_SUB_PREDICATE_TYPE = RegisterSimple<Codec<EntitySubPredicate>>(Registries.ENTITY_SUB_PREDICATE_TYPE);
    public static readonly Registry<DamageType> DAMAGE_TYPE = RegisterSimple<DamageType>(Registries.DAMAGE_TYPE);
    public static readonly Registry<DataComponentPredicateType<object>> DATA_COMPONENT_PREDICATE_TYPE = RegisterSimple<DataComponentPredicateType<object>>(Registries.DATA_COMPONENT_PREDICATE_TYPE);
    public static readonly Registry<MapDecorationType> MAP_DECORATION_TYPE = RegisterSimple<MapDecorationType>(Registries.MAP_DECORATION_TYPE);
    public static readonly Registry<object> ENCHANTMENT_EFFECT_COMPONENT_TYPE = RegisterSimple<object>(Registries.ENCHANTMENT_EFFECT_COMPONENT_TYPE);
    public static readonly Registry<MapCodec<LevelBasedValue>> ENCHANTMENT_LEVEL_BASED_VALUE_TYPE = RegisterSimple<MapCodec<LevelBasedValue>>(Registries.ENCHANTMENT_LEVEL_BASED_VALUE_TYPE);
    public static readonly Registry<MapCodec<EnchantmentEntityEffect>> ENCHANTMENT_ENTITY_EFFECT_TYPE = RegisterSimple<MapCodec<EnchantmentEntityEffect>>(Registries.ENCHANTMENT_ENTITY_EFFECT_TYPE);
    public static readonly Registry<MapCodec<EnchantmentLocationBasedEffect>> ENCHANTMENT_LOCATION_BASED_EFFECT_TYPE = RegisterSimple<MapCodec<EnchantmentLocationBasedEffect>>(Registries.ENCHANTMENT_LOCATION_BASED_EFFECT_TYPE);
    public static readonly Registry<MapCodec<EnchantmentValueEffect>> ENCHANTMENT_VALUE_EFFECT_TYPE = RegisterSimple<MapCodec<EnchantmentValueEffect>>(Registries.ENCHANTMENT_VALUE_EFFECT_TYPE);
    public static readonly Registry<MapCodec<EnchantmentProvider>> ENCHANTMENT_PROVIDER_TYPE = RegisterSimple<MapCodec<EnchantmentProvider>>(Registries.ENCHANTMENT_PROVIDER_TYPE);
    public static readonly Registry<ConsumeEffectType<object>> CONSUME_EFFECT_TYPE = RegisterSimple<ConsumeEffectType<object>>(Registries.CONSUME_EFFECT_TYPE);
    public static readonly Registry<RecipeDisplayType<object>> RECIPE_DISPLAY = RegisterSimple<RecipeDisplayType<object>>(Registries.RECIPE_DISPLAY);
    public static readonly Registry<SlotDisplayType<object>> SLOT_DISPLAY = RegisterSimple<SlotDisplayType<object>>(Registries.SLOT_DISPLAY);
    public static readonly Registry<RecipeBookCategory> RECIPE_BOOK_CATEGORY = RegisterSimple<RecipeBookCategory>(Registries.RECIPE_BOOK_CATEGORY);
    //TICKET_TYPE has no default value; declared as a writable registry so the nine built-in ticket types can be registered at once at startup
    public static readonly MappedRegistry<TicketType> TICKET_TYPE = RegisterSimple<TicketType>(Registries.TICKET_TYPE);
    public static readonly Registry<IncomingRpcMethod<object, object>> INCOMING_RPC_METHOD = RegisterSimple<IncomingRpcMethod<object, object>>(Registries.INCOMING_RPC_METHOD);
    public static readonly Registry<OutgoingRpcMethod<object, object>> OUTGOING_RPC_METHOD = RegisterSimple<OutgoingRpcMethod<object, object>>(Registries.OUTGOING_RPC_METHOD);
    public static readonly Registry<MapCodec<TestEnvironmentDefinition<object>>> TEST_ENVIRONMENT_DEFINITION_TYPE = RegisterSimple<MapCodec<TestEnvironmentDefinition<object>>>(Registries.TEST_ENVIRONMENT_DEFINITION_TYPE);
    public static readonly Registry<MapCodec<GameTestInstance>> TEST_INSTANCE_TYPE = RegisterSimple<MapCodec<GameTestInstance>>(Registries.TEST_INSTANCE_TYPE);
    public static readonly Registry<MapCodec<SpawnCondition>> SPAWN_CONDITION_TYPE = RegisterSimple<MapCodec<SpawnCondition>>(Registries.SPAWN_CONDITION_TYPE);
    public static readonly Registry<MapCodec<Dialog>> DIALOG_TYPE = RegisterSimple<MapCodec<Dialog>>(Registries.DIALOG_TYPE);
    public static readonly Registry<MapCodec<Action>> DIALOG_ACTION_TYPE = RegisterSimple<MapCodec<Action>>(Registries.DIALOG_ACTION_TYPE);
    public static readonly Registry<MapCodec<InputControl>> INPUT_CONTROL_TYPE = RegisterSimple<MapCodec<InputControl>>(Registries.INPUT_CONTROL_TYPE);
    public static readonly Registry<MapCodec<DialogBody>> DIALOG_BODY_TYPE = RegisterSimple<MapCodec<DialogBody>>(Registries.DIALOG_BODY_TYPE);
    public static readonly Registry<MapCodec<Permission>> PERMISSION_TYPE = RegisterSimple<MapCodec<Permission>>(Registries.PERMISSION_TYPE);
    public static readonly Registry<MapCodec<PermissionCheck>> PERMISSION_CHECK_TYPE = RegisterSimple<MapCodec<PermissionCheck>>(Registries.PERMISSION_CHECK_TYPE);
    public static readonly Registry<IEnvironmentAttribute> ENVIRONMENT_ATTRIBUTE = RegisterSimple<IEnvironmentAttribute>(Registries.ENVIRONMENT_ATTRIBUTE);
    public static readonly Registry<AttributeType<object>> ATTRIBUTE_TYPE = RegisterSimple<AttributeType<object>>(Registries.ATTRIBUTE_TYPE);
    public static readonly Registry<MapCodec<SlotSource>> SLOT_SOURCE_TYPE = RegisterSimple<MapCodec<SlotSource>>(Registries.SLOT_SOURCE_TYPE);
    public static readonly Registry<Consumer<GameTestHelper>> TEST_FUNCTION = RegisterSimple<Consumer<GameTestHelper>>(Registries.TEST_FUNCTION);
    //WORLD_CLOCK/TIMELINE registration order must match the lexicographic order of the SynchronizedRegistryData sync list
    public static readonly Registry<WorldClock> WORLD_CLOCK = RegisterSimple<WorldClock>(Registries.WORLD_CLOCK);
    public static readonly Registry<Timeline> TIMELINE = RegisterSimple<Timeline>(Registries.TIMELINE);
    //ENCHANTMENT/JUKEBOX_SONG/TRIM_MATERIAL/TRIM_PATTERN used by enchantment, jukebox and trim components and predicates
    public static readonly Registry<Enchantment> ENCHANTMENT = RegisterSimple<Enchantment>(Registries.ENCHANTMENT);
    public static readonly Registry<JukeboxSong> JUKEBOX_SONG = RegisterSimple<JukeboxSong>(Registries.JUKEBOX_SONG);
    public static readonly Registry<TrimMaterial> TRIM_MATERIAL = RegisterSimple<TrimMaterial>(Registries.TRIM_MATERIAL);
    public static readonly Registry<TrimPattern> TRIM_PATTERN = RegisterSimple<TrimPattern>(Registries.TRIM_PATTERN);

    //Simple registry registration
    //Returns the concrete type rather than Registry<T> so startup can register by name one by one (for tables without defaults like TICKET_TYPE)
    private static MappedRegistry<T> RegisterSimple<T>(ResourceKey<Registry<T>> key) where T : class
        => new MappedRegistry<T>(key, Lifecycle.Stable);

    //Defaulted registry registration
    private static DefaultedRegistry<T> RegisterDefaulted<T>(ResourceKey<Registry<T>> key, string defaultKey) where T : class
        => new DefaultedMappedRegistry<T>(defaultKey, key, Lifecycle.Stable);

    //TODO bootstrap: vanilla bootStrap calls each registry's bootstrap callback; nothing at the stub stage
    public static void BootStrap()
    {
        //Log.Debug($"BootStrap enter");
        //Log.Debug($"BootStrap exit");
    }

    //CreateRegistryAccess reflectively collects all static Registry<T> fields into an immutable RegistryAccess
    //Used by ReloadableServerResources.LoadResources for TagManager.BindAll
    //Must be called after BootstrapClass.BootStrap because BindAll requires the registries to be frozen
    public static Frozen CreateRegistryAccess()
    {
        var entries = new List<RegistryEntry>();
        foreach (var (id, registry) in EnumerateRegistries())
            entries.Add(new RegistryEntry(id, registry));
        return new ImmutableRegistryAccess(entries);
    }

    //EnumerateRegistries reflectively walks all static Registry<T> fields and returns (registry Identifier, instance)
    //Reuses the ValidateRegistries reflection pattern to avoid duplicated iteration logic
    public static IEnumerable<(Identifier Key, object Value)> EnumerateRegistries()
    {
        foreach (var field in typeof(BuiltInRegistries).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            //The field's declared type may be Registry<T> itself (like DATA_COMPONENT_TYPE) or a derived interface such as DefaultedRegistry<T>
            //GetInterfaces does not return the interface itself, so testing only inheritance would miss the former and leave those registries neither frozen nor present in RegistryAccess
            var fieldType = field.FieldType;
            var isRegistry = fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(Registry<>)
                || fieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Registry<>));
            if (!isRegistry) continue;
            var registry = field.GetValue(null);
            if (registry == null) continue;
            //Reflectively read registry.Key.Identifier since generic invariance prevents casting to ResourceKey<Registry<T>>
            var keyObj = registry.GetType().GetProperty("Key")?.GetValue(registry);
            var identifier = keyObj?.GetType().GetProperty("Identifier")?.GetValue(keyObj) as Identifier?;
            if (identifier is null) continue;
            yield return (identifier.Value, registry);
        }
    }
}
