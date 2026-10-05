using System.Buffers.Binary;
using System.Threading;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Util;
using NetCraft.Util.Random;
//实体属性自带命名空间 与环境属性(NetCraft.Registry.Environment)里同名类型分开
//Attribute 与 System.Attribute 撞名 这里只取需要的三个名字并给它换个别名
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;

namespace NetCraft.Registry;

//ITrackedEntity 实体追踪层需要的实体视图
//服务端实体与管理实体数据的玩家对象都实现它 追踪器只依赖这组读写能力
public interface ITrackedEntity
{
    //EntityId 实体网络 id
    int EntityId { get; }

    //Type 实体类型 null 表示不支持追踪
    EntityType<object>? Type { get; }

    //Uuid 实体唯一标识
    Guid Uuid { get; }

    //Pos 实体位置
    Vec3 Pos { get; }

    //Velocity 实体速度
    Vec3 Velocity { get; }

    //YRot 偏航角
    float YRot { get; }

    //XRot 俯仰角
    float XRot { get; }

    //OnGround 是否接触地面
    bool OnGround { get; }

    //Attributes 实体属性表 没有属性体系的实体返回 null 追踪层据此跳过属性同步
    AttributeMap? Attributes { get; }
}

//Entity 抽象基类对应原版 net.minecraft.world.entity.Entity
//持有 EntityId/Pos/Uuid/Velocity/YRot/XRot 核心字段Level 用 object 占位待 Level 子系统就绪后替换
//原版持有 CompoundTag 持久化字段此处简化子类按需扩展
public abstract class Entity : ITrackedEntity, ISyncedEntity
{
    //_entityCounter 全局实体 id 计数器 对应原版 ServerLevel.ENTITY_COUNTER
    //放在这里而不是关卡上 让实体与玩家共用同一个 id 空间 两套计数器会撞号
    private static int _entityCounter;

    //SharedFlagsIndex 共享标志位索引 对应原版 Entity.DATA_SHARED_FLAGS_ID
    public const byte SharedFlagsIndex = 0;

    //PoseIndex 姿态索引 对应原版 Entity.DATA_POSE
    public const byte PoseIndex = 6;

    //SyncedData 实体元数据容器 子类构造时 Define 声明自己拥有的条目
    public SynchedEntityData SyncedData { get; } = new();

    //_deathHandled 死亡钩子是否已触发 保证只走一次死亡流程
    private bool _deathHandled;

    //Gravity 重力加速度 对应原版 0.08
    public const double Gravity = 0.08;

    //VerticalDrag 垂直阻力 对应原版 0.98
    public const double VerticalDrag = 0.98;

    //HorizontalDrag 水平阻力 对应原版 0.91
    public const double HorizontalDrag = 0.91;

    //DefaultGravity 默认重力加速度 对应原版 getDefaultGravity 掉落物重写为 0.04
    public virtual double DefaultGravity => Gravity;

    //SafeFallDistance 起算坠落伤害的距离 对应原版属性 SAFE_FALL_DISTANCE 默认 3
    public virtual double SafeFallDistance => 3.0;

    //FallDamageMultiplier 坠落伤害系数 对应原版属性 FALL_DAMAGE_MULTIPLIER 默认 1
    public virtual double FallDamageMultiplier => 1.0;

    //KnockbackResistance 击退抗性 0 到 1 之间 1 表示完全免疫击退 对应原版属性 KNOCKBACK_RESISTANCE
    public virtual double KnockbackResistance => 0.0;

    //TakesFallDamage 是否受坠落伤害 对应原版 Entity 基类不受而 LivingEntity 受
    //本作没有 LivingEntity 这一层 由活体子类打开
    public virtual bool TakesFallDamage => false;

    //IsInWater 是否浸在水中 本作没有流体判定恒否 对应原版 wasTouchingWater
    //在流体判定接入前它让所有下落都累积距离 与原版陆上行为一致
    public virtual bool IsInWater => false;

