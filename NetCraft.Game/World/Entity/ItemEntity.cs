using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Entity;

//ItemEntity item entity, maps to vanilla net.minecraft.world.entity.item.ItemEntity
//Follows vanilla: gravity 0.04 / air drag 0.98 / ground horizontal friction multiplied by the block below / landing vertical speed bounces to -0.5
//Adjacent same-type item entities merge every 2 or 40 ticks / removed after 6000 ticks or when health hits zero / only same item and components merge
//Save field names and types align with vanilla Item/Age/PickupDelay/Health(short)/Owner/Thrower
//This project has no fluid system, the water and lava motion branches are not wired up
//The namespace segment shares a name with the entity base class, so the base class must be fully qualified
public sealed class ItemEntity : NetCraft.Registry.Entity
{
    //InfinitePickupDelay sentinel for never pickable, maps to vanilla 32767
    public const int InfinitePickupDelay = 32767;

    //InfiniteLifetime sentinel for unlimited lifetime, maps to vanilla -32768, reading it stops incrementing the age
    public const int InfiniteLifetime = -32768;

    //Lifetime ticks before an item entity naturally disappears, maps to vanilla 6000
    public const int Lifetime = 6000;

    //DefaultHealth default health, maps to vanilla 5, removed when it hits zero
    public const int DefaultHealth = 5;

    //DefaultPickupDelay default pickup cooldown ticks, maps to vanilla 10
    public const int DefaultPickupDelay = 10;

    //DataItemIndex entity data index of the item stack, maps to vanilla ItemEntity.DATA_ITEM
    //Entity occupies eight data slots 0-7, the item entity's is the 9th, both sides share this constant so sync stays consistent
    public const byte DataItemIndex = 8;

    //BounceFactor vertical speed bounce factor on landing, maps to vanilla -0.5
    private const double BounceFactor = -0.5;

    //MinHorizontalSpeedSqr horizontal speed below this squared value counts as at rest, maps to vanilla 1.0E-5
    private const double MinHorizontalSpeedSqr = 1e-5;

    //DefaultFriction fallback block friction when the level is not wired up, same as the vanilla block default
    private const double DefaultFriction = 0.6f;

    private readonly EntityType<object> _type;
    private ItemStack _item = ItemStack.Empty;

    //ItemEntity constructor, the entity type is bound at registration and determines the registry name and network index
    public ItemEntity(EntityType<object> type)
    {
        _type = type;
        //The bob phase and initial rotation are random, maps to bobOffs and setYRot in the vanilla constructor
        //Without randomness items spawned together bob and face in perfect sync, looking like a neat row of blocks
        BobOffset = Random.Shared.NextSingle() * MathF.PI * 2f;
        YRot = Random.Shared.NextSingle() * 360f;
        //Synched data declaration, the item stack is the only data synced for an item entity, maps to vanilla defineSynchedData
        SyncedData.Define(DataItemIndex, EntityDataSerializers.ItemStackId, ItemStack.Empty);
    }

    //ItemEntity constructor from position and item, maps to vanilla ItemEntity(Level, x, y, z, ItemStack)
    //Initial velocity is a random horizontal spread plus a fixed upward toss, so items spread out on landing instead of stacking in place
    public ItemEntity(EntityType<object> type, double x, double y, double z, ItemStack item) : this(type)
    {
        Pos = new Vec3(x, y, z);
        Item = item;
        Velocity = new Vec3(
            Random.Shared.NextDouble() * 0.2 - 0.1,
            0.2,
            Random.Shared.NextDouble() * 0.2 - 0.1);
    }

    //Id entity registry name taken from the bound type
    public override Identifier Id => _type.Id;

    //Type entity type, the tracker uses it to get the tracking range and network index
    public override EntityType<object>? Type => _type;

    //DefaultGravity item entity gravity 0.04, less than the default 0.08, maps to vanilla getDefaultGravity
    public override double DefaultGravity => 0.04;

    //SavesHealth the item entity's Health is a short with independent meaning, the base class does not write the float Health
    protected override bool SavesHealth => false;

    //Item the held item stack, an empty stack makes the entity pointless so it removes itself
    //Assigning syncs to observers, maps to vanilla setItem writing DATA_ITEM
    public ItemStack Item
    {
        get => _item;
        set
        {
            _item = value;
            SyncedData.Set(DataItemIndex, value);
        }
    }

    //Age ticks survived, disappears naturally at Lifetime
    public int Age { get; set; }

    //PickupDelay remaining ticks before pickup is allowed, 0 means pickable and 32767 means never
    public int PickupDelay { get; set; }

    //ItemHealth item entity health, maps to vanilla ItemEntity.health
    //Shares the name with the mob base class Entity.Health but has independent meaning, so it is a separate property
    public int ItemHealth { get; private set; } = DefaultHealth;

    //Owner only this player may pick it up, maps to vanilla target, null means anyone can pick it up
    public Guid? Owner { get; set; }

    //Thrower the thrower, maps to vanilla thrower
    public Guid? Thrower { get; set; }

