using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block;

//BlockTags 本作接进来的方块标签 对应原版 net.minecraft.tags.BlockTags
//标签值由数据包 data/<ns>/tags/block/*.json 提供 启动时经 TagsReloadListener 绑到注册表上
//这里只登记已用到的那些 原版那几百条等用到再补
//supports_* 一族是 26.2 的附着系统: 植被与作物"能种在什么上面"全按标签判 不再写死在代码里
public static class BlockTags
{
    //SupportsVegetation 普通植被的落脚面 草花树苗灌木一类共用 对应原版 supports_vegetation
    public static readonly TagKey<NetCraft.Registry.Block> SupportsVegetation = Create("supports_vegetation");

    //SupportsDryVegetation 枯草与枯灌木的落脚面 对应原版 supports_dry_vegetation
    public static readonly TagKey<NetCraft.Registry.Block> SupportsDryVegetation = Create("supports_dry_vegetation");

    //SupportsCrops 作物落脚面 实际就是耕地 对应原版 supports_crops
    public static readonly TagKey<NetCraft.Registry.Block> SupportsCrops = Create("supports_crops");

    //SupportsStemCrops 瓜梗落脚面 对应原版 supports_stem_crops
    public static readonly TagKey<NetCraft.Registry.Block> SupportsStemCrops = Create("supports_stem_crops");

    //SupportsStemFruit 瓜梗结果那格要贴着的面 对应原版 supports_stem_fruit
    public static readonly TagKey<NetCraft.Registry.Block> SupportsStemFruit = Create("supports_stem_fruit");

    //SupportsSugarCane 甘蔗落脚面 对应原版 supports_sugar_cane
    public static readonly TagKey<NetCraft.Registry.Block> SupportsSugarCane = Create("supports_sugar_cane");

    //SupportsSugarCaneAdjacently 甘蔗旁边要挨着什么才能长 对应原版 supports_sugar_cane_adjacently
    public static readonly TagKey<NetCraft.Registry.Block> SupportsSugarCaneAdjacently = Create("supports_sugar_cane_adjacently");

    //SupportsBamboo 竹子的落脚面 对应原版 supports_bamboo
    public static readonly TagKey<NetCraft.Registry.Block> SupportsBamboo = Create("supports_bamboo");

    //SupportsSmallDripleaf 小型垂滴叶落脚面 对应原版 supports_small_dripleaf
    public static readonly TagKey<NetCraft.Registry.Block> SupportsSmallDripleaf = Create("supports_small_dripleaf");

    //SupportsBigDripleaf 大型垂滴叶落脚面 对应原版 supports_big_dripleaf
    public static readonly TagKey<NetCraft.Registry.Block> SupportsBigDripleaf = Create("supports_big_dripleaf");

    //SupportsCactus 仙人掌落脚面 对应原版 supports_cactus
    public static readonly TagKey<NetCraft.Registry.Block> SupportsCactus = Create("supports_cactus");

    //SupportsChorusPlant 紫颂植株落脚面 对应原版 supports_chorus_plant
    public static readonly TagKey<NetCraft.Registry.Block> SupportsChorusPlant = Create("supports_chorus_plant");

    //SupportsChorusFlower 紫颂花落脚面 对应原版 supports_chorus_flower
    public static readonly TagKey<NetCraft.Registry.Block> SupportsChorusFlower = Create("supports_chorus_flower");

    //SupportsNetherSprouts 下界苗落脚面 对应原版 supports_nether_sprouts
    public static readonly TagKey<NetCraft.Registry.Block> SupportsNetherSprouts = Create("supports_nether_sprouts");

    //SupportsAzalea 杜鹃落脚面 对应原版 supports_azalea
    public static readonly TagKey<NetCraft.Registry.Block> SupportsAzalea = Create("supports_azalea");

    //SupportsWarpedFungus 诡异菌落脚面 对应原版 supports_warped_fungus
    public static readonly TagKey<NetCraft.Registry.Block> SupportsWarpedFungus = Create("supports_warped_fungus");

    //SupportsCrimsonFungus 绯红菌落脚面 对应原版 supports_crimson_fungus
    public static readonly TagKey<NetCraft.Registry.Block> SupportsCrimsonFungus = Create("supports_crimson_fungus");

    //SupportsMangrovePropagule 红树胎生苗落脚面 对应原版 supports_mangrove_propagule
    public static readonly TagKey<NetCraft.Registry.Block> SupportsMangrovePropagule = Create("supports_mangrove_propagule");

    //SupportsNetherWart 下界疣落脚面 对应原版 supports_nether_wart
    public static readonly TagKey<NetCraft.Registry.Block> SupportsNetherWart = Create("supports_nether_wart");

    //SupportsCrimsonRoots 绯红菌索落脚面 对应原版 supports_crimson_roots
    public static readonly TagKey<NetCraft.Registry.Block> SupportsCrimsonRoots = Create("supports_crimson_roots");

    //SupportsWarpedRoots 诡异菌索落脚面 对应原版 supports_warped_roots
    public static readonly TagKey<NetCraft.Registry.Block> SupportsWarpedRoots = Create("supports_warped_roots");

    //SupportsWitherRose 凋灵玫瑰落脚面 对应原版 supports_wither_rose
    public static readonly TagKey<NetCraft.Registry.Block> SupportsWitherRose = Create("supports_wither_rose");

    //SupportsCocoa 可可豆贴着的那面 对应原版 supports_cocoa
    public static readonly TagKey<NetCraft.Registry.Block> SupportsCocoa = Create("supports_cocoa");

    //SupportsLilyPad 睡莲浮在什么上 对应原版 supports_lily_pad
    public static readonly TagKey<NetCraft.Registry.Block> SupportsLilyPad = Create("supports_lily_pad");

    //SupportsFrogspawn 青蛙卵落脚面 对应原版 supports_frogspawn
    public static readonly TagKey<NetCraft.Registry.Block> SupportsFrogspawn = Create("supports_frogspawn");

    //CannotSupportSeagrass 托不住海草的方块 实际只有岩浆块 对应原版 cannot_support_seagrass
    public static readonly TagKey<NetCraft.Registry.Block> CannotSupportSeagrass = Create("cannot_support_seagrass");

    //CannotSupportKelp 托不住海带的方块 对应原版 cannot_support_kelp
    public static readonly TagKey<NetCraft.Registry.Block> CannotSupportKelp = Create("cannot_support_kelp");

    //SupportOverrideCactusFlower 仙人掌花不按面坚固度判定而是直接认它 对应原版 support_override_cactus_flower
    public static readonly TagKey<NetCraft.Registry.Block> SupportOverrideCactusFlower = Create("support_override_cactus_flower");

    //Walls 各类围墙 栅栏门靠它判断要不要落成夹墙态 对应原版 walls
    public static readonly TagKey<NetCraft.Registry.Block> Walls = Create("walls");

    //Create 按原版方块标签的路径拼出键
    private static TagKey<NetCraft.Registry.Block> Create(string path)
        => TagKey<NetCraft.Registry.Block>.Create(Registries.BLOCK,
            Identifier.WithDefaultNamespace(path));
}
