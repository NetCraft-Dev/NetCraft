using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Nbt;

namespace NetCraft.Storage;

//LevelData, world metadata, maps to vanilla PrimaryLevelData, persisted as level.dat in the world root
//NBT field names and hierarchy align with vanilla 26.2, so vanilla saves can be read and written directly later
//The seed is not in this file; WorldGenSettingsData stores it separately in data/minecraft/world_gen_settings.dat
public sealed class LevelData
{
    //AnvilVersionId, save format version, maps to vanilla WorldData.ANVIL_VERSION_ID
    public const int AnvilVersionId = 19133;

    //OverworldDimension, overworld dimension name; written to spawn.dimension
    private const string OverworldDimension = "minecraft:overworld";

    public string LevelName { get; set; } = "world";
    //GameTypeId, game mode id, maps to vanilla GameType.getId: survival 0, creative 1, adventure 2, spectator 3
    public int GameTypeId { get; set; }
    //DifficultyName, difficulty key, maps to the vanilla Difficulty key: peaceful/easy/normal/hard
    public string DifficultyName { get; set; } = "normal";
    public bool Hardcore { get; set; }
    public bool DifficultyLocked { get; set; }
    public bool AllowCommands { get; set; } = true;
    public bool Initialized { get; set; } = true;
    //GameTime, world game time, maps to the vanilla Time field; day-night time is managed by world_clocks.dat
    public long GameTime { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; } = 64;
    public int SpawnZ { get; set; }
    public float SpawnYaw { get; set; }
    public float SpawnPitch { get; set; }

    //CreateTag builds the Data layer content; fields and hierarchy align with vanilla PrimaryLevelData.setTagData
    public CompoundTag CreateTag()
    {
        var tag = new CompoundTag();
        //ServerBrands, server brand list; vanilla dedicated writes vanilla
        tag.Put("ServerBrands", new ListTag { new StringTag("vanilla") });
        tag.PutBoolean("WasModded", false);
        //Version, game version block, vanilla writeVersionTag
        var version = new CompoundTag();
        version.PutString("Name", SharedConstants.Version);
        version.PutInt("Id", SharedConstants.WorldDataVersion);
        version.PutBoolean("Snapshot", false);
        version.PutString("Series", "main");
        tag.Put("Version", version);
        tag.PutInt("DataVersion", SharedConstants.WorldDataVersion);
        tag.PutInt("GameType", GameTypeId);
        //spawn, respawn point structure, aligns with the vanilla RespawnData codec; pos is an IntArray
        var spawn = new CompoundTag();
        spawn.PutString("dimension", OverworldDimension);
        spawn.PutIntArray("pos", new[] { SpawnX, SpawnY, SpawnZ });
        spawn.PutFloat("yaw", SpawnYaw);
        spawn.PutFloat("pitch", SpawnPitch);
        tag.Put("spawn", spawn);
        tag.PutLong("Time", GameTime);
        tag.PutLong("LastPlayed", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        tag.PutString("LevelName", LevelName);
        //dimensions, the dimension table writes an empty map; the reader falls back to the default dimension based on that
        //Dimensions here are defined by the world_preset data pack; storing another copy in the save would only disagree with the data pack
        tag.Put("dimensions", new CompoundTag());
        tag.PutInt("version", AnvilVersionId);
        tag.PutBoolean("allowCommands", AllowCommands);
        tag.PutBoolean("initialized", Initialized);
        //difficulty_settings aligns with the vanilla LevelSettings.DifficultySettings codec
        var difficulty = new CompoundTag();
        difficulty.PutString("difficulty", DifficultyName);
        difficulty.PutBoolean("hardcore", Hardcore);
        difficulty.PutBoolean("locked", DifficultyLocked);
        tag.Put("difficulty_settings", difficulty);
        return tag;
    }

    //Parse restores from the Data layer content; missing fields take defaults
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
        //spawn pos is IntArray[x,y,z]; a malformed structure keeps the default spawn point
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

    //Load reads level.dat from the world directory
    //Returns null when the file is missing; the caller starts a new world flow
    //Throws InvalidDataException when the file exists but is corrupt, rather than silently treating it as a new world and overwriting the old save on startup
    public static LevelData? Load(string worldDir)
    {
        var path = Path.Combine(worldDir, "level.dat");
        if (!File.Exists(path)) return null;
        try
        {
            var root = NbtIo.ReadCompressed(path, NbtAccounter.UnlimitedHeap());
            var data = root.GetCompound("Data");
            if (data is null) throw new InvalidDataException("Missing Data root layer");
            return Parse(data);
        }
        catch (Exception e)
        {
            Log.Error($"level.dat read failed {path}: {e.Message}");
            throw new InvalidDataException($"level.dat read failed {path}", e);
        }
    }

    //Save writes level.dat with a Data wrapper at the root, using a temp file and atomic replace to avoid a half-written file
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
