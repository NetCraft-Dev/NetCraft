using NetCraft.Network.Protocol.Common;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Network.Protocol.Login;
using NetCraft.Game.World.Effect;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Primitives;
//属性相关类型自带命名空间 这里只取需要的几个名字
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;
//效果类与注册表占位接口同名 这里给效果实现类取短别名
using GameMobEffect = NetCraft.Game.World.Effect.MobEffect;

namespace NetCraft.Game.Server;

//ServerPlayer 服务端玩家对象对应原版 ServerPlayer
//最小实现持有 Connection/GameProfile/坐标/游戏模式供 PlayerList 管理
//不继承 NetCraft.Registry.Entity 避免与已有实体体系混淆本轮只做网络层玩家
//实现 ITrackedEntity 让玩家与实体走同一条追踪同步链路
//实现 ISyncedEntity 让元数据同步与掉落物等实体共用一套机制
public sealed class ServerPlayer : ITrackedEntity, ISyncedEntity
{
    //EntityId 服务端分配的实体 id 用于 ClientboundLoginPacket 等同步
    //与 Registry.Entity 共用同一个分配器 两套自增器会撞号导致攻击包找错目标
    public int EntityId { get; private set; }
    public GameProfile Profile { get; }
    public Connection Connection { get; }
    public ServerLevel Level { get; }

    //GameType 玩家游戏模式 PlaceNewPlayer 时从服务端默认模式初始化
    //切换时同步背包的无限材料标记: 创造模式塞不下时按原版直接吞掉物品不算失败
    //只有创造开 instabuild 旁观没有 与原版 updatePlayerAbilities 一致
    public GameType GameType
    {
        get => _gameType;
        set
        {
            _gameType = value;
            Inventory.InfiniteMaterials = value == GameType.Creative;
            //能力随模式重算 创造分支不动飞行状态 玩家按出来的飞行切模式后还在 对应原版 updatePlayerAbilities
            Abilities.ApplyGameType(value);
        }
    }

    private GameType _gameType = GameType.Survival;

    //Abilities 玩家能力状态 入场下发能力包与玩家存档都读它 对应原版 Player.abilities
    public Abilities Abilities { get; } = new();

    //PermissionLevel 权限等级 0-4 加入时由 PlayerList 按 ops.json 设置
    //op/deop 变更后由 PlayerList 更新并同步客户端 对应原版 ServerPlayer 的 permission level
    public int PermissionLevel { get; set; }

    //HasPermissions 是否达到指定权限等级 对应原版 ServerPlayer.hasPermissions
    public bool HasPermissions(int level) => PermissionLevel >= level;

    //Position 玩家坐标默认出生点 0,0,0 后续接入出生点逻辑
    public Vec3 Position { get; set; } = new(0, 64, 0);
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    //_lastActivePosition 上一刻位置 与当前位置不同即认为玩家在活动
    private Vec3 _lastActivePosition;

    //LastActiveMillis 最后一次活动时刻 挂机踢出计时基准
    public long LastActiveMillis { get; private set; } = Environment.TickCount64;

    //RespawnPos 个人重生点 对应原版 spawnpoint 命令设置的玩家重生位置 为空时回落到世界出生点
    public Vec3? RespawnPos { get; set; }

    //RespawnAngle 个人重生朝向
    public float RespawnAngle { get; set; }

    //Velocity/OnGround 服务端记录的玩家运动状态 玩家移动由客户端上报后回填
    public Vec3 Velocity { get; set; } = Vec3.Zero;
    public bool OnGround { get; set; }

    //--- 旁观相机 对应原版 ServerPlayer.camera ---

    //IsSpectator 是否处于旁观模式 对应原版 isSpectator
    public bool IsSpectator => GameType == GameType.Spectator;

    //_camera 当前旁观相机实体 null 表示视角在自己 对应原版 camera 字段
    private ITrackedEntity? _camera;

