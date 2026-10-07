using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Game.World.Entity;

//ThrowableProjectile throwable projectile base class, maps to vanilla net.minecraft.world.entity.projectile.ThrowableProjectile
//Gravity and drag shared by snowballs, eggs and ender pearls: gravity 0.03, air drag 0.99
public abstract class ThrowableProjectile : Projectile
{
    protected ThrowableProjectile(EntityType<object> type) : base(type) { }

    //DefaultGravity throwable gravity 0.03, maps to vanilla getDefaultGravity
    public override double DefaultGravity => 0.03;

    //AirDrag throwable air drag 0.99, maps to vanilla getAirDrag
    public override double AirDrag => 0.99;

    //Tick advances each tick, order aligns with vanilla ThrowableProjectile.tick
    //Hit detection uses a ray along the movement segment, the nearest of block and entity wins, on a hit it stops at the hit point
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

//ThrowableItemProjectile throwable projectile carrying an item stack, maps to vanilla net.minecraft.world.entity.projectile.ThrowableItemProjectile
//The client uses the synced item stack to pick the texture and the server uses it to decide the post-hit behavior
public abstract class ThrowableItemProjectile : ThrowableProjectile
{
    //DataItemIndex entity data index of the item stack, maps to vanilla DATA_ITEM_STACK
    //Entity occupies eight data slots 0-7, the throwable's is the 9th, the same index as the item entity
    public const byte DataItemIndex = 8;

    private ItemStack _item = ItemStack.Empty;

    protected ThrowableItemProjectile(EntityType<object> type) : base(type)
        => SyncedData.Define(DataItemIndex, EntityDataSerializers.ItemStackId, ItemStack.Empty);

    //Item the item stack carried by the throwable, only one is kept for display
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
