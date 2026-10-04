using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//ThrowableProjectile 投掷物基类 对应原版 net.minecraft.world.entity.projectile.ThrowableProjectile
//雪球 鸡蛋 末影珍珠共用的重力与阻力 重力 0.03 空气阻力 0.99
public abstract class ThrowableProjectile : Projectile
{
    protected ThrowableProjectile(EntityType<object> type) : base(type) { }

    //DefaultGravity 投掷物重力 0.03 对应原版 getDefaultGravity
    public override double DefaultGravity => 0.03;

    //AirDrag 投掷物空气阻力 0.99 对应原版 getAirDrag
    public override double AirDrag => 0.99;

    //Tick 每刻推进 顺序对齐原版 ThrowableProjectile.tick
    //命中判定走移动线段射线 方块与实体都取最近 命中就停在命中点
    public override void Tick()
    {
        TickBase();
        CheckLeftOwner();
        ApplyGravityAndInertia();
        var hit = ProjectileUtil.GetHitResult(this);
        if (hit.Type != ProjectileHitType.miss)
        {
            Pos = hit.Location;
            UpdateRotation();
            OnHit(hit);
            return;
        }
        Move(Velocity);
        UpdateRotation();
    }
}

//ThrowableItemProjectile 携带物品栈的投掷物 对应原版 net.minecraft.world.entity.projectile.ThrowableItemProjectile
//客户端靠同步的物品栈决定贴图 服务端靠它决定命中后的表现
public abstract class ThrowableItemProjectile : ThrowableProjectile
{
    //DataItemIndex 物品栈的实体数据索引 对应原版 DATA_ITEM_STACK
    //Entity 自身占 0-7 八个数据 投掷物的是第 9 个 与掉落物同一个位置
    public const byte DataItemIndex = 8;

    private ItemStack _item = ItemStack.Empty;

    protected ThrowableItemProjectile(EntityType<object> type) : base(type)
        => SyncedData.Define(DataItemIndex, EntityDataSerializers.ItemStackId, ItemStack.Empty);

    //Item 投掷物携带的物品栈 只保留一个用于显示
    public ItemStack Item
    {
        get => _item;
        set
        {
            _item = value.IsEmpty() ? ItemStack.Empty : value.CopyWithCount(1);
            SyncedData.Set(DataItemIndex, _item);
        }
    }

    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        base.AddAdditionalSaveData(tag);
        if (!_item.IsEmpty()) ItemStack.WriteNbt(tag, "Item", _item);
    }

    protected override void ReadAdditionalSaveData(CompoundTag tag)
    {
        base.ReadAdditionalSaveData(tag);
        Item = ItemStack.ReadNbt(tag.GetCompound("Item"));
    }
}
