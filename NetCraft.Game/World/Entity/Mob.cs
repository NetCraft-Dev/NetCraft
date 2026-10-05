using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.EntityAttribute;
//属性常量类与 Entity 上的 Attributes 实例属性同名 引用常量要走别名
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;

namespace NetCraft.Game.World.Entity;

//Mob 生物实体对应原版 net.minecraft.world.entity.Mob
//继承 Entity 持有实体类型引用与 AI 标志位
//具体生物类(僵尸/猪等)待 AI/寻路接入后按需新增 当前由类型工厂直接建 Mob 承载
public class Mob : NetCraft.Registry.Entity, IEquipmentHolder, IEffectHolder
{
    private readonly EntityType<object> _type;

    //Equipment 生物装备槽 供实体谓词与后续装备同步使用
    public EntityEquipment Equipment { get; } = new();

    //Effects 活跃药水效果 供实体谓词与后续效果同步使用
    public EntityEffects Effects { get; } = new();

    //GetItemBySlot 取指定槽位物品 对应原版 LivingEntity.getItemBySlot
    public ItemStack GetItemBySlot(EquipmentSlot slot) => Equipment.Get(slot);

    //Mob 构造 实体类型由注册表注册时传入 决定注册名与网络序号
    //属性表按类型从 DefaultAttributes 取 物种差异全在那张表里 该类型没登记过就退回空表
    public Mob(EntityType<object> type)
    {
        _type = type;
        Attributes = new AttributeMap(DefaultAttributes.GetSupplier(type) ?? AttributeSupplier.Empty);
    }

    //活体的重力/击退抗性/安全坠落距离/坠落伤害系数/跨步高度一律走属性
    //对应原版 LivingEntity 覆盖 Entity 以及读属性的那几个点
    public override double DefaultGravity => GetAttributeValue(EntityAttributes.Gravity);
    public override double KnockbackResistance => GetAttributeValue(EntityAttributes.KnockbackResistance);
    public override double SafeFallDistance => GetAttributeValue(EntityAttributes.SafeFallDistance);
    public override double FallDamageMultiplier => GetAttributeValue(EntityAttributes.FallDamageMultiplier);
    public override double MaxUpStep => GetAttributeValue(EntityAttributes.StepHeight);

    //Id 实体注册名取自已绑定的类型
    public override Identifier Id => _type.Id;

    //Type 实体类型 追踪器按它取视距与网络序号
    public override EntityType<object>? Type => _type;

    //生物是活体 阻挡方块放置 对应原版 LivingEntity 构造里打开 blocksBuilding
    public override bool BlocksBuilding => true;

    //生物是活体 受坠落伤害 对应原版 LivingEntity 覆盖 Entity 的基类行为
    public override bool TakesFallDamage => true;

    //NoAi 是否禁用 AI 默认 false 对齐原版 NoAI NBT 标签
    public bool NoAi { get; set; }

    //TargetUuid 当前攻击目标实体 Uuid 占位待 AI 子系统就绪
    public Guid? TargetUuid { get; set; }

    //PersistenceRequired 是否强制保留不卸载默认 false
    public bool PersistenceRequired { get; set; }

    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        tag.PutBoolean("NoAI", NoAi);
        tag.PutBoolean("PersistenceRequired", PersistenceRequired);
    }

    protected override void ReadAdditionalSaveData(CompoundTag tag)
    {
        NoAi = tag.GetBooleanOr("NoAI", false);
        PersistenceRequired = tag.GetBooleanOr("PersistenceRequired", false);
    }
}