    //BobOffset phase of the up-down bob, maps to vanilla bobOffs, decided by a random number at spawn
    public float BobOffset { get; }

    //Tick advances each tick, order aligns with vanilla ItemEntity.tick
    public override void Tick()
    {
        if (Item.IsEmpty())
        {
            Discard();
            return;
        }
        TickBase();
        if (PickupDelay > 0 && PickupDelay != InfinitePickupDelay) PickupDelay--;
        //This project has no fluid checks, the underwater and lava branches are skipped and everything follows gravity
        Velocity = new Vec3(Velocity.X, Velocity.Y - DefaultGravity, Velocity.Z);
        //A grounded resting item entity need not be recomputed every tick, vanilla staggers refreshes with (tickCount + id) % 4
        if (!OnGround || HorizontalSpeedSqr(Velocity) > MinHorizontalSpeedSqr || (TickCount + EntityId) % 4 == 0)
        {
            Move(Velocity);
            var airDrag = AirDrag;
            var friction = airDrag;
            if (OnGround) friction *= GroundFriction();
            Velocity = new Vec3(Velocity.X * friction, Velocity.Y * airDrag, Velocity.Z * friction);
            //Vertical speed is reversed and damped on landing, maps to vanilla -0.5
            if (OnGround && Velocity.Y < 0)
                Velocity = new Vec3(Velocity.X, Velocity.Y * BounceFactor, Velocity.Z);
        }
        //Merge checks are more frequent on block crossing, maps to vanilla rate = moved ? 2 : 40
        var moved = Mth.Floor(PreviousPos.X) != Mth.Floor(Pos.X)
            || Mth.Floor(PreviousPos.Y) != Mth.Floor(Pos.Y)
            || Mth.Floor(PreviousPos.Z) != Mth.Floor(Pos.Z);
        if (TickCount % (moved ? 2 : 40) == 0) MergeWithNeighbours();
        if (Age != InfiniteLifetime) Age++;
        if (Age >= Lifetime) Discard();
    }

    //PlayerTouch tries to pick up when a player touches the item entity, maps to vanilla ItemEntity.playerTouch
    //The stack handed over is the entity's own, whatever the core inserts in place is deducted here
    //Pickup only succeeds when the whole stack fits (or creative swallows the rest); a take item packet goes to the picker and the pickup sound plays
    //A partial insert is not settled this tick, the remainder stays on the entity until the next contact, same as the vanilla branch where add returns false
    public void PlayerTouch(ServerPlayer player)
    {
        if (HasPickUpDelay) return;
        if (Owner is { } owner && owner != player.Profile.Id) return;
        var stack = Item;
        var orgCount = stack.GetCount();
        if (!player.AddItem(stack)) return;
        player.Connection.Send(new ClientboundTakeItemEntityPacket(EntityId, player.EntityId, orgCount));
        player.PlayPickupSound();
        Discard();
    }

    //Hurt damage to an item entity directly reduces its own health without the mob invulnerability frames and death flow, maps to vanilla hurtServer
    //Item entities are not knocked back, the source parameter is accepted but unused
    public override bool Hurt(float amount, Vec3? knockbackSource = null)
    {
        ItemHealth -= (int)amount;
        if (ItemHealth > 0) return true;
        Discard();
        return true;
    }

    //SetDefaultPickUpDelay pickup cooldown on landing, 10 ticks, maps to vanilla setDefaultPickUpDelay
    public void SetDefaultPickUpDelay() => PickupDelay = DefaultPickupDelay;

    //SetNoPickUpDelay immediately pickable, maps to vanilla setNoPickUpDelay
    public void SetNoPickUpDelay() => PickupDelay = 0;

    //SetNeverPickUp never pickable, maps to vanilla setNeverPickUp
    public void SetNeverPickUp() => PickupDelay = InfinitePickupDelay;

    //SetPickUpDelay sets the pickup cooldown ticks, maps to vanilla setPickUpDelay
    public void SetPickUpDelay(int ticks) => PickupDelay = ticks;

    //HasPickUpDelay whether it is still on pickup cooldown, maps to vanilla hasPickUpDelay
    public bool HasPickUpDelay => PickupDelay > 0;

    //SetUnlimitedLifetime unlimited lifetime, maps to vanilla setUnlimitedLifetime
    public void SetUnlimitedLifetime() => Age = InfiniteLifetime;

    //SetExtendedLifetime extends by another 6000 ticks, maps to vanilla setExtendedLifetime
    public void SetExtendedLifetime() => Age = -Lifetime;

    //MakeFakeItem turns it into a cosmetic item entity that cannot be picked up and despawns at once, maps to vanilla makeFakeItem
    public void MakeFakeItem()
    {
        SetNeverPickUp();
        Age = Lifetime - 1;
    }