    //GetCamera 当前相机实体 未旁观时返回自身 对应原版 getCamera
    public ITrackedEntity GetCamera() => _camera ?? this;

    //SetCamera 切换旁观相机 对应原版 ServerPlayer.setCamera
    //null 表示回到自身视角 切换时把位置同步到相机处并下发设置相机包
    //跨维度旁观未实现: ServerPlayer.Level 构造后固定 相机在其它维度时只跟随位置不换维度
    public void SetCamera(ITrackedEntity? newCamera)
    {
        var oldCamera = GetCamera();
        _camera = newCamera ?? this;
        if (ReferenceEquals(oldCamera, _camera)) return;
        //位置落到相机处 区块加载与实体追踪跟随相机 对应原版 setCamera 里的 teleportTo
        var pos = _camera.Pos;
        if (Listener is { } listener)
            listener.Teleport(pos, Yaw, Pitch, pos.X, pos.Y, pos.Z, Yaw, Pitch, 0);
        else
            Position = pos;
        Connection.Send(new ClientboundSetCameraPacket(_camera.EntityId));
    }

    //TickCamera 旁观者每刻跟随相机位置 对应原版 ServerPlayer.tick 的 camera 分支
    //相机实体被移除时回到自身视角 位置直接落服务端状态 客户端视角已由相机包接管
    private void TickCamera()
    {
        if (_camera is not { } camera || ReferenceEquals(camera, this)) return;
        if (camera is NetCraft.Registry.Entity { IsRemoved: true })
        {
            SetCamera(null);
            return;
        }
        Position = camera.Pos;
        Yaw = camera.YRot;
        Pitch = camera.XRot;
    }

    //--- ITrackedEntity 实现 ---

    //Type 玩家实体类型 玩家与实体走同一追踪链路按该类型下发 AddEntity
    public EntityType<object>? Type => EntityTypes.PLAYER;

    //Uuid 玩家唯一标识沿用 GameProfile 的 id
    public Guid Uuid => Profile.Id;

    Vec3 ITrackedEntity.Pos => Position;
    float ITrackedEntity.YRot => Yaw;
    float ITrackedEntity.XRot => Pitch;

    //Attributes 玩家属性表 按实体类型取默认表
    //对应原版 ServerPlayer 经 LivingEntity 持有 AttributeMap 这一层
    public AttributeMap Attributes { get; }

    //GetAttributeValue 取属性最终值 对应原版 getAttributeValue
    public double GetAttributeValue(AttributeDef attribute) => Attributes.GetValue(attribute);

    //--- 姿态与共享标志 对应原版 Entity.DATA_SHARED_FLAGS_ID 与 DATA_POSE ---

    //PoseStanding/PoseCrouching 姿态 id 对齐原版 Pose 枚举
    public const int PoseStanding = 0;
    public const int PoseCrouching = 5;

    //SyncedData 玩家元数据容器 共享标志位与姿态走它同步 对应原版 SynchedEntityData
    public SynchedEntityData SyncedData { get; } = new();

    //IsSprinting/IsSneaking 疾跑与潜行状态 由 player_command 包切换
    public bool IsSprinting { get; private set; }
    public bool IsSneaking { get; private set; }

    //SharedFlags 共享标志位 位1 潜行 位3 疾跑 其余位本作未用
    public byte SharedFlags => (byte)((IsSneaking ? 1 << 1 : 0) | (IsSprinting ? 1 << 3 : 0));

    //PoseId 当前姿态 潜行时模型下蹲
    public int PoseId => IsSneaking ? PoseCrouching : PoseStanding;

    //SetSprinting 切换疾跑 对应原版 setSprinting
    public void SetSprinting(bool value)
    {
        IsSprinting = value;
        SyncSharedState();
    }

    //SetSneaking 切换潜行 对应原版 setShiftKeyDown
    public void SetSneaking(bool value)
    {
        IsSneaking = value;
        SyncSharedState();
    }

