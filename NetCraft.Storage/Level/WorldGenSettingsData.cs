using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//WorldGenSettingsData, world gen settings, maps to vanilla WorldGenSettings SavedData
//Stored in data/minecraft/world_gen_settings.dat; the seed is fixed in the save, dimensions are a temporary empty placeholder for the codec
public sealed class WorldGenSettingsData : SavedData
{
    //TypeId, the save identifier, maps to the minecraft:world_gen_settings of the vanilla SavedDataType
    private const string TypeId = "minecraft:world_gen_settings";

    //Type, the SavedData factory; load restores the options field, dimensions is not parsed yet
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

    //Save writes options and dimensions to align with the vanilla WorldGenSettings codec; with empty dimensions vanilla falls back to the default dimension
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

    //ReadSeed reads world_gen_settings.dat from the world directory directly to get the seed, for the startup flow before the server is constructed
    //Returns null when the file is missing or corrupt; the caller falls back to server.properties
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
