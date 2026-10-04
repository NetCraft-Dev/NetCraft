using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Entity;

//ItemEntity 掉落物实体对应原版 net.minecraft.world.entity.item.ItemEntity
//按原版实现: 重力 0.04 / 空气阻力 0.98 / 落地水平摩擦乘脚下方块摩擦 / 落地竖直速度反弹 -0.5
//相邻同类掉落物按 2 或 40 刻合并 / 存活 6000 刻或血量归零后移除 / 同物品同组件才合并
//存档字段名与类型对齐原版 Item/Age/PickupDelay/Health(short)/Owner/Thrower
//本作没有流体系统 水中与岩浆运动分支未接入
//命名空间段与实体基类同名 基类必须写全限定名
public sealed class ItemEntity : NetCraft.Registry.Entity
{
    //InfinitePickupDelay 永不拾取的哨兵值 对应原版 32767
    public const int InfinitePickupDelay = 32767;

    //InfiniteLifetime 无限存活哨兵值 对应原版 -32768 读到它就不再递增存活刻数
    public const int InfiniteLifetime = -32768;

    //Lifetime 掉落物自然消失的刻数 对应原版 6000
    public const int Lifetime = 6000;

    //DefaultHealth 默认血量 对应原版 5 归零即移除
    public const int DefaultHealth = 5;

    //DefaultPickupDelay 默认拾取冷却刻数 对应原版 10
    public const int DefaultPickupDelay = 10;

    //DataItemIndex 物品栈的实体数据索引 对应原版 ItemEntity.DATA_ITEM
    //Entity 自身占 0-7 八个数据 掉落物的是第 9 个 双端共用这个常量保证同步口径一致
    public const byte DataItemIndex = 8;

    //BounceFactor 落地竖直速度反弹系数 对应原版 -0.5
    private const double BounceFactor = -0.5;

    //MinHorizontalSpeedSqr 水平速度低于该平方值视为静止 对应原版 1.0E-5
    private const double MinHorizontalSpeedSqr = 1e-5;

    //DefaultFriction 关卡未接入时的兜底方块摩擦 与原版方块默认值一致
    private const double DefaultFriction = 0.6f;

    private readonly EntityType<object> _type;
    private ItemStack _item = ItemStack.Empty;

    //ItemEntity 构造 实体类型由注册时绑定 决定注册名与网络序号
    public ItemEntity(EntityType<object> type)
    {
        _type = type;
        //浮动相位与初始朝向都取随机数 对应原版构造里的 bobOffs 与 setYRot
        //不随机的话同批生成的掉落物浮动与朝向完全同步 看着像一排整齐的方块
        BobOffset = Random.Shared.NextSingle() * MathF.PI * 2f;
        YRot = Random.Shared.NextSingle() * 360f;
        //元数据声明 物品栈是掉落物唯一同步的数据 对应原版 defineSynchedData
        SyncedData.Define(DataItemIndex, EntityDataSerializers.ItemStackId, ItemStack.Empty);
    }

    //ItemEntity 按坐标与物品构造 对应原版 ItemEntity(Level, x, y, z, ItemStack)
    //初速为随机水平散布加固定上抛 落地后自然铺开而不是原地堆成一摞
    public ItemEntity(EntityType<object> type, double x, double y, double z, ItemStack item) : this(type)
    {
        Pos = new Vec3(x, y, z);
        Item = item;
        Velocity = new Vec3(
            Random.Shared.NextDouble() * 0.2 - 0.1,
            0.2,
            Random.Shared.NextDouble() * 0.2 - 0.1);
    }

    //Id 实体注册名取自已绑定的类型
    public override Identifier Id => _type.Id;

    //Type 实体类型 追踪器按它取视距与网络序号
    public override EntityType<object>? Type => _type;

    //DefaultGravity 掉落物重力 0.04 小于默认的 0.08 对应原版 getDefaultGravity
    public override double DefaultGravity => 0.04;

    //SavesHealth 掉落物的 Health 是 short 且语义独立 基类不代写 float Health
    protected override bool SavesHealth => false;

    //Item 持有的物品栈 空栈实体没有存在意义会自行移除
    //赋值即同步给观察者 对应原版 setItem 写 DATA_ITEM
    public ItemStack Item
    {
        get => _item;
        set
        {
            _item = value;
            SyncedData.Set(DataItemIndex, value);
        }
    }

    //Age 已存活刻数 到 Lifetime 自然消失
    public int Age { get; set; }

    //PickupDelay 剩余不可拾取刻数 0 表示可拾取 32767 表示永不拾取
    public int PickupDelay { get; set; }

