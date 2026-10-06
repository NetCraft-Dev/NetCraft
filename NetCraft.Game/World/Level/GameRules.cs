using NetCraft.Registry;

namespace NetCraft.Game.World.Level;

//GameRules game rule definition table, maps to vanilla net.minecraft.world.level.gamerules.GameRules
//Names, types, defaults and value ranges align with the declaration order of vanilla 26.2 static blocks
//Runtime values are held by GameRuleMapData; this class only provides definitions, registration and lookup by name
public static class GameRules
{
    public static readonly GameRule<object> AdvanceTime = Boolean("advance_time", true);
    public static readonly GameRule<object> AdvanceWeather = Boolean("advance_weather", true);
    public static readonly GameRule<object> AllowEnteringNetherUsingPortals = Boolean("allow_entering_nether_using_portals", true);
    public static readonly GameRule<object> BlockDrops = Boolean("block_drops", true);
    public static readonly GameRule<object> BlockExplosionDropDecay = Boolean("block_explosion_drop_decay", true);
    public static readonly GameRule<object> CommandBlocksWork = Boolean("command_blocks_work", true);
    public static readonly GameRule<object> CommandBlockOutput = Boolean("command_block_output", true);
    public static readonly GameRule<object> DrowningDamage = Boolean("drowning_damage", true);
    public static readonly GameRule<object> ElytraMovementCheck = Boolean("elytra_movement_check", true);
    public static readonly GameRule<object> EnderPearlsVanishOnDeath = Boolean("ender_pearls_vanish_on_death", true);
    public static readonly GameRule<object> EntityDrops = Boolean("entity_drops", true);
    public static readonly GameRule<object> FallDamage = Boolean("fall_damage", true);
    public static readonly GameRule<object> FireDamage = Boolean("fire_damage", true);
    public static readonly GameRule<object> FireSpreadRadiusAroundPlayer = Integer("fire_spread_radius_around_player", 128, -1);
    public static readonly GameRule<object> ForgiveDeadPlayers = Boolean("forgive_dead_players", true);
    public static readonly GameRule<object> FreezeDamage = Boolean("freeze_damage", true);
    public static readonly GameRule<object> GlobalSoundEvents = Boolean("global_sound_events", true);
    public static readonly GameRule<object> ImmediateRespawn = Boolean("immediate_respawn", false);
    public static readonly GameRule<object> KeepInventory = Boolean("keep_inventory", false);
    public static readonly GameRule<object> LavaSourceConversion = Boolean("lava_source_conversion", false);
    public static readonly GameRule<object> LimitedCrafting = Boolean("limited_crafting", false);
    public static readonly GameRule<object> LocatorBar = Boolean("locator_bar", true);
    public static readonly GameRule<object> LogAdminCommands = Boolean("log_admin_commands", true);
    public static readonly GameRule<object> MaxBlockModifications = Integer("max_block_modifications", 32768, 1);
    public static readonly GameRule<object> MaxCommandForks = Integer("max_command_forks", 65536, 0);
    public static readonly GameRule<object> MaxCommandSequenceLength = Integer("max_command_sequence_length", 65536, 0);
    public static readonly GameRule<object> MaxEntityCramming = Integer("max_entity_cramming", 24, 0);
    public static readonly GameRule<object> MaxMinecartSpeed = Integer("max_minecart_speed", 8, 1, 1000);
    public static readonly GameRule<object> MaxSnowAccumulationHeight = Integer("max_snow_accumulation_height", 1, 0, 8);
    public static readonly GameRule<object> MobDrops = Boolean("mob_drops", true);
    public static readonly GameRule<object> MobExplosionDropDecay = Boolean("mob_explosion_drop_decay", true);
    public static readonly GameRule<object> MobGriefing = Boolean("mob_griefing", true);
    public static readonly GameRule<object> NaturalHealthRegeneration = Boolean("natural_health_regeneration", true);
    public static readonly GameRule<object> PlayerMovementCheck = Boolean("player_movement_check", true);
    public static readonly GameRule<object> PlayersNetherPortalCreativeDelay = Integer("players_nether_portal_creative_delay", 0, 0);
    public static readonly GameRule<object> PlayersNetherPortalDefaultDelay = Integer("players_nether_portal_default_delay", 80, 0);
    public static readonly GameRule<object> PlayersSleepingPercentage = Integer("players_sleeping_percentage", 100, 0);
    public static readonly GameRule<object> ProjectilesCanBreakBlocks = Boolean("projectiles_can_break_blocks", true);
    public static readonly GameRule<object> Pvp = Boolean("pvp", true);
    public static readonly GameRule<object> Raids = Boolean("raids", true);
    public static readonly GameRule<object> RandomTickSpeed = Integer("random_tick_speed", 3, 0);
    public static readonly GameRule<object> ReducedDebugInfo = Boolean("reduced_debug_info", false);
    public static readonly GameRule<object> RespawnRadius = Integer("respawn_radius", 10, 0);
    public static readonly GameRule<object> SendCommandFeedback = Boolean("send_command_feedback", true);
    public static readonly GameRule<object> ShowAdvancementMessages = Boolean("show_advancement_messages", true);
    public static readonly GameRule<object> ShowDeathMessages = Boolean("show_death_messages", true);
    public static readonly GameRule<object> SpawnerBlocksWork = Boolean("spawner_blocks_work", true);
    public static readonly GameRule<object> SpawnMobs = Boolean("spawn_mobs", true);
    public static readonly GameRule<object> SpawnMonsters = Boolean("spawn_monsters", true);
    public static readonly GameRule<object> SpawnPatrols = Boolean("spawn_patrols", true);
    public static readonly GameRule<object> SpawnPhantoms = Boolean("spawn_phantoms", true);
    public static readonly GameRule<object> SpawnWanderingTraders = Boolean("spawn_wandering_traders", true);
    public static readonly GameRule<object> SpawnWardens = Boolean("spawn_wardens", true);
    public static readonly GameRule<object> SpectatorsGenerateChunks = Boolean("spectators_generate_chunks", true);
    public static readonly GameRule<object> SpreadVines = Boolean("spread_vines", true);
    public static readonly GameRule<object> TntExplodes = Boolean("tnt_explodes", true);
    public static readonly GameRule<object> TntExplosionDropDecay = Boolean("tnt_explosion_drop_decay", false);
    public static readonly GameRule<object> UniversalAnger = Boolean("universal_anger", false);
    public static readonly GameRule<object> WaterSourceConversion = Boolean("water_source_conversion", true);

