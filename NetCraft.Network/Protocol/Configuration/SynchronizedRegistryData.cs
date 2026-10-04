//SynchronizedRegistryData 26.2 原版 SYNCHRONIZED_REGISTRIES 除 biome 外 28 个注册表的 vanilla id 列表
//从原版 jar data/minecraft/<registry>/*.json 按字典序提取对齐原版资源加载顺序
//entry 全部省略 contents(存在标记 0)客户端按 known pack 从本地 vanilla 资源加载实际数据
//biome 由服务端注册表自带(带 contents)不在此列
using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Configuration;

public static class SynchronizedRegistryData
{
    //chat_type 7 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] ChatType = { "minecraft:chat", "minecraft:emote_command", "minecraft:msg_command_incoming", "minecraft:msg_command_outgoing", "minecraft:say_command", "minecraft:team_msg_command_incoming", "minecraft:team_msg_command_outgoing" };

    //trim_pattern 18 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] TrimPattern = { "minecraft:bolt", "minecraft:coast", "minecraft:dune", "minecraft:eye", "minecraft:flow", "minecraft:host", "minecraft:raiser", "minecraft:rib", "minecraft:sentry", "minecraft:shaper", "minecraft:silence", "minecraft:snout", "minecraft:spire", "minecraft:tide", "minecraft:vex", "minecraft:ward", "minecraft:wayfinder", "minecraft:wild" };

    //trim_material 11 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] TrimMaterial = { "minecraft:amethyst", "minecraft:copper", "minecraft:diamond", "minecraft:emerald", "minecraft:gold", "minecraft:iron", "minecraft:lapis", "minecraft:netherite", "minecraft:quartz", "minecraft:redstone", "minecraft:resin" };

    //wolf_variant 9 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] WolfVariant = { "minecraft:ashen", "minecraft:black", "minecraft:chestnut", "minecraft:pale", "minecraft:rusty", "minecraft:snowy", "minecraft:spotted", "minecraft:striped", "minecraft:woods" };

    //wolf_sound_variant 7 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] WolfSoundVariant = { "minecraft:angry", "minecraft:big", "minecraft:classic", "minecraft:cute", "minecraft:grumpy", "minecraft:puglin", "minecraft:sad" };

    //pig_variant 3 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] PigVariant = { "minecraft:cold", "minecraft:temperate", "minecraft:warm" };

    //pig_sound_variant 3 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] PigSoundVariant = { "minecraft:big", "minecraft:classic", "minecraft:mini" };

    //frog_variant 3 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] FrogVariant = { "minecraft:cold", "minecraft:temperate", "minecraft:warm" };

    //cat_variant 11 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] CatVariant = { "minecraft:all_black", "minecraft:black", "minecraft:british_shorthair", "minecraft:calico", "minecraft:jellie", "minecraft:persian", "minecraft:ragdoll", "minecraft:red", "minecraft:siamese", "minecraft:tabby", "minecraft:white" };

    //cat_sound_variant 2 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] CatSoundVariant = { "minecraft:classic", "minecraft:royal" };

    //cow_sound_variant 2 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] CowSoundVariant = { "minecraft:classic", "minecraft:moody" };

    //cow_variant 3 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] CowVariant = { "minecraft:cold", "minecraft:temperate", "minecraft:warm" };

    //chicken_sound_variant 2 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] ChickenSoundVariant = { "minecraft:classic", "minecraft:picky" };

    //chicken_variant 3 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] ChickenVariant = { "minecraft:cold", "minecraft:temperate", "minecraft:warm" };

    //zombie_nautilus_variant 2 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] ZombieNautilusVariant = { "minecraft:temperate", "minecraft:warm" };

    //painting_variant 51 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] PaintingVariant = { "minecraft:alban", "minecraft:aztec", "minecraft:aztec2", "minecraft:backyard", "minecraft:baroque", "minecraft:bomb", "minecraft:bouquet", "minecraft:burning_skull", "minecraft:bust", "minecraft:cavebird", "minecraft:changing", "minecraft:cotan", "minecraft:courbet", "minecraft:creebet", "minecraft:dennis", "minecraft:donkey_kong", "minecraft:earth", "minecraft:endboss", "minecraft:fern", "minecraft:fighters", "minecraft:finding", "minecraft:fire", "minecraft:graham", "minecraft:humble", "minecraft:kebab", "minecraft:lowmist", "minecraft:match", "minecraft:meditative", "minecraft:orb", "minecraft:owlemons", "minecraft:passage", "minecraft:pigscene", "minecraft:plant", "minecraft:pointer", "minecraft:pond", "minecraft:pool", "minecraft:prairie_ride", "minecraft:sea", "minecraft:skeleton", "minecraft:skull_and_roses", "minecraft:stage", "minecraft:sunflowers", "minecraft:sunset", "minecraft:tides", "minecraft:unpacked", "minecraft:void", "minecraft:wanderer", "minecraft:wasteland", "minecraft:water", "minecraft:wind", "minecraft:wither" };

