namespace NetCraft.DataFixer.Fixes;

using System;

//MC类型引用集合对应原版net.minecraft.util.datafix.fixes.References
//每个引用是DSL.ITypeReference实例按type名从Schema取模板
//原版部分字段引用Display/UnbakedModel/SpawnData常量这里用字面值替代
public static class References
{
    public static readonly DSL.ITypeReference Level = Reference("level");
    public static readonly DSL.ITypeReference LightweightLevel = Reference("lightweight_level");
    public static readonly DSL.ITypeReference Player = Reference("player");
    public static readonly DSL.ITypeReference Chunk = Reference("chunk");
    public static readonly DSL.ITypeReference Hotbar = Reference("hotbar");
    public static readonly DSL.ITypeReference Options = Reference("options");
    public static readonly DSL.ITypeReference Structure = Reference("structure");
    public static readonly DSL.ITypeReference Stats = Reference("stats");
    public static readonly DSL.ITypeReference SavedDataCommandStorage = Reference("saved_data/command_storage");
    public static readonly DSL.ITypeReference SavedDataCustomBossEvents = Reference("saved_data/custom_boss_events");
    public static readonly DSL.ITypeReference SavedDataEnderDragonFight = Reference("saved_data/ender_dragon_fight");
    public static readonly DSL.ITypeReference SavedDataGameRules = Reference("saved_data/game_rules");
    public static readonly DSL.ITypeReference SavedDataTickets = Reference("saved_data/tickets");
    public static readonly DSL.ITypeReference SavedDataMapData = Reference("saved_data/map_data");
    public static readonly DSL.ITypeReference SavedDataMapIndex = Reference("saved_data/idcounts");
    public static readonly DSL.ITypeReference SavedDataRaids = Reference("saved_data/raids");
    public static readonly DSL.ITypeReference SavedDataRandomSequences = Reference("saved_data/random_sequences");
    public static readonly DSL.ITypeReference SavedDataScheduledEvents = Reference("saved_data/scheduled_events");
    public static readonly DSL.ITypeReference SavedDataScoreboard = Reference("saved_data/scoreboard");
    public static readonly DSL.ITypeReference SavedDataStopwatches = Reference("saved_data/stopwatches");
    public static readonly DSL.ITypeReference SavedDataStructureFeatureIndices = Reference("saved_data/structure_feature_indices");
    public static readonly DSL.ITypeReference SavedDataWanderingTrader = Reference("saved_data/wandering_trader");
    public static readonly DSL.ITypeReference SavedDataWeather = Reference("saved_data/weather");
    public static readonly DSL.ITypeReference SavedDataWorldBorder = Reference("saved_data/world_border");
    public static readonly DSL.ITypeReference SavedDataWorldClocks = Reference("saved_data/world_clocks");
    public static readonly DSL.ITypeReference SavedDataWorldGenSettings = Reference("saved_data/world_gen_settings");
    public static readonly DSL.ITypeReference Advancements = Reference("advancements");
    public static readonly DSL.ITypeReference PoiChunk = Reference("poi_chunk");
    public static readonly DSL.ITypeReference EntityChunk = Reference("entity_chunk");
    public static readonly DSL.ITypeReference DebugProfile = Reference("debug_profile");
    public static readonly DSL.ITypeReference BlockEntity = Reference("block_entity");
    public static readonly DSL.ITypeReference ItemStack = Reference("item_stack");
    public static readonly DSL.ITypeReference BlockState = Reference("block_state");
    public static readonly DSL.ITypeReference FlatBlockState = Reference("flat_block_state");
    public static readonly DSL.ITypeReference DataComponents = Reference("data_components");
    public static readonly DSL.ITypeReference VillagerTrade = Reference("villager_trade");
    public static readonly DSL.ITypeReference Particle = Reference("particle");
    public static readonly DSL.ITypeReference TextComponent = Reference("text_component");
    public static readonly DSL.ITypeReference EntityEquipment = Reference("entity_equipment");
    public static readonly DSL.ITypeReference EntityName = Reference("entity_name");
    public static readonly DSL.ITypeReference EntityTree = Reference("entity_tree");
    public static readonly DSL.ITypeReference Entity = Reference("entity");
    public static readonly DSL.ITypeReference BlockName = Reference("block_name");
    public static readonly DSL.ITypeReference ItemName = Reference("item_name");
    public static readonly DSL.ITypeReference GameEventName = Reference("game_event_name");
    public static readonly DSL.ITypeReference UntaggedSpawner = Reference("untagged_spawner");
    public static readonly DSL.ITypeReference StructureFeature = Reference("structure_feature");
    public static readonly DSL.ITypeReference Objective = Reference("objective");
    public static readonly DSL.ITypeReference Team = Reference("team");
    public static readonly DSL.ITypeReference Recipe = Reference("recipe");
    public static readonly DSL.ITypeReference Biome = Reference("biome");
    public static readonly DSL.ITypeReference MultiNoiseBiomeSourceParameterList = Reference("multi_noise_biome_source_parameter_list");
    public static readonly DSL.ITypeReference WorldGenSettings = Reference("world_gen_settings");

    //ExampleCounter端到端示例引用非原版MC类型仅用于验证DFU流程
    public static readonly DSL.ITypeReference ExampleCounter = Reference("example_counter");

    //reference按id构造ITypeReference匿名实例
    public static DSL.ITypeReference Reference(string id) => new TypeReferenceImpl(id);

    //内部ITypeReference实现typeName返回id toString返回@id
    private sealed class TypeReferenceImpl : DSL.ITypeReference
    {
        private readonly string _id;
        internal TypeReferenceImpl(string id) => _id = id;
        public string TypeName() => _id;
        public override string ToString() => "@" + _id;
    }
}