    //KnockbackDirectionEpsilon 击退方向的最小长度平方 对应原版 9.999999747378752E-6
    //方向分量短于这个量级时随机化 免得纯竖直击退把实体原地钉住
    private const double KnockbackDirectionEpsilon = 9.999999747378752E-6;

    //_random 实体自己的随机源 对应原版 Entity.random 用于击退方向随机化等
    private readonly RandomSource _random = RandomSource.Create();

    //FallDistance 本刻之前已累积的下落距离 对应原版 fallDistance 落地或触及重置面时清零
    public double FallDistance { get; private set; }

    //AirDrag 空气阻力 对应原版 getAirDrag 默认 0.98
    public virtual double AirDrag => VerticalDrag;

    //SavesHealth 基类是否代写 Health 字段
    //掉落物的 Health 是与生物同名但类型为 short 的独立字段 重写为 false 由子类自己写
    protected virtual bool SavesHealth => true;

    //TagsTag 实体标签在存档里的键名 对应原版 "Tags"
    private const string TagsTag = "Tags";

    //MaxTagCount 单个实体的标签数量上限 对应原版 1024 加满再加直接失败
    private const int MaxTagCount = 1024;

    //_tags 实体自定义字符串标签集合 对应原版 Entity.tags
    private readonly HashSet<string> _tags = new();

    //GetTags 取全部标签 对应原版 entityTags
    public IReadOnlyCollection<string> GetTags() => _tags;

    //AddTag 加标签 已存在或已达上限返回 false 对应原版 addTag
    public bool AddTag(string tag)
    {
        if (_tags.Count >= MaxTagCount) return false;
        return _tags.Add(tag);
    }

    //RemoveTag 移除标签 不存在返回 false 对应原版 removeTag
    public bool RemoveTag(string tag) => _tags.Remove(tag);

    //HasTag 实体是否有该标签 供选择器一类的判定使用
    public bool HasTag(string tag) => _tags.Contains(tag);

    //EntityId 实体网络 id 加入关卡时由关卡分配 未加入前是 0
    //对应原版构造里先置 0 再 level.getNextEntityId() 本作关卡引用构造后才注入所以分配一并延后
    public int EntityId { get; private set; }

    //SetId 直接指定实体 id 对应原版 Entity.setId
    public void SetId(int id) => EntityId = id;

    //NextEntityId 取下一个可用实体 id 对应原版 ServerLevel.getNextEntityId
    //从 0 起试 0 与已被占用的 id 都跳过 isTaken 由关卡回答某个 id 是否已被占用
    //分配出的 id 要由调用方立刻登记进占用集合 否则下一次分配会拿到同一个值
    public static int NextEntityId(Func<int, bool> isTaken)
    {
        var candidate = 0;
        while (true)
        {
            if (candidate != 0 && !isTaken(candidate)) return candidate;
            candidate = Interlocked.Increment(ref _entityCounter);
        }
    }

    //Id 实体的注册名子类必须实现
    public abstract Identifier Id { get; }

    //Type 实体类型 子类按需重写 未重写时追踪层跳过该实体
    public virtual EntityType<object>? Type => null;

    //Level 实体所在世界引用占位待 Level 子系统就绪后替换为强类型
    public object? Level { get; set; }

    //CollisionShapes 测试区内的碰撞形状查询 由关卡注入 未注入时不做碰撞只做位置推进
    //原版这一层是 level 上的实体碰撞加世界边界加方块碰撞三样 这里由关卡一并给全
    public Func<AABB, IReadOnlyList<VoxelShape>>? CollisionShapes { get; set; }

    //NoPhysics 是否忽略碰撞直接推进 对应原版 noPhysics
    public bool NoPhysics { get; set; }

    //HorizontalCollision 本刻水平两轴是否有任一受阻 对应原版 horizontalCollision
    public bool HorizontalCollision { get; private set; }

    //VerticalCollision 本刻竖直方向是否受阻 对应原版 verticalCollision
    public bool VerticalCollision { get; private set; }

    //VerticalCollisionBelow 本刻向下受阻 站地与否看它而不是看竖直受阻 对应原版 verticalCollisionBelow
    public bool VerticalCollisionBelow { get; private set; }