    //sulfur_cube_archetype 12 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] SulfurCubeArchetype = { "minecraft:bouncy", "minecraft:explosive", "minecraft:fast_flat", "minecraft:fast_sliding", "minecraft:high_resistance", "minecraft:hot", "minecraft:light", "minecraft:regular", "minecraft:slow_bouncy", "minecraft:slow_flat", "minecraft:slow_sliding", "minecraft:sticky" };

    //dimension_type 4 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] DimensionType = { "minecraft:overworld", "minecraft:overworld_caves", "minecraft:the_end", "minecraft:the_nether" };

    //damage_type 51 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] DamageType = { "minecraft:arrow", "minecraft:bad_respawn_point", "minecraft:cactus", "minecraft:campfire", "minecraft:cramming", "minecraft:dragon_breath", "minecraft:drown", "minecraft:dry_out", "minecraft:ender_pearl", "minecraft:explosion", "minecraft:fall", "minecraft:falling_anvil", "minecraft:falling_block", "minecraft:falling_stalactite", "minecraft:fireball", "minecraft:fireworks", "minecraft:fly_into_wall", "minecraft:freeze", "minecraft:generic", "minecraft:generic_kill", "minecraft:hot_floor", "minecraft:in_fire", "minecraft:in_wall", "minecraft:indirect_magic", "minecraft:lava", "minecraft:lightning_bolt", "minecraft:mace_smash", "minecraft:magic", "minecraft:mob_attack", "minecraft:mob_attack_no_aggro", "minecraft:mob_projectile", "minecraft:on_fire", "minecraft:out_of_world", "minecraft:outside_border", "minecraft:player_attack", "minecraft:player_explosion", "minecraft:sonic_boom", "minecraft:spear", "minecraft:spit", "minecraft:stalagmite", "minecraft:starve", "minecraft:sting", "minecraft:sulfur_cube_hot", "minecraft:sweet_berry_bush", "minecraft:thorns", "minecraft:thrown", "minecraft:trident", "minecraft:unattributed_fireball", "minecraft:wind_charge", "minecraft:wither", "minecraft:wither_skull" };

    //banner_pattern 43 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] BannerPattern = { "minecraft:base", "minecraft:border", "minecraft:bricks", "minecraft:circle", "minecraft:creeper", "minecraft:cross", "minecraft:curly_border", "minecraft:diagonal_left", "minecraft:diagonal_right", "minecraft:diagonal_up_left", "minecraft:diagonal_up_right", "minecraft:flow", "minecraft:flower", "minecraft:globe", "minecraft:gradient", "minecraft:gradient_up", "minecraft:guster", "minecraft:half_horizontal", "minecraft:half_horizontal_bottom", "minecraft:half_vertical", "minecraft:half_vertical_right", "minecraft:mojang", "minecraft:piglin", "minecraft:rhombus", "minecraft:skull", "minecraft:small_stripes", "minecraft:square_bottom_left", "minecraft:square_bottom_right", "minecraft:square_top_left", "minecraft:square_top_right", "minecraft:straight_cross", "minecraft:stripe_bottom", "minecraft:stripe_center", "minecraft:stripe_downleft", "minecraft:stripe_downright", "minecraft:stripe_left", "minecraft:stripe_middle", "minecraft:stripe_right", "minecraft:stripe_top", "minecraft:triangle_bottom", "minecraft:triangle_top", "minecraft:triangles_bottom", "minecraft:triangles_top" };

    //enchantment 43 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] Enchantment = { "minecraft:aqua_affinity", "minecraft:bane_of_arthropods", "minecraft:binding_curse", "minecraft:blast_protection", "minecraft:breach", "minecraft:channeling", "minecraft:density", "minecraft:depth_strider", "minecraft:efficiency", "minecraft:feather_falling", "minecraft:fire_aspect", "minecraft:fire_protection", "minecraft:flame", "minecraft:fortune", "minecraft:frost_walker", "minecraft:impaling", "minecraft:infinity", "minecraft:knockback", "minecraft:looting", "minecraft:loyalty", "minecraft:luck_of_the_sea", "minecraft:lunge", "minecraft:lure", "minecraft:mending", "minecraft:multishot", "minecraft:piercing", "minecraft:power", "minecraft:projectile_protection", "minecraft:protection", "minecraft:punch", "minecraft:quick_charge", "minecraft:respiration", "minecraft:riptide", "minecraft:sharpness", "minecraft:silk_touch", "minecraft:smite", "minecraft:soul_speed", "minecraft:sweeping_edge", "minecraft:swift_sneak", "minecraft:thorns", "minecraft:unbreaking", "minecraft:vanishing_curse", "minecraft:wind_burst" };

