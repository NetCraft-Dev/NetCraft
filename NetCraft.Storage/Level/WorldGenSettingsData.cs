using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//WorldGenSettingsData 世界生成设置对应原版 WorldGenSettings SavedData
//存 data/minecraft/world_gen_settings.dat 种子固化在存档维度 codec 暂存空占位
public sealed class WorldGenSettingsData : SavedData
{
    //TypeId 存档标识对应原版 SavedDataType 的 minecraft:world_gen_settings
    private const string TypeId = "minecraft:world_gen_settings";

    //Type SavedData 工厂读档恢复 options 字段 dimensions 暂不解析
    public static readonly SavedDataType<WorldGenSettingsData> Type = new WorldGenSettingsType();

    public long Seed { get; set; }
    public bool GenerateStructures { get; set; } = true;
    public bool GenerateBonusChest { get; set; }

    public override string Id => TypeId;

    private sealed class WorldGenSettingsType : SavedDataType<WorldGenSettingsData>
    {
        public string Id => TypeId;

        public WorldGenSettingsData Create(CompoundTag tag, RegistryAccess registryAccess)
        {
            var options = tag.GetCompoundOrEmpty("options");
            return new WorldGenSettingsData
            {
                Seed = options.GetLongOr("seed", 0L),
                GenerateStructures = options.GetBooleanOr("generate_structures", true),
                GenerateBonusChest = options.GetBooleanOr("bonus_chest", false),
            };
        }
    }

    //Save 写 options 与 dimensions 对齐原版 WorldGenSettings codec 空 dimensions 原版回退默认维度
    public override CompoundTag Save(CompoundTag tag)
    {
        var options = new CompoundTag();
        options.PutLong("seed", Seed);
        options.PutBoolean("generate_structures", GenerateStructures);
        options.PutBoolean("bonus_chest", GenerateBonusChest);
        tag.Put("options", options);
        tag.Put("dimensions", new CompoundTag());
        return tag;
    }

    //ReadSeed 直读世界目录的 world_gen_settings.dat 取种子供启动流程在构造服务器前使用
    //文件不存在或损坏返回 null 由调用方回退 server.properties
    public static long? ReadSeed(string worldDir)
    {
        var path = Path.Combine(worldDir, "data", "minecraft", "world_gen_settings.dat");
        if (!File.Exists(path)) return null;
        try
        {
            var root = NbtIo.ReadCompressed(path, NbtAccounter.UnlimitedHeap());
            return root.GetCompoundOrEmpty("data").GetCompoundOrEmpty("options").GetLongOr("seed", 0L);
        }
        catch (Exception e)
        {
            Log.Warning($"world_gen_settings read failed: {e.Message}");
            return null;
        }
    }
}