    //SyncSharedState 把共享标志位与姿态写进元数据 Set 内部只在值有变化时计版本
    private void SyncSharedState()
    {
        SyncedData.Set(NetCraft.Registry.Entity.SharedFlagsIndex, SharedFlags);
        SyncedData.Set(NetCraft.Registry.Entity.PoseIndex, PoseId);
    }

    //ChunkSender 渐进区块发送器 PlaceNewPlayer 时由 PlayerList 注入 tick 驱动发送
    public ChunkSender? ChunkSender { get; set; }

    //ViewDistanceChunks 玩家视距区块数 PlaceNewPlayer 时取服务端配置
    //实体追踪按它与实体追踪距离的较小者判定可见性
    public int ViewDistanceChunks { get; set; }

    //KeepAliveIntervalMillis 心跳间隔对齐原版 15 秒客户端 Netty 读超时 30 秒靠它压住
    public const long KeepAliveIntervalMillis = 15000;

    //Inventory 玩家物品栏 随玩家对象创建 菜单槽位最终落到这里
    public PlayerInventory Inventory { get; } = new();

    //OwnerList 所属玩家列表 音效要广播给全服 由 PlayerList.PlaceNewPlayer 注入
    public PlayerList? OwnerList { get; set; }

    //AddItem 内核给予入口 对应原版 Player.addItem
    //所有把物品交到玩家手上的路径都收敛到这里 返回是否放进去了至少一个
    //放不下的数量留在传入栈里 由调用方决定掉落还是留在原地
    public bool AddItem(ItemStack stack) => Inventory.Add(stack);

    //GiveItem 给予物品并把放不下的部分弹在脚下 放进去了就播拾取音效 返回实际放入数量
    //对应原版 give 命令里 inventory.add 加 player.drop 的组合 指令给予与拾取共用这条链路
    public int GiveItem(ItemStack stack)
    {
        var total = stack.GetCount();
        Inventory.Add(stack);
        var placed = total - stack.GetCount();
        if (!stack.IsEmpty()) Drop(stack, randomly: false, thrownFromHand: false);
        if (placed > 0) PlayPickupSound();
        ContainerMenu?.SendAllDataToRemote();
        return placed;
    }

    //PlayPickupSound 播物品入手音效 对应原版 ServerPlayer.onItemPickup
    //音量 0.2 音调按原版公式在两倍附近抖动 这是物品进入背包唯一的声音出口
    public void PlayPickupSound()
    {
        if (OwnerList is null) return;
        var pitch = (Random.Shared.NextSingle() - Random.Shared.NextSingle()) * 0.7f + 1.0f;
        ServerSounds.PlaySound(OwnerList, SoundEvents.ItemPickup, SoundSource.Players,
            Position.X, Position.Y, Position.Z, 0.2f, pitch * 2.0f);
    }

    //EyeHeight 玩家眼高 对应原版玩家实体尺寸里的眼睛高度 丢弃时按它算出手位置
    public const float EyeHeight = 1.62f;

    //DropPickupDelay 丢出后的拾取冷却刻数 对应原版 40 刻
    private const int DropPickupDelay = 40;

    //Drop 丢弃选中槽的物品 对应原版 ServerPlayer.drop(boolean)
    //all 为真整槽丢出 为假只丢一个 背包变更后同步给客户端
    public ItemEntity? Drop(bool all)
    {
        var removed = Inventory.RemoveFromSelected(all);
        ContainerMenu?.SendAllDataToRemote();
        return Drop(removed, randomly: false, thrownFromHand: true);
    }

