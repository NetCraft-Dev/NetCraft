using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//DimensionTypes 内置维度类型 对应原版 net.minecraft.world.level.dimension.BuiltinDimensionTypes
//数据驱动从 data/minecraft/dimension_type/*.json 装载是首选 这里是无数据包时的兜底
//数值一律照原版三个 json 抄 改动前先核对 jar 里的文件
public static class DimensionTypes
{
    //Overworld 主世界 有天光无天花板 高度 -64..320 坐标不缩放
    public static readonly DimensionType Overworld = Create(
        "overworld", hasFixedTime: false, hasSkyLight: true, hasCeiling: false, hasEnderDragonFight: false,
        coordinateScale: 1.0, minY: -64, height: 384, logicalHeight: 384,
        infiniburn: "infiniburn_overworld", ambientLight: 0f,
        spawnLightMin: 0, spawnLightMax: 7, blockLightLimit: 0, skybox: null, cardinalLight: null);

    //Nether 下界 有天花板无天光 高度 0..256 坐标 1:8 放大
    public static readonly DimensionType Nether = Create(
        "the_nether", hasFixedTime: true, hasSkyLight: false, hasCeiling: true, hasEnderDragonFight: false,
        coordinateScale: 8.0, minY: 0, height: 256, logicalHeight: 128,
        infiniburn: "infiniburn_nether", ambientLight: 0.1f,
        spawnLightMin: 7, spawnLightMax: 7, blockLightLimit: 15, skybox: "none", cardinalLight: "nether");

    //End 末地 有天光无天花板 高度 0..256 逻辑高度等同总高度
    public static readonly DimensionType End = Create(
        "the_end", hasFixedTime: true, hasSkyLight: true, hasCeiling: false, hasEnderDragonFight: true,
        coordinateScale: 1.0, minY: 0, height: 256, logicalHeight: 256,
        infiniburn: "infiniburn_end", ambientLight: 0.25f,
        spawnLightMin: 15, spawnLightMax: 15, blockLightLimit: 0, skybox: "end", cardinalLight: null);

    //All 三个内置维度类型
    public static readonly DimensionType[] All = { Overworld, Nether, End };

    //RegisterBuiltin 把内置维度类型登记进注册表 已有同名键时跳过让数据驱动的真值优先
    public static void RegisterBuiltin()
    {
        var registry = (WritableRegistry<NetCraft.Registry.DimensionType>)BuiltInRegistries.DIMENSION_TYPE;
        foreach (var type in All)
        {
            if (registry.ContainsKey(type.Id)) continue;
            registry.Register(ResourceKey<NetCraft.Registry.DimensionType>.Create(registry.Key, type.Id), type,
                RegistrationInfo.BuiltIn);
        }
    }

    private static DimensionType Create(string path, bool hasFixedTime, bool hasSkyLight, bool hasCeiling,
        bool hasEnderDragonFight, double coordinateScale, int minY, int height, int logicalHeight,
        string infiniburn, float ambientLight, int spawnLightMin, int spawnLightMax, int blockLightLimit,
        string? skybox, string? cardinalLight)
    {
        var type = new DimensionType(
            hasFixedTime,
            hasSkyLight,
            hasCeiling,
            hasEnderDragonFight,
            coordinateScale,
            minY,
            height,
            logicalHeight,
            Identifier.WithDefaultNamespace(infiniburn),
            ambientLight,
            new MonsterSettings(
                spawnLightMin == spawnLightMax
                    ? ConstantInt.Of(spawnLightMin)
                    : UniformInt.Of(spawnLightMin, spawnLightMax),
                blockLightLimit),
            skybox is null ? null : Identifier.WithDefaultNamespace(skybox),
            cardinalLight is null ? null : Identifier.WithDefaultNamespace(cardinalLight));
        type.SetRegistryId(Identifier.WithDefaultNamespace(path));
        return type;
    }
}