    //Attributes 实体属性表 基类默认空表 活体子类在构造里换成自己类型的默认表
    //对应原版 LivingEntity 持有的 AttributeMap
    public AttributeMap Attributes { get; protected set; } = new(AttributeSupplier.Empty);

    //GetAttributeValue 取属性最终值 对应原版 getAttributeValue
    public double GetAttributeValue(AttributeDef attribute) => Attributes.GetValue(attribute);

    //MaxUpStep 能自动跨上的最大台阶高度 基类为 0 生物按跨步高度属性覆盖 对应原版 maxUpStep
    public virtual double MaxUpStep => 0.0;

    //Pos 实体在世界中的位置默认原点
    public Vec3 Pos { get; set; } = Vec3.Zero;

    //Velocity 实体速度向量默认零
    public Vec3 Velocity { get; set; } = Vec3.Zero;

    //Uuid 实体唯一标识默认随机生成
    public Guid Uuid { get; set; } = Guid.NewGuid();

    //YRot/Yaw 偏航角默认 0
    public float YRot { get; set; }

    //XRot/Pitch 俯仰角默认 0
    public float XRot { get; set; }

    //OnGround 是否接触地面默认 false
    public bool OnGround { get; set; }

    //IsOnFire 是否正在燃烧 对应原版 isOnFire
    public bool IsOnFire { get; set; }

    //IsCrouching 是否潜行 对应原版 isCrouching
    public bool IsCrouching { get; set; }

    //IsSprinting 是否疾跑 对应原版 isSprinting
    public bool IsSprinting { get; set; }

    //IsSwimming 是否处于游泳姿态 对应原版 isSwimming
    public bool IsSwimming { get; set; }

    //IsBaby 是否幼年 对应原版 LivingEntity.isBaby
    public bool IsBaby { get; set; }

    //IsFallFlying 是否鞘翅滑翔 对应原版 LivingEntity.isFallFlying
    public bool IsFallFlying { get; set; }

    //IsFlying 是否处于飞行 对应原版玩家能力 flying 本作所有实体通用
    public bool IsFlying { get; set; }

    //IsDescending 是否处于下落姿态 对应原版 isDescending
    //碰撞上下文按它放宽脚手架一类方块的侧向判定 基类实体一律否
    public virtual bool IsDescending() => false;

    //TickCount 实体已存活刻数 对应原版 tickCount 掉落物按它决定合并频率与消失
    public int TickCount { get; private set; }

    //PreviousPos 进入本刻前的位置 对应原版 xo/yo/zo 用于判断本刻是否跨了方块格
    public Vec3 PreviousPos { get; private set; } = Vec3.Zero;

    //MovementEpsilon 位移阈值 小于它认为没动 对应原版 1.0E-7
    private const double MovementEpsilon = 1e-7;

    //Width 碰撞盒宽度取实体类型声明的尺寸 未绑定类型时按玩家尺寸
    public double Width => Type?.Width ?? 0.6f;

    //Height 碰撞盒高度取实体类型声明的尺寸 未绑定类型时按玩家尺寸
    public double Height => Type?.Height ?? 1.8f;

    //BoundingBox 当前包围盒 对应原版 getBoundingBox
    public AABB BoundingBox => MakeBoundingBox(Pos);

    //MakeBoundingBox 按脚部位置生成包围盒 以脚部为中心向两侧各半宽 对应原版 makeBoundingBox
    public AABB MakeBoundingBox(Vec3 pos)
    {
        var half = Width / 2.0;
        return new AABB(pos.X - half, pos.Y, pos.Z - half, pos.X + half, pos.Y + Height, pos.Z + half);
    }

    //Health 当前血量 默认 20 对齐原版 MAX_HEALTH 默认值
    public float Health { get; private set; } = 20f;

    //MaxHealth 血量上限 子类按需覆盖
    public float MaxHealth { get; protected set; } = 20f;

    //InvulnerableTime 受伤无敌帧剩余刻数 对应原版 invulnerableTime
    public int InvulnerableTime { get; private set; }