    //Drop 生成掉落物实体并加入关卡 对应原版 LivingEntity.drop 与 createItemStackToDrop
    //位置取眼睛下方 0.3 格 丢出后带 40 刻拾取冷却 免得刚丢出就被自己捡回
    public ItemEntity? Drop(ItemStack stack, bool randomly, bool thrownFromHand)
    {
        if (stack.IsEmpty()) return null;
        if (Level is not PersistentServerLevel level) return null;
        var drop = new ItemEntity(EntityTypes.ITEM)
        {
            Pos = new Vec3(Position.X, Position.Y + EyeHeight - 0.3, Position.Z),
            Item = stack,
            Thrower = thrownFromHand ? Profile.Id : null,
        };
        drop.SetPickUpDelay(DropPickupDelay);
        drop.Velocity = DropVelocity(randomly);
        level.AddEntity(drop);
        return drop;
    }

    //DropVelocity 丢出速度 对应原版 createItemStackToDrop 的两条分支
    //randomly 用于死亡掉落一类的随机散布 手上丢出按视线方向抛并带少量随机抖动
    private Vec3 DropVelocity(bool randomly)
    {
        if (randomly)
        {
            var power = Random.Shared.NextSingle() * 0.5f;
            var angle = Random.Shared.NextSingle() * MathF.PI * 2f;
            return new Vec3(-MathF.Sin(angle) * power, 0.2, MathF.Cos(angle) * power);
        }
        const float toRadians = MathF.PI / 180f;
        var sinX = MathF.Sin(Pitch * toRadians);
        var cosX = MathF.Cos(Pitch * toRadians);
        var sinY = MathF.Sin(Yaw * toRadians);
        var cosY = MathF.Cos(Yaw * toRadians);
        var spread = Random.Shared.NextSingle() * MathF.PI * 2f;
        var jitter = 0.02f * Random.Shared.NextSingle();
        return new Vec3(
            -sinY * cosX * 0.3f + MathF.Cos(spread) * jitter,
            -sinX * 0.3f + 0.1f + (Random.Shared.NextSingle() - Random.Shared.NextSingle()) * 0.1f,
            cosY * cosX * 0.3f + MathF.Sin(spread) * jitter);
    }

    //GetNearestLookingDirection 玩家视线最近的六向 观察者这类含上下的方块放置时要用 对应原版同名方法
    //视线向量按原版 getViewVector 算 含俯仰所以能落出 UP 与 DOWN
    public Direction GetNearestLookingDirection()
    {
        const float toRadians = MathF.PI / 180f;
        var pitch = Pitch * toRadians;
        var yaw = -Yaw * toRadians;
        var cosPitch = MathF.Cos(pitch);
        return Direction.FromViewVector(
            MathF.Sin(yaw) * cosPitch, -MathF.Sin(pitch), MathF.Cos(yaw) * cosPitch);
    }

    //MaxHealth 满血量取 max_health 属性 对应原版 getMaxHealth
    //装备与效果接入后带修饰符的血量上限会经属性生效
    public float MaxHealth => (float)GetAttributeValue(EntityAttributes.MaxHealth);

    //AttackDamage 空手攻击伤害取 attack_damage 属性 对应原版玩家的 ATTACK_DAMAGE
    //武器加成/冷却系数/暴击未实现 阶段 2 再补
    public float AttackDamage => (float)GetAttributeValue(EntityAttributes.AttackDamage);

    //Health 当前血量 新玩家按 max_health 属性满血 由 playerdata 恢复 入服 SetHealth 包按它下发
    public float Health { get; set; }

    //InvulnerableTime 受伤无敌帧剩余刻数 对应原版 invulnerableTime
    public int InvulnerableTime { get; private set; }

    //IsDeadOrDying 血量归零 对应原版 isDeadOrDying
    public bool IsDeadOrDying => Health <= 0f;

    //Hurt 造成伤害 对应原版 hurtServer 的最小集
    //无敌帧内不重复受伤 未做护甲减免/击退/伤害来源追踪
    public bool Hurt(float amount)
    {
        if (IsDeadOrDying || InvulnerableTime > 0) return false;
        Health = Math.Max(0f, Health - amount);
        //无敌帧 10 刻 原版是 20 刻其中 10 刻给受伤动画
        InvulnerableTime = 10;
        return true;
    }

