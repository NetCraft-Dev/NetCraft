using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.EntityAttribute;
//属性常量类与 Entity 上的 Attributes 实例属性同名 引用常量要走别名
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;

namespace NetCraft.Game.World.Entity;

//Inventory 玩家物品栏 PoC 数据层对应原版 net.minecraft.world.entity.player.Inventory
//PoC 用 object identity 标识物品（如字符串 "cube"）不接真 ItemStack/Item 注册表
//生产环境由 ClientboundContainerSetContentPacket 等网络包填充真 ItemStack
//Hotbar 占用 0-8 槽位主物品栏 9-35 槽位共 36 格对标原版
public sealed class Inventory
{
    //HotbarSlots hotbar 槽位数固定 9 对标原版
    public const int HotbarSlots = 9;
    //TotalSlots 总槽位数 36 = 9 hotbar + 27 主物品栏 对标原版
    public const int TotalSlots = 36;

    //_items 槽位物品 identity null 表示空槽
    private readonly object?[] _items = new object?[TotalSlots];

    //GetItem 取指定槽位物品 identity null 表示空槽
    public object? GetItem(int slot) => (uint)slot < TotalSlots ? _items[slot] : null;

    //SetItem 设置指定槽位物品 identity null 清空槽位
    public void SetItem(int slot, object? identity)
    {
        if ((uint)slot < TotalSlots) _items[slot] = identity;
    }

    //GetHotbarItem 取 hotbar 槽位物品 identity（0-8）便捷方法供 GameScreen Hotbar 渲染用
    public object? GetHotbarItem(int hotbarSlot) => GetItem(hotbarSlot);
}

//Player 玩家实体对应原版 net.minecraft.world.entity.player.Player
//继承 Entity 持有经验/生命值/饥饿值核心字段
//Inventory/Abilities 等子系统待后续接入此处仅基础字段
public class Player : NetCraft.Registry.Entity, IEquipmentHolder, IEffectHolder
{
    //Equipment 玩家装备槽 供实体谓词与后续装备同步使用
    public EntityEquipment Equipment { get; } = new();

    //Effects 活跃药水效果 供实体谓词与后续效果同步使用
    public EntityEffects Effects { get; } = new();

    //GetItemBySlot 取指定槽位物品 对应原版 LivingEntity.getItemBySlot
    public ItemStack GetItemBySlot(EquipmentSlot slot) => Equipment.Get(slot);

    //Id 玩家实体类型注册名固定 minecraft:player
    public override Identifier Id => Identifier.WithDefaultNamespace("player");

    //Type 玩家实体类型 追踪器据此识别玩家
    public override EntityType<object>? Type => EntityTypes.PLAYER;

    //玩家是活体 阻挡方块放置 对应原版 LivingEntity 构造里打开 blocksBuilding
    public override bool BlocksBuilding => true;

    //玩家是活体 受坠落伤害 对应原版 LivingEntity 覆盖 Entity 的基类行为
    public override bool TakesFallDamage => true;

    //玩家的重力/击退抗性/安全坠落距离/坠落伤害系数/跨步高度一律走属性
    public override double DefaultGravity => GetAttributeValue(EntityAttributes.Gravity);
    public override double KnockbackResistance => GetAttributeValue(EntityAttributes.KnockbackResistance);
    public override double SafeFallDistance => GetAttributeValue(EntityAttributes.SafeFallDistance);
    public override double FallDamageMultiplier => GetAttributeValue(EntityAttributes.FallDamageMultiplier);
    public override double MaxUpStep => GetAttributeValue(EntityAttributes.StepHeight);

    //XpLevel 玩家经验等级默认 0
    public int XpLevel { get; set; }

    //XpP 当前经验进度 0~1
    public float XpP { get; set; }

    //XpTotal 累计经验总点数
    public int XpTotal { get; set; }

    //Health 当前生命值默认 20
    public float Health { get; set; } = 20f;

    //MaxHealth 最大生命值默认 20
    public float MaxHealth { get; set; } = 20f;

    //FoodLevel 饥饿值默认 20
    public int FoodLevel { get; set; } = 20;

    //AbsorptionHealth 吸收生命值（金苹果等）默认 0 用于吸收心渲染
    public float AbsorptionHealth { get; set; }

    //ActiveEffects 当前活跃药水效果集合用于 forPlayer 检测 POISON/WITHER/REGENERATION
    public HashSet<MobEffect> ActiveEffects { get; set; } = new();

    //IsFullyFrozen 玩家是否完全冻结（细雪等）用于 forPlayer 检测 FROZEN
    public bool IsFullyFrozen { get; set; }

    //IsHardcore 硬核模式用于 heart sprite 选取 hardcore 变体对标原版 level.getLevelData().isHardcore()
    public bool IsHardcore { get; set; }

    //GameMode 游戏模式 0=生存 1=创造 2=冒险 3=旁观
    public int GameMode { get; set; }

    //Inventory 玩家物品栏 PoC 数据层 hotbar 0-8 主物品栏 9-35
    //生产由网络包填充 PoC 预填 cube 用于 Hotbar 物品图标渲染演示
    public Inventory Inventory { get; } = new();

    public Player()
    {
        //玩家属性表按类型取 对应原版 Player.createAttributes
        Attributes = new AttributeMap(DefaultAttributes.GetSupplier(EntityTypes.PLAYER) ?? AttributeSupplier.Empty);
        //玩家默认站在原点稍上方
        Pos = new Vec3(0, 0, 0);
        //PoC 预填 hotbar 9 格 cube 物品演示 Hotbar 物品图标渲染
        //生产环境由 ClientboundContainerSetContentPacket 填充真 ItemStack
        for (var i = 0; i < Inventory.HotbarSlots; i++)
            Inventory.SetItem(i, "cube");
    }
}