    //MergeWithNeighbours merges with same-type item entities within half a block, maps to vanilla mergeWithNeighbours
    //The range query uses the level's spatial index, bucket results are imprecise so they are filtered again by bounding box intersection
    public void MergeWithNeighbours()
    {
        if (!IsMergable || Level is not PersistentServerLevel level) return;
        var area = BoundingBox.Inflate(0.5, 0, 0.5);
        foreach (var candidate in level.EntityLookup.GetInRange(area.Min, area.Max))
        {
            if (ReferenceEquals(candidate, this) || candidate is not ItemEntity other) continue;
            if (!area.Intersects(other.BoundingBox) || !other.IsMergable) continue;
            TryToMerge(other);
            //After merging this entity may already be removed, vanilla also returns here directly
            if (IsRemoved) return;
        }
    }

    //IsMergable whether this item entity can take part in merging, maps to vanilla isMergable
    //Never-pickable, unlimited-lifetime, expired and already full ones do not merge
    public bool IsMergable
        => !IsRemoved && PickupDelay != InfinitePickupDelay && Age != InfiniteLifetime
            && Age < Lifetime && !Item.IsEmpty() && Item.GetCount() < Item.GetMaxStackSize();

    //AreMergable whether two stacks can merge, maps to vanilla areMergable
    public static bool AreMergable(ItemStack first, ItemStack second)
    {
        if (first.IsEmpty() || second.IsEmpty()) return false;
        if (first.GetCount() + second.GetCount() > first.GetMaxStackSize()) return false;
        return first.IsSameItemAndComponentsAs(second);
    }

    //GetSpin item entity spin phase, maps to vanilla getSpin for client rendering
    public static float GetSpin(float ageInTicks, float bobOffset) => ageInTicks / 20f + bobOffset;

    //VisualRotationYInDegrees item entity facing angle, maps to vanilla getVisualRotationYInDegrees
    public float VisualRotationYInDegrees
        => 180f - GetSpin(Age + 0.5f, BobOffset) / (MathF.PI * 2f) * 360f;

    //AddAdditionalSaveData save field names and types align with vanilla, the base class already skips the float Health
    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        tag.PutShort("Health", (short)ItemHealth);
        tag.PutShort("Age", (short)Age);
        tag.PutShort("PickupDelay", (short)PickupDelay);
        //Both fields are written with UUIDUtil.CODEC in vanilla, that is an array of 4 ints
        if (Thrower is { } thrower) tag.PutIntArray("Thrower", UuidToIntArray(thrower));
        if (Owner is { } owner) tag.PutIntArray("Owner", UuidToIntArray(owner));
        if (!Item.IsEmpty()) ItemStack.WriteNbt(tag, "Item", Item);
    }

    //ReadAdditionalSaveData reads back the item and state, removes the entity on the spot when the item is unusable
    protected override void ReadAdditionalSaveData(CompoundTag tag)
    {
        ItemHealth = tag.GetShort("Health")?.Value ?? DefaultHealth;
        Age = tag.GetShort("Age")?.Value ?? 0;
        PickupDelay = tag.GetShort("PickupDelay")?.Value ?? 0;
        if (tag.GetIntArray("Thrower") is { } thrower) Thrower = IntArrayToUuid(thrower.Value);
        if (tag.GetIntArray("Owner") is { } owner) Owner = IntArrayToUuid(owner.Value);
        Item = ItemStack.ReadNbt(tag.GetCompound("Item"));
        //No usable item in the save is the same as summoning with empty NBT and removes it on the spot
        if (Item.IsEmpty()) Discard();
    }

    //TryToMerge merges the lesser stack into the greater, maps to vanilla tryToMerge
    private void TryToMerge(ItemEntity other)
    {
        if (!AreMergable(Item, other.Item)) return;
        if (other.Item.GetCount() < Item.GetCount()) Merge(this, other);
        else Merge(other, this);
    }

    //Merge moves the count from into to and takes the larger pickup delay and smaller age of the two
    //Once the count is drained from is removed immediately, maps to the vanilla fromStack.isEmpty() branch
    private static void Merge(ItemEntity to, ItemEntity from)
    {
        var toStack = to.Item;
        var fromStack = from.Item;
        var moved = Math.Min(toStack.GetMaxStackSize() - toStack.GetCount(), fromStack.GetCount());
        if (moved <= 0) return;
        to.Item = toStack.CopyWithCount(toStack.GetCount() + moved);
        fromStack.Shrink(moved);
        to.PickupDelay = Math.Max(to.PickupDelay, from.PickupDelay);
        to.Age = Math.Min(to.Age, from.Age);
        if (fromStack.IsEmpty()) from.Discard();
    }

    //GroundFriction friction of the block below, maps to vanilla getBlockPosBelowThatAffectsMyMovement
    //Falls back to the default friction when the level is not wired up or there is no block at the position
    private double GroundFriction()
    {
        if (Level is not PersistentServerLevel level) return DefaultFriction;
        var below = new BlockPos(Mth.Floor(Pos.X), Mth.Floor(Pos.Y - 1e-7), Mth.Floor(Pos.Z));
        return level.GetBlockState(below)?.Owner.Friction ?? (float)DefaultFriction;
    }

    //HorizontalSpeedSqr squared horizontal speed
    private static double HorizontalSpeedSqr(Vec3 velocity) => velocity.X * velocity.X + velocity.Z * velocity.Z;
}