    //SetHealth 设置血量并钳制到 0..满血 存档恢复与重生走它 对应原版 setHealth
    public void SetHealth(float value) => Health = Math.Clamp(value, 0f, MaxHealth);

    //XpLevel 经验等级 XpProgress 当前级进度 0-1 XpTotal 累计经验
    public int XpLevel { get; set; }
    public float XpProgress { get; set; }
    public int XpTotal { get; set; }

    //ContainerMenu 玩家当前打开的菜单 PlaceNewPlayer 创建背包菜单并下发初始内容
    public AbstractContainerMenu? ContainerMenu { get; set; }

    //BackpackMenu 玩家背包菜单 关闭容器菜单后切回它 对应原版 inventoryMenu
    public InventoryMenu BackpackMenu { get; private set; } = null!;

    //_containerCounter 容器菜单 id 分配器 原版从 1 起 0 留给背包菜单
    private int _containerCounter;

    //SetUpInventoryMenu 建好背包菜单并置为当前菜单 玩家进世界时调一次
    public void SetUpInventoryMenu()
    {
        BackpackMenu = new InventoryMenu(Inventory) { Synchronizer = new ServerContainerSynchronizer(this) };
        ContainerMenu = BackpackMenu;
    }

    //OpenMenu 打开菜单 先收掉上一个容器菜单 分配 id 下发 open_screen 再同步初始内容
    //对应原版 ServerPlayer.openMenu(MenuProvider)
    public void OpenMenu(MenuProvider provider)
    {
        if (ContainerMenu is { } current && !ReferenceEquals(current, BackpackMenu)) DoCloseContainer();
        var menu = provider.CreateMenu(++_containerCounter, Inventory, this);
        if (menu is null) return;
        menu.Synchronizer = new ServerContainerSynchronizer(this);
        ContainerMenu = menu;
        if (menu.Kind is { } kind)
            Connection.Send(new ClientboundOpenScreenPacket(menu.ContainerId, kind, provider.DisplayName));
        menu.SendAllDataToRemote();
    }

    //CloseContainer 服务端主动关闭容器菜单 先告知客户端再切回背包
    //方块被拆或玩家走远时走它 对应原版 ServerPlayer.closeContainer
    public void CloseContainer()
    {
        if (ContainerMenu is not { } menu || ReferenceEquals(menu, BackpackMenu)) return;
        Connection.Send(new ClientboundContainerClosePacket(menu.ContainerId));
        DoCloseContainer();
    }

    //DoCloseContainer 切回背包菜单并同步背包内容 对应原版 doCloseContainer
    //客户端主动关菜单只走这里 服务端不再回发 container_close 与原版一致
    public void DoCloseContainer()
    {
        if (BackpackMenu is null) return;
        ContainerMenu = BackpackMenu;
        BackpackMenu.SendAllDataToRemote();
    }

    //SendSystemMessage 发系统聊天 默认走聊天栏 对应原版 sendSystemMessage(Component)
    public void SendSystemMessage(Component message) => SendSystemMessage(message, false);

    //SendOverlayMessage 发叠加消息 客户端渲染在动作栏 对应原版 sendOverlayMessage
    //这是动作栏的通用底层入口 title 命令的 actionbar 分支另有专用包 两者客户端表现一致
    public void SendOverlayMessage(Component message) => SendSystemMessage(message, true);

    //SendSystemMessage overlay 为真走动作栏 对应原版 sendSystemMessage(Component,boolean)
    public void SendSystemMessage(Component message, bool overlay)
        => Connection.Send(new ClientboundSystemChatPacket(message, overlay));