    //ItemHealth 掉落物血量 对应原版 ItemEntity.health
    //与生物基类的 Entity.Health 同名但语义独立 故单独一个属性
    public int ItemHealth { get; private set; } = DefaultHealth;

    //Owner 只允许该玩家拾取 对应原版 target 为 null 表示任何人可拾取
    public Guid? Owner { get; set; }

    //Thrower 投掷者 对应原版 thrower
    public Guid? Thrower { get; set; }

    //BobOffset 上下浮动的相位 对应原版 bobOffs 由生成时的随机数决定
    public float BobOffset { get; }

    //Tick 每刻推进 顺序对齐原版 ItemEntity.tick
    public override void Tick()
    {
        if (Item.IsEmpty())
        {
            Discard();
            return;
        }
        TickBase();
        if (PickupDelay > 0 && PickupDelay != InfinitePickupDelay) PickupDelay--;
        //本作没有流体判定 水下与岩浆分支不做 一律按重力处理
        Velocity = new Vec3(Velocity.X, Velocity.Y - DefaultGravity, Velocity.Z);
        //贴地的静止掉落物不必每刻重算 原版按 (tickCount + id) % 4 错开刷新
        if (!OnGround || HorizontalSpeedSqr(Velocity) > MinHorizontalSpeedSqr || (TickCount + EntityId) % 4 == 0)
        {
            Move(Velocity);
            var airDrag = AirDrag;
            var friction = airDrag;
            if (OnGround) friction *= GroundFriction();
            Velocity = new Vec3(Velocity.X * friction, Velocity.Y * airDrag, Velocity.Z * friction);
            //落地时竖直速度反向衰减 对应原版 -0.5
            if (OnGround && Velocity.Y < 0)
                Velocity = new Vec3(Velocity.X, Velocity.Y * BounceFactor, Velocity.Z);
        }
        //跨方块格时合并检查更频繁 对应原版 rate = moved ? 2 : 40
        var moved = Mth.Floor(PreviousPos.X) != Mth.Floor(Pos.X)
            || Mth.Floor(PreviousPos.Y) != Mth.Floor(Pos.Y)
            || Mth.Floor(PreviousPos.Z) != Mth.Floor(Pos.Z);
        if (TickCount % (moved ? 2 : 40) == 0) MergeWithNeighbours();
        if (Age != InfiniteLifetime) Age++;
        if (Age >= Lifetime) Discard();
    }

    //PlayerTouch 玩家碰到掉落物时尝试拾取 对应原版 ItemEntity.playerTouch
    //交给的栈就是实体自己的那一个 内核就地放进多少就从这里扣多少
    //只有整栈放得下(或创造模式吞掉剩余)才算拾取成功 发 take item 包给拾取者并播入手音效
    //部分放入时本刻不结算 剩余量留在实体上等下一次接触 与原版 add 返回 false 的分支一致
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

    //Hurt 掉落物受伤 直接扣自己的血量 不走生物的无敌帧与死亡流程 对应原版 hurtServer
    //掉落物不受击退 来源参数收下但不用
    public override bool Hurt(float amount, Vec3? knockbackSource = null)
    {
        ItemHealth -= (int)amount;
        if (ItemHealth > 0) return true;
        Discard();
        return true;
    }

    //SetDefaultPickUpDelay 落地拾取冷却 10 刻 对应原版 setDefaultPickUpDelay
    public void SetDefaultPickUpDelay() => PickupDelay = DefaultPickupDelay;

    //SetNoPickUpDelay 立即可拾取 对应原版 setNoPickUpDelay
    public void SetNoPickUpDelay() => PickupDelay = 0;

    //SetNeverPickUp 永不拾取 对应原版 setNeverPickUp
    public void SetNeverPickUp() => PickupDelay = InfinitePickupDelay;

    //SetPickUpDelay 指定拾取冷却刻数 对应原版 setPickUpDelay
    public void SetPickUpDelay(int ticks) => PickupDelay = ticks;

    //HasPickUpDelay 是否仍在拾取冷却 对应原版 hasPickUpDelay
    public bool HasPickUpDelay => PickupDelay > 0;

    //SetUnlimitedLifetime 无限存活 对应原版 setUnlimitedLifetime
    public void SetUnlimitedLifetime() => Age = InfiniteLifetime;

    //SetExtendedLifetime 再续 6000 刻 对应原版 setExtendedLifetime
    public void SetExtendedLifetime() => Age = -Lifetime;

    //MakeFakeItem 变成不可拾取且马上消失的表现用掉落物 对应原版 makeFakeItem
    public void MakeFakeItem()
    {
        SetNeverPickUp();
        Age = Lifetime - 1;
    }

