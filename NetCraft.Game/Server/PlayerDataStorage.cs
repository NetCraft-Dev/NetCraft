using NetCraft.Config;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Network.Component;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Server;

//PlayerDataStorage player data storage, maps to vanilla PlayerDataStorage
//Path <world dir>/playerdata/<uuid>.dat; the root is directly the player NBT with no Data wrapper, matching vanilla
//Currently persists position/facing/health/attributes/game type/xp/inventory; status effects are added once the matching system is wired up
public sealed class PlayerDataStorage
{
    //OverworldDimension the player's dimension name; the single-dimension phase fixes it to the overworld
    private const string OverworldDimension = "minecraft:overworld";

    private readonly string _directory;

    public PlayerDataStorage(string worldDir)
        => _directory = Path.Combine(worldDir, "playerdata");

    //Save writes the player save; a temp file atomic replace prevents a half write from corrupting it
    //A disk write failure only logs without throwing, to avoid one player's save problem halting the main loop
    public void Save(ServerPlayer player)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var path = PathFor(player);
            var tmp = path + ".tmp";
            NbtIo.WriteCompressed(CreateTag(player), tmp);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception e)
        {
            Log.Error($"Player save write failed {player.Profile.Name}: {e.Message}");
        }
    }

    //LoadInto reads the player save and applies it to the player object; returns whether a save was hit
    //No save is handled as a new player; a corrupt save is likewise handled as a new player without blocking entry
    public bool LoadInto(ServerPlayer player)
    {
        var path = PathFor(player);
        if (!File.Exists(path)) return false;
        try
        {
            Apply(NbtIo.ReadCompressed(path, NbtAccounter.UnlimitedHeap()), player);
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Player save read failed {path}: {e.Message}, treating as a new player");
            return false;
        }
    }

    //PathFor the player save file path; uuid uses the standard hyphenated form aligned with the vanilla file name
    private string PathFor(ServerPlayer player)
        => Path.Combine(_directory, player.Profile.Id.ToString("D") + ".dat");

    //CreateTag builds the player NBT with field names aligned with vanilla Entity/Player save
    //Exposed for /data get entity to read player entity data, sharing the same serialization as the save
    public static CompoundTag CreateTag(ServerPlayer player)
    {
        var tag = new CompoundTag();
        tag.PutInt("DataVersion", SharedConstants.WorldDataVersion);
        //Pos is a double list, Rotation a float list, aligned with vanilla Entity save
        tag.Put("Pos", new ListTag
        {
            new DoubleTag(player.Position.X),
            new DoubleTag(player.Position.Y),
            new DoubleTag(player.Position.Z),
        });
        tag.Put("Rotation", new ListTag
        {
            new FloatTag(player.Yaw),
            new FloatTag(player.Pitch),
        });
        tag.PutFloat("Health", player.Health);
        //Attributes share the same field name and structure as the entity save; only changed attributes are written
        player.Attributes.WriteTo(tag);
        //The ability state must carry flying, so creative-toggled flight is still there on rejoin, maps to the abilities of vanilla Player.addAdditionalSaveData
        player.Abilities.WriteTo(tag);
        tag.PutInt("playerGameType", player.GameType.Id);
        tag.PutString("Dimension", OverworldDimension);
        tag.PutInt("SelectedItemSlot", player.Inventory.SelectedSlot);
        tag.PutInt("XpLevel", player.XpLevel);
        tag.PutFloat("XpP", player.XpProgress);
        tag.PutInt("XpTotal", player.XpTotal);
        tag.PutIntArray("UUID", UuidToIntArray(player.Profile.Id));
        tag.Put("Inventory", InventoryToTag(player.Inventory));
        return tag;
    }

    //Apply restores state from player NBT; missing fields keep default values
    public static void Apply(CompoundTag tag, ServerPlayer player)
    {
        var pos = tag.GetList("Pos");
        if (pos is { Count: 3 })
            player.Position = new Vec3(
                pos.GetDouble(0)!.Value, pos.GetDouble(1)!.Value, pos.GetDouble(2)!.Value);
        var rotation = tag.GetList("Rotation");
        if (rotation is { Count: 2 })
        {
            player.Yaw = rotation.GetFloat(0)!.Value;
            player.Pitch = rotation.GetFloat(1)!.Value;
        }
        //Read attributes back first; the max health must be computed from the restored attributes
        player.Attributes.ReadFrom(tag);
        player.Health = tag.Contains("Health") ? tag.GetFloatValue("Health") : player.MaxHealth;
        //Read abilities back first, then recompute once by the saved game type
        //Order matches vanilla: Player reads abilities first, ServerPlayer sets gameMode after
        //The creative derivation branch does not touch flying, so the player's toggled flight state is kept
        player.Abilities.ReadFrom(tag);
        var savedGameType = tag.Contains("playerGameType")
            ? GameType.ById(tag.GetIntValue("playerGameType"))
            : null;
        player.GameType = savedGameType ?? player.GameType;
        player.Inventory.SelectedSlot = tag.GetIntValue("SelectedItemSlot");
        player.XpLevel = tag.GetIntValue("XpLevel");
        player.XpProgress = tag.GetFloatValue("XpP");
        player.XpTotal = tag.GetIntValue("XpTotal");
        ApplyInventory(tag.GetList("Inventory"), player.Inventory);
    }

    //InventoryToTag converts the player inventory to the vanilla 1.20.5+ structure id + count + Slot
    //Components are all empty for now; add the components layer here once component-bearing items are wired up
    private static ListTag InventoryToTag(PlayerInventory inventory)
    {
        var list = new ListTag();
        for (var slot = 0; slot < inventory.Size; slot++)
        {
            var stack = inventory.GetItem(slot);
            if (stack.IsEmpty()) continue;
            var entry = new CompoundTag();
            entry.PutByte("Slot", (byte)slot);
            entry.PutString("id", stack.GetItem().Id.ToString());
            entry.PutInt("count", stack.GetCount());
            list.Add(entry);
        }
        return list;
    }

    //ApplyInventory restores the inventory; unregistered item names and invalid counts are skipped
    private static void ApplyInventory(ListTag? list, PlayerInventory inventory)
    {
        if (list is null) return;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list.GetCompound(i);
            if (entry is null) continue;
            var idText = entry.GetStringValue("id");
            var count = entry.GetIntValue("count");
            var slot = entry.GetByteValue("Slot");
            if (idText.Length == 0 || count <= 0) continue;
            //The item name carries a namespace; without a colon TryParse adds the default namespace, aligned with vanilla Identifier parsing
            var itemId = Identifier.TryParse(idText);
            if (itemId is null) continue;
            var holder = BuiltInRegistries.ITEM.Get(itemId.Value);
            if (holder is null)
            {
                Log.Warning($"Player save item is not registered, skipped {idText}");
                continue;
            }
            inventory.SetItem(slot, new ItemStack(holder, count, DataComponentPatch.Empty));
        }
    }

    //UuidToIntArray splits the UUID into 4 ints, aligned with the IntArray structure of vanilla UUIDUtil
    //The byte order uses .NET Guid's memory layout, different from the Java version; local reads/writes are self-consistent, but reading a vanilla save needs conversion
    private static int[] UuidToIntArray(Guid uuid)
    {
        var bytes = uuid.ToByteArray();
        return new[]
        {
            BitConverter.ToInt32(bytes, 0),
            BitConverter.ToInt32(bytes, 4),
            BitConverter.ToInt32(bytes, 8),
            BitConverter.ToInt32(bytes, 12),
        };
    }
}