    //All all rules in registration order, matching the vanilla static block order
    public static readonly GameRule<object>[] All =
    {
        AdvanceTime, AdvanceWeather, AllowEnteringNetherUsingPortals, BlockDrops, BlockExplosionDropDecay,
        CommandBlocksWork, CommandBlockOutput, DrowningDamage, ElytraMovementCheck, EnderPearlsVanishOnDeath,
        EntityDrops, FallDamage, FireDamage, FireSpreadRadiusAroundPlayer, ForgiveDeadPlayers,
        FreezeDamage, GlobalSoundEvents, ImmediateRespawn, KeepInventory, LavaSourceConversion,
        LimitedCrafting, LocatorBar, LogAdminCommands, MaxBlockModifications, MaxCommandForks,
        MaxCommandSequenceLength, MaxEntityCramming, MaxMinecartSpeed, MaxSnowAccumulationHeight, MobDrops,
        MobExplosionDropDecay, MobGriefing, NaturalHealthRegeneration, PlayerMovementCheck,
        PlayersNetherPortalCreativeDelay, PlayersNetherPortalDefaultDelay, PlayersSleepingPercentage,
        ProjectilesCanBreakBlocks, Pvp, Raids,
        RandomTickSpeed, ReducedDebugInfo, RespawnRadius, SendCommandFeedback, ShowAdvancementMessages,
        ShowDeathMessages, SpawnerBlocksWork, SpawnMobs, SpawnMonsters, SpawnPatrols,
        SpawnPhantoms, SpawnWanderingTraders, SpawnWardens, SpectatorsGenerateChunks, SpreadVines,
        TntExplodes, TntExplosionDropDecay, UniversalAnger, WaterSourceConversion,
    };

    //Bootstrap register all rules into BuiltInRegistries.GAME_RULE, must be called before the registry is frozen
    public static void Bootstrap()
    {
        foreach (var rule in All)
            Registry<GameRule<object>>.Register(BuiltInRegistries.GAME_RULE, rule.Id, rule);
    }

    //Find lookup by rule name, both short names and namespaced forms match, returns null if not found
    public static GameRule<object>? Find(string name)
    {
        var id = Identifier.TryParse(name.Contains(':') ? name : "minecraft:" + name);
        return id is null ? null : BuiltInRegistries.GAME_RULE.GetValue(id.Value);
    }

    //Boolean declare a boolean rule
    private static GameRule<object> Boolean(string path, bool defaultValue)
        => new(Identifier.WithDefaultNamespace(path), GameRuleType.Bool, defaultValue);

    //Integer declare an integer rule, uses int.MaxValue when unbounded, matching vanilla IntegerArgumentType.integer(min, max)
    private static GameRule<object> Integer(string path, int defaultValue, int min, int max = int.MaxValue)
        => new(Identifier.WithDefaultNamespace(path), GameRuleType.Int, defaultValue, min, max);
}