    //SendBuildLimitMessage 建筑高度越界提示 对应原版 sendBuildLimitMessage
    //越上界给 build.tooHigh 带 319 越下界给 build.tooLow 带 -64 都是红色动作栏文字
    public void SendBuildLimitMessage(bool isTooHigh, int limit)
        => SendOverlayMessage(Component.Translatable(isTooHigh ? "build.tooHigh" : "build.tooLow", limit)
            .WithStyle(ChatFormatting.Red));

    //Listener 关联的 play 阶段监听器 由 DedicatedServer.TransitionToGame 注入
    //传送要走它的等待客户端确认流程 直接改坐标发包会被在途的旧位置包打回传送前的坐标
    public ServerGamePacketListenerImpl? Listener { get; set; }

    private long _keepAliveSentAt;
    private long _keepAliveId;
    private bool _keepAlivePending;
    //_lastChunkX/_lastChunkZ 上次视野中心区块跨块检测用初始无效值
    private int _lastChunkX = int.MinValue;
    private int _lastChunkZ;

    //--- 药水效果 对应原版 LivingEntity.activeEffects ---

    //_activeEffects 玩家当前生效的效果 按效果值索引
    private readonly Dictionary<NetCraft.Registry.MobEffect, MobEffectInstance> _activeEffects = new();

    //ActiveEffects 当前生效的全部效果实例
    public IReadOnlyCollection<MobEffectInstance> ActiveEffects => _activeEffects.Values;

    //GetEffect 取指定效果的实例 没有返回 null
    public MobEffectInstance? GetEffect(NetCraft.Registry.MobEffect effect)
        => _activeEffects.GetValueOrDefault(effect);

    //AddEffect 施加效果并返回是否生效 已有同类效果时按原版 update 语义取舍 更弱的不覆盖
    //只发给玩家自己 对应原版 ServerPlayer.onEffectUpdated 只同步本连接
    public bool AddEffect(MobEffectInstance instance)
    {
        var effect = instance.Effect.Value;
        if (_activeEffects.TryGetValue(effect, out var existing))
        {
            //等级更低 或等级相同但时长更短 都不覆盖
            if (instance.Amplifier < existing.Amplifier) return false;
            if (instance.Amplifier == existing.Amplifier && instance.Duration < existing.Duration) return false;
        }
        _activeEffects[effect] = instance;
        Connection.Send(ClientboundUpdateMobEffectPacket.Create(EntityId, instance,
            effect is GameMobEffect gameEffect && gameEffect.NeedsBlend));
        return true;
    }

    //RemoveEffect 移除指定效果并通知客户端 返回是否移除
    public bool RemoveEffect(NetCraft.Registry.MobEffect effect)
    {
        if (!_activeEffects.Remove(effect, out var removed)) return false;
        Connection.Send(new ClientboundRemoveMobEffectPacket(EntityId, removed.Effect));
        return true;
    }

    //RemoveAllEffects 移除全部效果并逐个通知客户端 返回是否有移除
    public bool RemoveAllEffects()
    {
        if (_activeEffects.Count == 0) return false;
        foreach (var instance in _activeEffects.Values)
            Connection.Send(new ClientboundRemoveMobEffectPacket(EntityId, instance.Effect));
        _activeEffects.Clear();
        return true;
    }

    //TickEffects 每刻推进效果时长 到期的移除并通知客户端
    public void TickEffects()
    {
        List<MobEffectInstance>? expired = null;
        foreach (var instance in _activeEffects.Values)
        {
            instance.Tick();
            if (instance.Expired) (expired ??= new()).Add(instance);
        }
        if (expired is null) return;
        //遍历中不能改字典 到期项收集完再统一移除
        foreach (var instance in expired)
        {
            _activeEffects.Remove(instance.Effect.Value);
            Connection.Send(new ClientboundRemoveMobEffectPacket(EntityId, instance.Effect));
        }
    }

