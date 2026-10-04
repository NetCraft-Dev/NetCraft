using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Nbt;

namespace NetCraft.Storage;

//LevelData 世界元数据对应原版 PrimaryLevelData 持久化为世界根目录 level.dat
//NBT 字段名与层级对齐原版 26.2 供将来直接读写原版存档
//种子不在本文件 由 WorldGenSettingsData 独立存 data/minecraft/world_gen_settings.dat
public sealed class LevelData
{
    //AnvilVersionId 存档格式版本号对应原版 WorldData.ANVIL_VERSION_ID
    public const int AnvilVersionId = 19133;

    //OverworldDimension 主世界维度名固定写入 spawn.dimension
    private const string OverworldDimension = "minecraft:overworld";

    public string LevelName { get; set; } = "world";
    //GameTypeId 游戏模式 id 对应原版 GameType.getId 生存0 创造1 冒险2 旁观3
    public int GameTypeId { get; set; }
    //DifficultyName 难度 key 对应原版 Difficulty key peaceful/easy/normal/hard
    public string DifficultyName { get; set; } = "normal";
    public bool Hardcore { get; set; }
    public bool DifficultyLocked { get; set; }
    public bool AllowCommands { get; set; } = true;
    public bool Initialized { get; set; } = true;
    //GameTime 世界游戏刻对应原版 Time 字段 昼夜时间由 world_clocks.dat 管理
    public long GameTime { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; } = 64;
    public int SpawnZ { get; set; }
    public float SpawnYaw { get; set; }
    public float SpawnPitch { get; set; }

    //CreateTag 生成 Data 层内容字段与层级对齐原版 PrimaryLevelData.setTagData
    public CompoundTag CreateTag()
    {
        var tag = new CompoundTag();
        //ServerBrands 服务端品牌列表原版 dedicated 写 vanilla
        tag.Put("ServerBrands", new ListTag { new StringTag("vanilla") });
        tag.PutBoolean("WasModded", false);
        //Version 游戏版本块原版 writeVersionTag
        var version = new CompoundTag();
        version.PutString("Name", SharedConstants.Version);
        version.PutInt("Id", SharedConstants.WorldDataVersion);
        version.PutBoolean("Snapshot", false);
        version.PutString("Series", "main");
        tag.Put("Version", version);
        tag.PutInt("DataVersion", SharedConstants.WorldDataVersion);
        tag.PutInt("GameType", GameTypeId);
        //spawn 重生点结构对齐原版 RespawnData codec pos 为 IntArray
        var spawn = new CompoundTag();
        spawn.PutString("dimension", OverworldDimension);
        spawn.PutIntArray("pos", new[] { SpawnX, SpawnY, SpawnZ });
        spawn.PutFloat("yaw", SpawnYaw);
        spawn.PutFloat("pitch", SpawnPitch);
        tag.Put("spawn", spawn);
        tag.PutLong("Time", GameTime);
        tag.PutLong("LastPlayed", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        tag.PutString("LevelName", LevelName);
        //dimensions 维度表写空 map 读取方据此回退默认维度
        //本作维度由数据包 world_preset 定义 存档里再存一份反而会与数据包不一致
        tag.Put("dimensions", new CompoundTag());
        tag.PutInt("version", AnvilVersionId);
        tag.PutBoolean("allowCommands", AllowCommands);
        tag.PutBoolean("initialized", Initialized);
        //difficulty_settings 对齐原版 LevelSettings.DifficultySettings codec
        var difficulty = new CompoundTag();
        difficulty.PutString("difficulty", DifficultyName);
        difficulty.PutBoolean("hardcore", Hardcore);
        difficulty.PutBoolean("locked", DifficultyLocked);
        tag.Put("difficulty_settings", difficulty);
        return tag;
    }

    //Parse 从 Data 层内容恢复缺失字段取默认值
    public static LevelData Parse(CompoundTag data)
    {
        var result = new LevelData();
        var name = data.GetStringValue("LevelName");
        if (name.Length > 0) result.LevelName = name;
        result.GameTypeId = data.GetIntOr("GameType", 0);
        var difficulty = data.GetCompoundOrEmpty("difficulty_settings");
        var difficultyName = difficulty.GetStringValue("difficulty");
        if (difficultyName.Length > 0) result.DifficultyName = difficultyName;
        result.Hardcore = difficulty.GetBooleanOr("hardcore", false);
        result.DifficultyLocked = difficulty.GetBooleanOr("locked", false);
        result.AllowCommands = data.GetBooleanOr("allowCommands", true);
        result.Initialized = data.GetBooleanOr("initialized", true);
        result.GameTime = data.GetLongValue("Time");
        //spawn pos 为 IntArray[x,y,z] 结构异常时保留默认出生点
        var spawn = data.GetCompoundOrEmpty("spawn");
        var pos = spawn.GetIntArray("pos");
        if (pos is not null && pos.Value.Length == 3)
        {
            result.SpawnX = pos.Value[0];
            result.SpawnY = pos.Value[1];
            result.SpawnZ = pos.Value[2];
        }
        result.SpawnYaw = spawn.GetFloatValue("yaw");
        result.SpawnPitch = spawn.GetFloatValue("pitch");
        return result;
    }

    //Load 读世界目录 level.dat
    //文件不存在返回 null 由调用方走新世界流程
    //文件存在但损坏抛 InvalidDataException 不静默当新世界否则启动会把旧存档覆盖掉
    public static LevelData? Load(string worldDir)
    {
        var path = Path.Combine(worldDir, "level.dat");
        if (!File.Exists(path)) return null;
        try
        {
            var root = NbtIo.ReadCompressed(path, NbtAccounter.UnlimitedHeap());
            var data = root.GetCompound("Data");
            if (data is null) throw new InvalidDataException("缺少 Data 根层");
            return Parse(data);
        }
        catch (Exception e)
        {
            Log.Error($"level.dat read failed {path}: {e.Message}");
            throw new InvalidDataException($"level.dat 读取失败 {path}", e);
        }
    }

    //Save 写 level.dat 根为 Data 包装层临时文件原子替换防写一半损坏
    public void Save(string worldDir)
    {
        var path = Path.Combine(worldDir, "level.dat");
        var tmp = path + ".tmp";
        var root = new CompoundTag();
        root.Put("Data", CreateTag());
        NbtIo.WriteCompressed(root, tmp);
        File.Move(tmp, path, overwrite: true);
    }
}