    //MergeWithNeighbours 与半格范围内同类掉落物合并 对应原版 mergeWithNeighbours
    //范围查询走关卡的空间索引 分桶结果不精确故再按包围盒相交过滤
    public void MergeWithNeighbours()
    {
        if (!IsMergable || Level is not PersistentServerLevel level) return;
        var area = BoundingBox.Inflate(0.5, 0, 0.5);
        foreach (var candidate in level.EntityLookup.GetInRange(area.Min, area.Max))
        {
            if (ReferenceEquals(candidate, this) || candidate is not ItemEntity other) continue;
            if (!area.Intersects(other.BoundingBox) || !other.IsMergable) continue;
            TryToMerge(other);
            //合并后本实体可能已被移除 原版同样在这里直接返回
            if (IsRemoved) return;
        }
    }

    //IsMergable 该掉落物能否参与合并 对应原版 isMergable
    //永不拾取/无限存活/已到寿命/已堆满的都不合并
    public bool IsMergable
        => !IsRemoved && PickupDelay != InfinitePickupDelay && Age != InfiniteLifetime
            && Age < Lifetime && !Item.IsEmpty() && Item.GetCount() < Item.GetMaxStackSize();

    //AreMergable 两栈能否合并 对应原版 areMergable
    public static bool AreMergable(ItemStack first, ItemStack second)
    {
        if (first.IsEmpty() || second.IsEmpty()) return false;
        if (first.GetCount() + second.GetCount() > first.GetMaxStackSize()) return false;
        return first.IsSameItemAndComponentsAs(second);
    }

    //GetSpin 掉落物旋转相位 对应原版 getSpin 客户端渲染用
    public static float GetSpin(float ageInTicks, float bobOffset) => ageInTicks / 20f + bobOffset;

    //VisualRotationYInDegrees 掉落物朝向角 对应原版 getVisualRotationYInDegrees
    public float VisualRotationYInDegrees
        => 180f - GetSpin(Age + 0.5f, BobOffset) / (MathF.PI * 2f) * 360f;

    //AddAdditionalSaveData 存档字段名与类型对齐原版 基类已跳过 float Health
    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        tag.PutShort("Health", (short)ItemHealth);
        tag.PutShort("Age", (short)Age);
        tag.PutShort("PickupDelay", (short)PickupDelay);
        //原版两个字段都按 UUIDUtil.CODEC 写 即 4 个 int 的数组
        if (Thrower is { } thrower) tag.PutIntArray("Thrower", UuidToIntArray(thrower));
        if (Owner is { } owner) tag.PutIntArray("Owner", UuidToIntArray(owner));
        if (!Item.IsEmpty()) ItemStack.WriteNbt(tag, "Item", Item);
    }

    //ReadAdditionalSaveData 读回物品与状态 物品不可用时当场移除
    protected override void ReadAdditionalSaveData(CompoundTag tag)
    {
        ItemHealth = tag.GetShort("Health")?.Value ?? DefaultHealth;
        Age = tag.GetShort("Age")?.Value ?? 0;
        PickupDelay = tag.GetShort("PickupDelay")?.Value ?? 0;
        if (tag.GetIntArray("Thrower") is { } thrower) Thrower = IntArrayToUuid(thrower.Value);
        if (tag.GetIntArray("Owner") is { } owner) Owner = IntArrayToUuid(owner.Value);
        Item = ItemStack.ReadNbt(tag.GetCompound("Item"));
        //存档里没有可用物品与 summon 给空 NBT 一样当场移除
        if (Item.IsEmpty()) Discard();
    }

    //TryToMerge 数量少的并进数量多的 对应原版 tryToMerge
    private void TryToMerge(ItemEntity other)
    {
        if (!AreMergable(Item, other.Item)) return;
        if (other.Item.GetCount() < Item.GetCount()) Merge(this, other);
        else Merge(other, this);
    }

    //Merge 把 from 的数量搬进 to 并取两者的拾取延迟较大值与年龄较小值
    //数量搬空后 from 立即移除 对应原版 fromStack.isEmpty() 分支
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

    //GroundFriction 脚下方块的摩擦系数 对应原版 getBlockPosBelowThatAffectsMyMovement
    //关卡未接入或该位置无方块时退回默认摩擦
    private double GroundFriction()
    {
        if (Level is not PersistentServerLevel level) return DefaultFriction;
        var below = new BlockPos(Mth.Floor(Pos.X), Mth.Floor(Pos.Y - 1e-7), Mth.Floor(Pos.Z));
        return level.GetBlockState(below)?.Owner.Friction ?? (float)DefaultFriction;
    }

    //HorizontalSpeedSqr 水平速度平方
    private static double HorizontalSpeedSqr(Vec3 velocity) => velocity.X * velocity.X + velocity.Z * velocity.Z;
}