    //IsDeadOrDying 血量归零 对应原版 isDeadOrDying
    public bool IsDeadOrDying => Health <= 0f;

    //Hurt 造成伤害 对应原版 hurtServer 的最小集
    //无敌帧内不重复受伤 未做伤害减免/伤害来源类型/死亡动画期
    //给了伤害来源位置就顺带施加击退 方向由来源指向自身
    public virtual bool Hurt(float amount, Vec3? knockbackSource = null)
    {
        if (IsDeadOrDying || InvulnerableTime > 0) return false;
        Health = Math.Max(0f, Health - amount);
        //无敌帧 10 刻 原版是 20 刻其中 10 刻给受伤动画
        InvulnerableTime = 10;
        //受伤击退 力度 0.4 对应原版 LivingEntity.dealDefaultKnockback
        //方向按原版算"来源减自身" 击退内部再取负 净效果是把目标推离来源
        if (knockbackSource is { } source)
            ApplyKnockback(0.4, source.X - Pos.X, source.Z - Pos.Z);
        if (Health <= 0f) TriggerDeath();
        return true;
    }

    //SetHealth 直接设置血量并钳制到 0..MaxHealth 对应原版 setHealth
    public void SetHealth(float value)
    {
        Health = Math.Clamp(value, 0f, MaxHealth);
        //血量回到正值视为复活 重新允许触发死亡流程
        if (Health > 0f)
        {
            _deathHandled = false;
            return;
        }
        TriggerDeath();
    }

    //Die 血量归零钩子 对应原版 LivingEntity.die 子类做掉落与死亡表现
    protected virtual void Die() { }

    //Died 死亡事件 由关卡在实体加入时挂接 供服务端广播死亡表现并移除实体
    public event Action<Entity>? Died;

    //TriggerDeath 只触发一次死亡钩子 避免 Hurt 与 SetHealth 重复走死亡流程
    private void TriggerDeath()
    {
        if (_deathHandled) return;
        _deathHandled = true;
        Die();
        Died?.Invoke(this);
    }

    //Save 写出完整实体存档含类型 id 对应原版 Entity.save
    public void Save(CompoundTag tag)
    {
        tag.PutString("id", (Type?.Id ?? Id).ToString());
        SaveWithoutId(tag);
    }

    //SaveWithoutId 写出实体状态不含类型 id 对应原版 Entity.saveWithoutId
    //子类扩展持久化字段应重写 AddAdditionalSaveData
    public virtual void SaveWithoutId(CompoundTag tag)
    {
        tag.Put("Pos", DoubleList(Pos.X, Pos.Y, Pos.Z));
        tag.Put("Motion", DoubleList(Velocity.X, Velocity.Y, Velocity.Z));
        tag.Put("Rotation", FloatList(YRot, XRot));
        tag.PutIntArray("UUID", UuidToIntArray(Uuid));
        tag.PutBoolean("OnGround", OnGround);
        if (SavesHealth) tag.PutFloat("Health", Health);
        //标签是基类行为 非空才写 对应原版 saveWithoutId 里 Tags 的处理
        if (_tags.Count > 0) tag.Put(TagsTag, StringList(_tags));
        //属性只在活体上存 本作由 TakesFallDamage 代原版 LivingEntity 这一层
        if (TakesFallDamage) Attributes.WriteTo(tag);
        AddAdditionalSaveData(tag);
    }

