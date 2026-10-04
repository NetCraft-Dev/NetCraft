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

//PlayerDataStorage 玩家数据存档对应原版 PlayerDataStorage
//路径 <世界目录>/playerdata/<uuid>.dat 根直接是玩家 NBT 无 Data 包装层与原版一致
//当前持久化位置/朝向/血量/属性/游戏模式/经验/物品栏 状态效果待对应系统接入后补
public sealed class PlayerDataStorage
{
    //OverworldDimension 玩家所在维度名 单维度阶段固定主世界
    private const string OverworldDimension = "minecraft:overworld";

    private readonly string _directory;

    public PlayerDataStorage(string worldDir)
        => _directory = Path.Combine(worldDir, "playerdata");

    //Save 写玩家存档 临时文件原子替换防写一半损坏
    //写盘失败只记日志不抛出 避免单个玩家存档问题带停主循环
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

    //LoadInto 读玩家存档并应用到玩家对象 返回是否命中存档
    //无存档按新玩家处理 存档损坏同样按新玩家处理不阻断进入
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

    //PathFor 玩家存档文件路径 uuid 用带连字符的标准形式对齐原版文件名
    private string PathFor(ServerPlayer player)
        => Path.Combine(_directory, player.Profile.Id.ToString("D") + ".dat");

    //CreateTag 生成玩家 NBT 字段名对齐原版 Entity/Player save
    //公开供 /data get entity 取玩家实体数据 与写盘共用同一份序列化
    public static CompoundTag CreateTag(ServerPlayer player)
    {
        var tag = new CompoundTag();
        tag.PutInt("DataVersion", SharedConstants.WorldDataVersion);
        //Pos 是 double 列表 Rotation 是 float 列表 对齐原版 Entity save
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
        //属性与实体存档同一个字段名与结构 只写被改动过的属性
        player.Attributes.WriteTo(tag);
        //能力状态要带上 flying 创造模式按出来的飞行重进后还在 对应原版 Player.addAdditionalSaveData 的 abilities
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

    //Apply 从玩家 NBT 恢复状态 缺失字段保持默认值
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
        //属性先读回 血量上限要按恢复后的属性算
        player.Attributes.ReadFrom(tag);
        player.Health = tag.Contains("Health") ? tag.GetFloatValue("Health") : player.MaxHealth;
        //先读回能力 再按存档里的游戏模式重算一次
        //顺序对齐原版: Player 读 abilities 在前 ServerPlayer 设 gameMode 在后
        //创造的推导分支不会动 flying 所以玩家按出来的飞行状态能留住
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

    //InventoryToTag 玩家物品栏转原版 1.20.5+ 结构 id + count + Slot
    //组件当前全为空 接入带组件物品后在此补 components 层
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

    //ApplyInventory 恢复物品栏 未注册物品名与非法数量直接跳过
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
            //物品名带命名空间 无冒号时 TryParse 补默认命名空间对齐原版 Identifier 解析
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

    //UuidToIntArray UUID 拆 4 个 int 对齐原版 UUIDUtil 的 IntArray 结构
    //字节序用 .NET 的 Guid 内存布局 与 Java 版不同 本地读写自洽 读取原版存档时需换算
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