    //jukebox_song 22 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] JukeboxSong = { "minecraft:11", "minecraft:13", "minecraft:5", "minecraft:blocks", "minecraft:bounce", "minecraft:cat", "minecraft:chirp", "minecraft:creator", "minecraft:creator_music_box", "minecraft:far", "minecraft:lava_chicken", "minecraft:mall", "minecraft:mellohi", "minecraft:otherside", "minecraft:pigstep", "minecraft:precipice", "minecraft:relic", "minecraft:stal", "minecraft:strad", "minecraft:tears", "minecraft:wait", "minecraft:ward" };

    //instrument 8 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] Instrument = { "minecraft:admire_goat_horn", "minecraft:call_goat_horn", "minecraft:dream_goat_horn", "minecraft:feel_goat_horn", "minecraft:ponder_goat_horn", "minecraft:seek_goat_horn", "minecraft:sing_goat_horn", "minecraft:yearn_goat_horn" };

    //test_environment 1 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] TestEnvironment = { "minecraft:default" };

    //test_instance 1 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] TestInstance = { "minecraft:always_pass" };

    //dialog 3 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] Dialog = { "minecraft:custom_options", "minecraft:quick_actions", "minecraft:server_links" };

    //world_clock 2 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] WorldClock = { "minecraft:overworld", "minecraft:the_end" };

    //timeline 4 个 entry 按字典序对齐原版资源加载顺序
    private static readonly string[] Timeline = { "minecraft:day", "minecraft:early_game", "minecraft:moon", "minecraft:villager_schedule" };


    //All 按原版 RegistryDataLoader.SYNCHRONIZED_REGISTRIES 顺序返回(注册表 id, entry id 列表)
    public static readonly (string Registry, string[] Ids)[] All =
    {
        ("minecraft:chat_type", ChatType),
        ("minecraft:trim_pattern", TrimPattern),
        ("minecraft:trim_material", TrimMaterial),
        ("minecraft:wolf_variant", WolfVariant),
        ("minecraft:wolf_sound_variant", WolfSoundVariant),
        ("minecraft:pig_variant", PigVariant),
        ("minecraft:pig_sound_variant", PigSoundVariant),
        ("minecraft:frog_variant", FrogVariant),
        ("minecraft:cat_variant", CatVariant),
        ("minecraft:cat_sound_variant", CatSoundVariant),
        ("minecraft:cow_sound_variant", CowSoundVariant),
        ("minecraft:cow_variant", CowVariant),
        ("minecraft:chicken_sound_variant", ChickenSoundVariant),
        ("minecraft:chicken_variant", ChickenVariant),
        ("minecraft:zombie_nautilus_variant", ZombieNautilusVariant),
        ("minecraft:painting_variant", PaintingVariant),
        ("minecraft:sulfur_cube_archetype", SulfurCubeArchetype),
        ("minecraft:dimension_type", DimensionType),
        ("minecraft:damage_type", DamageType),
        ("minecraft:banner_pattern", BannerPattern),
        ("minecraft:enchantment", Enchantment),
        ("minecraft:jukebox_song", JukeboxSong),
        ("minecraft:instrument", Instrument),
        ("minecraft:test_environment", TestEnvironment),
        ("minecraft:test_instance", TestInstance),
        ("minecraft:dialog", Dialog),
        ("minecraft:world_clock", WorldClock),
        ("minecraft:timeline", Timeline),
    };

    //GetEntryId 查注册表条目在同步顺序中的 int id 未命中返回 -1
    //原版 holderRegistry codec 用该 id 编码登录包的维度类型 必须与同步给客户端的顺序一致
    public static int GetEntryId(string registry, Identifier entry)
    {
        foreach (var (reg, ids) in All)
        {
            if (reg != registry) continue;
            for (var i = 0; i < ids.Length; i++)
                if (Identifier.Parse(ids[i]) == entry) return i;
            break;
        }
        return -1;
    }

    //GetEntry 按 int id 反查注册表条目标识符 越界返回 null
    public static Identifier? GetEntry(string registry, int id)
    {
        foreach (var (reg, ids) in All)
        {
            if (reg != registry) continue;
            return id >= 0 && id < ids.Length ? Identifier.Parse(ids[id]) : null;
        }
        return null;
    }
}