    //Load 从存档读回实体状态对应原版 Entity.load
    //字段缺失或类型不符时保留当前值 不因单个字段异常丢弃整个实体
    public virtual void Load(CompoundTag tag)
    {
        if (ReadDoubleList(tag.GetList("Pos"), 3) is { } pos) Pos = new Vec3(pos[0], pos[1], pos[2]);
        if (ReadDoubleList(tag.GetList("Motion"), 3) is { } motion) Velocity = new Vec3(motion[0], motion[1], motion[2]);
        if (ReadFloatList(tag.GetList("Rotation"), 2) is { } rotation)
        {
            YRot = rotation[0];
            XRot = rotation[1];
        }
        //UUID 为 4 个 int 的数组 长度不符时视为无效保留原值
        if (tag.GetIntArray("UUID") is { } uuid && IntArrayToUuid(uuid.Value) is { } parsed) Uuid = parsed;
        OnGround = tag.GetBooleanOr("OnGround", false);
        if (SavesHealth && tag.GetFloat("Health") is { } health) SetHealth(health.Value);
        //标签先清空再读 对应原版 load 里 tags.clear 后再 addAll
        _tags.Clear();
        if (tag.GetList(TagsTag) is { } tagList)
            for (var i = 0; i < tagList.Count; i++)
                if (tagList.GetString(i) is { } entry) _tags.Add(entry.Value);
        if (TakesFallDamage) Attributes.ReadFrom(tag);
        ReadAdditionalSaveData(tag);
    }

    //AddAdditionalSaveData 子类追加持久化字段的钩子 对应原版同名方法
    protected virtual void AddAdditionalSaveData(CompoundTag tag) { }

    //ReadAdditionalSaveData 子类读回扩展字段的钩子 对应原版同名方法
    protected virtual void ReadAdditionalSaveData(CompoundTag tag) { }

    //DoubleList 构造 double 列表 对应原版 ListTag of DoubleTag
    private static ListTag DoubleList(double x, double y, double z)
        => new(new Tag[] { new DoubleTag(x), new DoubleTag(y), new DoubleTag(z) });

    //FloatList 构造 float 列表 对应原版 ListTag of FloatTag
    private static ListTag FloatList(float a, float b)
        => new(new Tag[] { new FloatTag(a), new FloatTag(b) });

    //StringList 构造字符串列表 对应原版 ListTag of StringTag
    private static ListTag StringList(IReadOnlyCollection<string> values)
    {
        var list = new ListTag();
        foreach (var value in values) list.Add(new StringTag(value));
        return list;
    }

    //ReadDoubleList 读定长 double 列表 长度不符返回 null
    private static double[]? ReadDoubleList(ListTag? list, int size)
    {
        if (list is null || list.Count != size) return null;
        var values = new double[size];
        for (var i = 0; i < size; i++)
        {
            var tag = list.GetDouble(i);
            if (tag is null) return null;
            values[i] = tag.Value;
        }
        return values;
    }

    //ReadFloatList 读定长 float 列表 长度不符返回 null
    private static float[]? ReadFloatList(ListTag? list, int size)
    {
        if (list is null || list.Count != size) return null;
        var values = new float[size];
        for (var i = 0; i < size; i++)
        {
            var tag = list.GetFloat(i);
            if (tag is null) return null;
            values[i] = tag.Value;
        }
        return values;
    }