    public ServerPlayer(GameProfile profile, Connection connection, ServerLevel level)
    {
        EntityId = level.GetNextEntityId();
        Profile = profile;
        Connection = connection;
        Level = level;
        //玩家属性表按实体类型取默认表 与客户端本地那份同源 没登记过就退回空表
        Attributes = new AttributeMap(DefaultAttributes.GetSupplier(EntityTypes.PLAYER) ?? AttributeSupplier.Empty);
        //新玩家满血 对应原版 LivingEntity 构造里的 setHealth(getMaxHealth())
        Health = MaxHealth;
        _keepAliveSentAt = Environment.TickCount64;
        //玩家元数据基线 对应原版 Player 的 defineSynchedData
        SyncedData.Define(NetCraft.Registry.Entity.SharedFlagsIndex, EntityDataSerializers.Byte, (byte)0);
        SyncedData.Define(NetCraft.Registry.Entity.PoseIndex, EntityDataSerializers.Pose, PoseStanding);
    }

    //Tick 每帧调度对应原版 ServerPlayer.tick
    //跨块时更新区块视野 驱动 ChunkSender 渐进发送 维持心跳 并把菜单脏槽同步给客户端
    public void Tick()
    {
        //旁观者每刻跟随相机位置 对应原版 ServerPlayer.tick 开头附近的 camera 分支
        TickCamera();
        //药水效果每刻递减时长 到期自动移除并同步客户端
        TickEffects();
        //无敌帧每刻递减 对应原版 LivingEntity.tick 里的 invulnerableTime--
        if (InvulnerableTime > 0) InvulnerableTime--;
        //位置有变化即视为活跃 挂机踢出计时靠它重置
        if (Position != _lastActivePosition)
        {
            _lastActivePosition = Position;
            LastActiveMillis = Environment.TickCount64;
        }
        //越界伤害 创造与旁观带无敌标志 原版由 isInvulnerableTo 挡掉
        if (GameType != GameType.Creative && GameType != GameType.Spectator) TickWorldBorderDamage();
        UpdateChunkTracking();
        ChunkSender?.Tick();
        TickItemPickup();
        ContainerMenu?.BroadcastChanges();
        //容器菜单失效时自动关闭 方块被拆或玩家走出 8 格 对应原版 ServerPlayer.tick 里的 stillValid 检查
        if (ContainerMenu is { } openMenu && BackpackMenu is not null
            && !ReferenceEquals(openMenu, BackpackMenu) && !openMenu.StillValid(this))
            CloseContainer();
        TickKeepAlive();
    }

    //TickItemPickup 检查脚下与身边的掉落物并尝试拾取
    //原版靠实体移动时的接触检测触发 playerTouch 本作没有实体间碰撞 改成每刻按拾取盒主动查
    private void TickItemPickup()
    {
        if (Level is not PersistentServerLevel level) return;
        var box = PickupBox();
        foreach (var entity in level.EntitiesInBox(box))
        {
            if (entity is not ItemEntity item) continue;
            //空间索引是分桶结果 再按包围盒相交筛一次
            if (!box.Intersects(item.BoundingBox)) continue;
            item.PlayerTouch(this);
        }
    }

    //PickupBox 拾取判定盒 玩家碰撞箱 0.6 宽 1.8 高 再按原版膨胀 1.0/0.5/1.0 格
    private AABB PickupBox()
        => new AABB(Position.X - 0.3, Position.Y, Position.Z - 0.3,
            Position.X + 0.3, Position.Y + 1.8, Position.Z + 0.3).Inflate(1.0, 0.5, 1.0);

    //TickWorldBorderDamage 越出世界边界的持续伤害 对应原版 LivingEntity.baseTick 的边界伤害段
    //免伤缓冲内不受伤 伤害按越界格数乘每格伤害取下限 1
    private void TickWorldBorderDamage()
    {
        var border = Level.WorldBorder;
        var box = new AABB(Position.X - 0.3, Position.Y, Position.Z - 0.3,
            Position.X + 0.3, Position.Y + 1.8, Position.Z + 0.3);
        if (border.IsWithinBounds(box)) return;
        var distance = border.GetDistanceToBorder(Position.X, Position.Z) + border.SafeZone;
        if (distance >= 0.0 || border.DamagePerBlock <= 0.0) return;
        Hurt(Math.Max(1f, MathF.Floor((float)(-distance * border.DamagePerBlock))));
    }

    //UpdateChunkTracking 玩家跨块时更新视野中心对应原版 ChunkMap.move
    //每 tick 比较当前区块与上次记录 变了才通知 ChunkSender
    private void UpdateChunkTracking()
    {
        var chunkX = (int)Math.Floor(Position.X / 16);
        var chunkZ = (int)Math.Floor(Position.Z / 16);
        if (chunkX == _lastChunkX && chunkZ == _lastChunkZ) return;
        _lastChunkX = chunkX;
        _lastChunkZ = chunkZ;
        ChunkSender?.UpdateCenter(chunkX, chunkZ, ViewDistanceChunks);
        //玩家票随视野中心走 视距内出加载票 玩家所在区块出模拟票 对应原版 ChunkMap.move
        if (Level is PersistentServerLevel persistent)
            persistent.ChunkSource.UpdatePlayerTickets(this, chunkX, chunkZ, ViewDistanceChunks);
    }

    //TickKeepAlive 每 15 秒发一次心跳
    //客户端收不到任何包满 30 秒就判读超时断开 区块发完后服务端不再有其它出站包
    //上一轮心跳未获回应说明链路已废直接断开 对齐原版 ServerGamePacketListenerImpl.tick
    private void TickKeepAlive()
    {
        if (!Connection.IsConnected) return;
        var now = Environment.TickCount64;
        if (now - _keepAliveSentAt < KeepAliveIntervalMillis) return;
        if (_keepAlivePending)
        {
            Log.Warning($"Disconnecting due to keep-alive timeout {Profile.Name}");
            Disconnect("disconnect.timeout");
            return;
        }
        _keepAliveSentAt = now;
        _keepAliveId = now;
        _keepAlivePending = true;
        try
        {
            Connection.Send(new ClientboundKeepAlivePacket(_keepAliveId));
        }
        catch (Exception e)
        {
            Log.Warning($"Keep-alive send failed {Profile.Name} {e.Message}");
        }
    }

    //PendingKeepAliveId 尚未回应的心跳 id 无等待时为 null
    //供假客户端模拟真实客户端的回包行为 假连接没有 netty 替它自动回包
    public long? PendingKeepAliveId => _keepAlivePending ? _keepAliveId : null;

    //HandleKeepAliveResponse 客户端回心跳后清除等待标记
    public void HandleKeepAliveResponse(long id)
    {
        //打印匹配结果 心跳超时断开时用于区分回包缺失与 id 不匹配
        Log.Debug($"HandleKeepAliveResponse id={id} expected={_keepAliveId} pending={_keepAlivePending} match={id == _keepAliveId}");
        if (id == _keepAliveId) _keepAlivePending = false;
    }

    //Disconnect 断开玩家连接对齐原版 ServerPlayer.disconnect
    //Disconnect 主动断开玩家 对应原版 ServerGamePacketListenerImpl.disconnect
    //必须先发断连包再关连接 否则客户端只看到连接中断不显示踢出原因
    //原版 stop 走 multiPlayerList.removeAll 每个玩家都发 multiplayer.disconnect.server_shutdown
    public void Disconnect(string reason)
    {
        if (Connection.IsConnected)
            Connection.Send(new ClientboundDisconnectPacket(Component.Literal(reason)));
        Connection.Disconnect(reason);
    }

    //Disconnect 带组件理由的断开 封禁/踢出用翻译键组件 客户端按本地语言显示
    public void Disconnect(Component reason)
    {
        if (Connection.IsConnected)
            Connection.Send(new ClientboundDisconnectPacket(reason));
        Connection.Disconnect("disconnected");
    }
}