    //UuidToIntArray 把 Uuid 按原版格式写成 4 个 int 大端序列
    //子类存 Owner/Thrower 一类 UUID 字段也走这个 与原版 UUIDUtil.CODEC 的写法一致
    protected static int[] UuidToIntArray(Guid uuid)
    {
        Span<byte> bytes = stackalloc byte[16];
        uuid.TryWriteBytes(bytes, bigEndian: true, out _);
        var result = new int[4];
        for (var i = 0; i < 4; i++)
            result[i] = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(i * 4, 4));
        return result;
    }

    //IntArrayToUuid 还原原版格式的 Uuid 长度不符返回 null
    protected static Guid? IntArrayToUuid(int[] value)
    {
        if (value.Length != 4) return null;
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32BigEndian(bytes.Slice(i * 4, 4), value[i]);
        return new Guid(bytes, bigEndian: true);
    }

    //IsRemoved 实体是否已被标记移除 对应原版 Entity.isRemoved
    public bool IsRemoved { get; private set; }

    //BlocksBuilding 实体是否阻挡方块放置 对应原版 Entity.blocksBuilding
    //原版默认关 活体在构造里打开 掉落物一类不挡放置
    public virtual bool BlocksBuilding => false;

    //Discard 标记实体移除 对应原版 Entity.discard
    //只打标记不立即摘除 避免在实体 tick 遍历中改动集合 摘除由 EntityManager 在 tick 末尾统一做
    public void Discard() => IsRemoved = true;

    //SetPos 同时设置 Pos 与角度对齐原版 moveTo/moveTo
    public void SetPos(Vec3 pos, float yRot, float xRot)
    {
        Pos = pos;
        YRot = yRot;
        XRot = xRot;
    }

    //Tick 每帧调用 基础物理为重力加速度与按速度推进位置
    //子类重写时应先调用 base.Tick 保留重力与移动 再叠加自身行为
    public virtual void Tick()
    {
        TickBase();
        ApplyDefaultPhysics();
    }

    //TickBase 推进存活刻数与无敌帧 对应原版 Entity.baseTick 的最小集
    //自带物理的子类改调它 避免与基类默认物理叠加成双重移动
    protected void TickBase()
    {
        TickCount++;
        PreviousPos = Pos;
        //无敌帧每刻递减 对应原版 LivingEntity.tick 里的 invulnerableTime--
        if (InvulnerableTime > 0) InvulnerableTime--;
    }

    //ApplyDefaultPhysics 默认物理: 重力扣减竖直速度 水平按阻力衰减 再按速度推进
    protected void ApplyDefaultPhysics()
    {
        Velocity = new Vec3(
            Velocity.X * HorizontalDrag,
            (Velocity.Y - DefaultGravity) * VerticalDrag,
            Velocity.Z * HorizontalDrag);
        Move(Velocity);
    }

    //ApplyKnockback 施加击退 对应原版 LivingEntity.knockback
    //水平速度减半再加反向推力 站在地面时竖直速度取 min(0.4, 原速一半+力度) 空中不改竖直速度
    //方向分量过小时随机化 对应原版避免纯竖直击退
    public void ApplyKnockback(double power, double xd, double zd)
    {
        var effective = power * (1.0 - KnockbackResistance);
        if (effective <= 0.0) return;
        while (xd * xd + zd * zd < KnockbackDirectionEpsilon)
        {
            xd = (_random.NextDouble() - _random.NextDouble()) * 0.01;
            zd = (_random.NextDouble() - _random.NextDouble()) * 0.01;
        }
        var push = new Vec3(xd, 0.0, zd).Normalize().Multiply(effective);
        Velocity = new Vec3(
            Velocity.X / 2.0 - push.X,
            OnGround ? Math.Min(0.4, Velocity.Y / 2.0 + effective) : Velocity.Y,
            Velocity.Z / 2.0 - push.Z);
    }

    //CheckFallDamage 累积下落距离并在落地时结算 对应原版 Entity.checkFallDamage
    //水中下落不累积 落地时交给落地钩子处理再清零
    //参数是裁剪后的竖直位移而不是原始位移 贴着地面下滑时不该被算成下落
    private void CheckFallDamage(double ya)
    {
        if (!IsInWater && ya < 0.0) FallDistance -= ya;
        if (!OnGround) return;
        if (FallDistance > 0.0) OnLandedOnGround(FallDistance);
        ResetFallDistance();
    }

    //OnLandedOnGround 落地钩子 对应原版 Block.fallOn 的默认实现
    //本作没有方块行为层 直接按坠落距离结算伤害 特殊方块(干草块/床/史莱姆)的差别待方块行为就绪
    protected virtual void OnLandedOnGround(double fallDistance) => CauseFallDamage(fallDistance, 1.0f);

    //ResetFallDistance 清零下落距离 对应原版 resetFallDistance
    public void ResetFallDistance() => FallDistance = 0.0;

    //CauseFallDamage 结算坠落伤害 对应原版 Entity.causeFallDamage 与 LivingEntity.causeFallDamage
    //基类不受伤 活体子类打开 TakesFallDamage 后按距离扣血
    public virtual bool CauseFallDamage(double fallDistance, float damageModifier)
    {
        if (!TakesFallDamage) return false;
        var damage = CalculateFallDamage(fallDistance, damageModifier);
        return damage > 0 && Hurt(damage);
    }

    //CalculateFallDamage 按坠落距离算伤害 对应原版 LivingEntity.calculateFallDamage
    //超过安全距离的部分乘系数后向下取整 安全距离之内算出 0 或负数也就是不受伤
    protected int CalculateFallDamage(double fallDistance, float damageModifier)
        => Mth.Floor(((fallDistance + 1.0E-6) - SafeFallDistance) * damageModifier * FallDamageMultiplier);

    //Move 按增量推进位置 对应原版 Entity.move
    //碰撞按原版 collideBoundingBox 的路子取移动全程扫过的区域内的碰撞形状 再逐轴裁剪
    //受阻轴速度清零其余保持 向下受阻才置站地 对应原版 restituteMovementAfterCollisions 默认弹性为 0
    public void Move(Vec3 delta)
    {
        if (NoPhysics)
        {
            Pos = Pos.Add(delta);
            HorizontalCollision = false;
            VerticalCollision = false;
            VerticalCollisionBelow = false;
            return;
        }
        var movement = Collide(delta);
        //位移太小时不推进位置 避免贴面时反复抖动 对应原版那两个阈值的与
        if (movement.LengthSqr() > MovementEpsilon
            || delta.LengthSqr() - movement.LengthSqr() < MovementEpsilon)
            Pos = Pos.Add(movement);

        var xCollision = !Mth.Equal(delta.X, movement.X);
        var zCollision = !Mth.Equal(delta.Z, movement.Z);
        HorizontalCollision = xCollision || zCollision;
        var movedVertically = Math.Abs(delta.Y) > 0.0;
        //竖直位移为 0 时不判站地 否则水平贴墙会被误判成落地 原版同此
        if (movedVertically)
        {
            VerticalCollision = delta.Y != movement.Y;
            VerticalCollisionBelow = VerticalCollision && delta.Y < 0.0;
            OnGround = VerticalCollisionBelow;
        }
        //水平受阻或竖直受阻才动速度 原版 restituteMovementAfterCollisions 默认弹性为 0
        //受阻轴清零 没受阻的轴保持原速度 用裁剪后的位移当速度会让实体一直顶着墙
        var verticalBlocked = movedVertically && VerticalCollision;
        if (HorizontalCollision || verticalBlocked)
        {
            Velocity = new Vec3(
                xCollision ? 0.0 : Velocity.X,
                verticalBlocked ? 0.0 : Velocity.Y,
                zCollision ? 0.0 : Velocity.Z);
        }
        //坠落距离按裁剪后的位移累积 落地时结算伤害 对应原版 move 尾部的 checkFallDamage
        CheckFallDamage(movement.Y);
    }

    //Collide 把位移裁剪到不撞上任何碰撞形状 对应原版 Entity.collide
    //世界边界体系与台阶自动跨越未接入 世界边界恒不挡路 台阶要等实体带跨步高度再来
    private Vec3 Collide(Vec3 movement)
    {
        if (CollisionShapes is not { } query) return movement;
        var box = BoundingBox;
        //形状查询是惰性的 逐轴裁剪会反复遍历 这里先落实成表
        var colliders = query(box.ExpandTowards(movement));
        return colliders.Count == 0 ? movement : CollideWithShapes(movement, box, colliders);
    }

    //CollideWithShapes 逐轴裁剪位移 对应原版 Entity.collideWithShapes
    //解完一轴把盒体挪过去再解下一轴 轴序由位移决定见 Direction.AxisStepOrder
    private static Vec3 CollideWithShapes(Vec3 movement, AABB box, IReadOnlyList<VoxelShape> shapes)
    {
        var resolved = Vec3.Zero;
        foreach (var axis in Direction.AxisStepOrder(movement))
        {
            var amount = axis.Choose(movement.X, movement.Y, movement.Z);
            if (amount == 0.0) continue;
            var clipped = Shapes.Collide(axis, box.Move(resolved), shapes, amount);
            resolved = resolved.WithAxis(axis, clipped);
        }
        return resolved;
    }
}
