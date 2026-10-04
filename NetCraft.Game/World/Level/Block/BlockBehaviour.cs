using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Network.Component;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Redstone;
using NetCraft.Storage.Updates;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Block;

//BlockBehaviour 方块行为基类对应原版 net.minecraft.world.level.block.state.BlockBehaviour
//继承 Block 抽象基类提供 Properties/StateDefinition 等基础行为契约
//子类按需重写具体行为方法本类只承载状态定义与默认状态
//行为回调默认空实现 由服务端方块更新入口按事件分发 客户端不参与
//当前命名空间段名 Block 与 Registry.Block 类型同名用完全限定名避免歧义
public abstract class BlockBehaviour : NetCraft.Registry.Block, IBlockUpdateBehaviour, IBlockSignalBehaviour,
    IEntityInsideBehaviour
{
    private BlockState? _defaultState;
    private BlockStateDefinition? _stateDefinition;
    private PropertyBase[]? _tableProperties;
    private IReadOnlyDictionary<string, string>? _defaultStateValues;
    private PlacementKind _placement = PlacementKind.None;
    private bool _canOcclude = true;
    private NetCraft.Registry.Enums.PushReaction _pushReaction = NetCraft.Registry.Enums.PushReaction.normal;
    private bool? _redstoneConductor;

    //Properties 方块属性集合 默认取内嵌方块表注入的那份
    //表是从原版提取的 真实类不必再抄一遍属性 抄漏一个状态数就错 全局 id 会跟着错位
    //状态不按常理来的方块仍可重写它自己声明
    public virtual IDictionary<string, PropertyBase> Properties
        => _tableProperties is { Length: > 0 }
            ? _tableProperties.ToDictionary(p => p.Name)
            : new Dictionary<string, PropertyBase>();

    //ApplyTableProperties 由注册流程把内嵌方块表第二列给的属性填进来
    //必须在首次访问 StateDefinition 之前调 状态定义一旦建好属性就固定了
    internal void ApplyTableProperties(PropertyBase[] properties) => _tableProperties = properties;

    //Instrument 该方块作为音符盒底座时给出的乐器 对应原版 Properties 里的 instrument
    //原版注册时逐个指定 本作从原版 Blocks.java 提取成表 由注册流程注入
    //命名空间段 Enums 里也有 Direction 之类同名枚举 这里用完全限定名避免歧义
    public NetCraft.Registry.Enums.NoteBlockInstrument Instrument { get; private set; } =
        NetCraft.Registry.Enums.NoteBlockInstrument.harp;

    internal void ApplyInstrument(NetCraft.Registry.Enums.NoteBlockInstrument instrument)
        => Instrument = instrument;

    //DefaultStateValues 默认状态相对状态定义首项的覆盖 对应原版构造器末尾的 registerDefaultState
    //键是属性名值是序列化名 没列出的属性沿用 any() 顺序取的首值 返回 null 表示默认就是 any()
    public IReadOnlyDictionary<string, string>? DefaultStateValues => _defaultStateValues;

    //ApplyTableDefaults 由注册流程把内嵌方块表第三列填进来
    //必须在首次访问 DefaultBlockState 之前调 否则状态已经构建无法回改
    internal void ApplyTableDefaults(IReadOnlyDictionary<string, string>? defaults)
        => _defaultStateValues = defaults;

    //ApplyTablePlacement 由注册流程把内嵌方块表 place= 段给的放置类别填进来
    internal void ApplyTablePlacement(PlacementKind placement) => _placement = placement;

    //ApplyTableOcclusion 由注册流程把内嵌方块表 occlude= 段填进来
    internal void ApplyTableOcclusion(bool canOcclude) => _canOcclude = canOcclude;

    //ApplyTablePushReaction 由注册流程把内嵌方块表 push= 段填进来
    internal void ApplyTablePushReaction(NetCraft.Registry.Enums.PushReaction reaction)
        => _pushReaction = reaction;

    //ApplyTableRedstoneConductor 由注册流程把内嵌方块表 conductor= 段填进来
    internal void ApplyTableRedstoneConductor(bool? conductor) => _redstoneConductor = conductor;

    //PushReaction 被活塞推动时的反应 表里没写的走基类的 normal
    public override NetCraft.Registry.Enums.PushReaction PushReaction => _pushReaction;

    //CanOcclude 是否遮挡光线 对应原版 Properties.noOcclusion
    //关掉的方块遮挡形状按空处理 减光随之落到 0 或 1 而不是整格 15
    public override bool CanOcclude => _canOcclude;

    //StateDefinition 方块状态定义延迟构建首次访问时构建所有可能状态
    public BlockStateDefinition StateDefinition
        => _stateDefinition ??= BuildStateDefinition();

    //DefaultBlockState 方块的默认状态取 StateDefinition 第一个状态
    public override BlockState DefaultBlockState
        => _defaultState ??= CreateDefaultState();

    //CreateDefaultState 默认状态取 any() 再按 DefaultStateValues 逐项覆盖
    //原版绝大多数方块的 registerDefaultState 就是 any() 但仍有 647 个方块显式改了
    //BooleanProperty 值序是 true,false 枚举取声明序 不改就会落在活塞 extended=true 这种错值上
    protected virtual BlockState CreateDefaultState()
    {
        var state = StateDefinition.PossibleStates[0];
        if (DefaultStateValues is not { Count: > 0 } overrides) return state;
        foreach (var (name, valueName) in overrides)
        {
            if (StateDefinition.GetProperty(name) is not { } property) continue;
            if (property.GetValueForName(valueName) is not { } value) continue;
            state = state.SetValue(property, value);
        }
        return state;
    }

    //AllStates 全部可能状态由状态定义展开
    public override IReadOnlyList<BlockState> AllStates => StateDefinition.PossibleStates;

    //OnPlace 方块被放置到位后回调 对应原版 onPlace 默认无行为
    public virtual void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
        bool movedByPiston) { }

    //PlayerDestroy 玩家破坏方块后回调 默认无行为
    public virtual void PlayerDestroy(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state) { }

    //PlayerWillDestroy 玩家破坏方块前回调 对应原版 playerWillDestroy
    //在掉落与置空之前调用 多格方块靠它先把另一半无掉落解掉
    //不这么做的话创造模式拆门或活塞头 另一半会跟着掉出物品 原版这条路径受 preventsBlockDrops 管着
    public virtual void PlayerWillDestroy(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state) { }

    //IsInTag 本方块是否属于某个方块标签 对应原版 BlockState.is(TagKey)
    //植被与作物的"能种在什么上面"全靠这条 标签数据由 TagsReloadListener 在启动时绑好
    //标签没绑定时按不属于处理 与原版空集语义一致
    public bool IsInTag(NetCraft.Registry.TagKey<NetCraft.Registry.Block> tag)
    {
        var self = NetCraft.Registry.BuiltInRegistries.BLOCK.Get(Id);
        return self is not null
            && NetCraft.Registry.BuiltInRegistries.BLOCK.Get(tag)?.Contains(self) == true;
    }

    //OnAttack 玩家开始挖该方块时回调 对应原版 Block.attack
    //音符盒靠它左键试听 放到破坏进度之前与原版一致
    public virtual void OnAttack(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state) { }

    //SetPlacedBy 方块被玩家放下后回调 对应原版 setPlacedBy 默认无行为
    //中继器要靠它排首刻 落位时输入端已经有信号的话不能等邻居变化
    public virtual void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player) { }

    //GetDrops 破坏时的掉落物 默认掉自身对应的方块物品 对应原版无特殊掉落表的方块
    //草方块掉泥土 石头掉圆石一类差异后续按方块重写本方法
    public virtual IEnumerable<ItemStack> GetDrops(ServerLevel level, ServerPlayer? player, BlockPos pos, BlockState state)
    {
        //命名空间段 Items 与注册表类同名 必须完全限定否则解析到命名空间
        var item = NetCraft.Game.World.Items.Items.ItemForBlock(this);
        if (item is null || ReferenceEquals(item, NetCraft.Game.World.Items.Items.AIR))
            return Array.Empty<ItemStack>();
        return new[] { new ItemStack(item.BuiltInRegistryHolder, 1, DataComponentPatch.Empty) };
    }

    //UseOn 玩家对指定面使用该方块 返回是否消耗本次交互 默认未处理
    public virtual bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        => false;

    //CanBeReplaced 该位置方块能否被手持方块放置直接替换 对应原版 canBeReplaced
    //空气与水等可覆盖方块返回 true
    public virtual bool CanBeReplaced => false;

    //GetStateForPlacement 放置时按上下文算落位状态 返回 null 表示这个面放不下
    //face 是玩家点到的那一面 horizontalFacing 是玩家水平朝向 对应原版 getStateForPlacement
    public virtual BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
        Direction horizontalFacing) => PlacementState(face, horizontalFacing, horizontalFacing);

    //GetStateForPlacement 带玩家视线六向的版本 观察者这类朝向含上下的方块覆写它
    //directional/looking 两类表驱动朝向要靠视线方向定 只有它们留在这一层处理
    //其余一律下放给四参版本 拉杆活板门那类覆写的都是四参 不下放它们的实现永远不会被调到
    public virtual BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
        Direction horizontalFacing, Direction lookingDirection)
        => _placement is PlacementKind.SixFacing or PlacementKind.LookingFacing
            ? PlacementState(face, horizontalFacing, lookingDirection)
            : GetStateForPlacement(level, pos, face, horizontalFacing);

    //GetStateForPlacement 带命中点的版本 活板门按命中高度定上下半 门按命中水平位置定合页侧
    //hitLocal 是命中点在方块内的坐标 三个分量都在 0 到 1 之间 对应原版 BlockPlaceContext.getClickLocation
    //基类默认转发到五参版本 现有方块不用动
    public virtual BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
        Direction horizontalFacing, Direction lookingDirection, Vec3 hitLocal)
        => GetStateForPlacement(level, pos, face, horizontalFacing, lookingDirection);

    //PlacementState 按内嵌方块表给的类别套原版三种通用朝向
    //26.2 里朝向不再由父类统一给 每个方块类自己实现 只有这三种模式能原样照搬
    //其余方块各自覆写本方法 类别为 None 的退回默认状态
    private BlockState PlacementState(Direction face, Direction horizontalFacing, Direction lookingDirection)
        => _placement switch
        {
            PlacementKind.HorizontalFacing => DefaultBlockState.SetValue(BlockStateProperties.HorizontalFacing,
                ToPropertyDirection(horizontalFacing.Opposite)),
            PlacementKind.SixFacing => DefaultBlockState.SetValue(BlockStateProperties.FacingProperty,
                ToPropertyDirection(lookingDirection.Opposite)),
            //观察者写的是两次取反 等价于直接用视线方向
            PlacementKind.LookingFacing => DefaultBlockState.SetValue(BlockStateProperties.FacingProperty,
                ToPropertyDirection(lookingDirection)),
            PlacementKind.PillarAxis => DefaultBlockState.SetValue(BlockStateProperties.AxisProperty,
                ToPropertyAxis(face.AxisValue)),
            _ => DefaultBlockState,
        };

    //ToPropertyDirection 把原版 Direction 折算成方块属性用的枚举成员
    //属性枚举的成员名是序列化名 与 Primitives 的 Id3D 恰好同序但不做数值假设
    private static NetCraft.Registry.Enums.Direction ToPropertyDirection(Direction direction)
        => direction.Id3D switch
        {
            Direction.DownId => NetCraft.Registry.Enums.Direction.down,
            Direction.UpId => NetCraft.Registry.Enums.Direction.up,
            Direction.NorthId => NetCraft.Registry.Enums.Direction.north,
            Direction.SouthId => NetCraft.Registry.Enums.Direction.south,
            Direction.WestId => NetCraft.Registry.Enums.Direction.west,
            _ => NetCraft.Registry.Enums.Direction.east,
        };

    private static NetCraft.Registry.Enums.Axis ToPropertyAxis(Direction.Axis axis) => axis switch
    {
        Direction.Axis.X => NetCraft.Registry.Enums.Axis.x,
        Direction.Axis.Y => NetCraft.Registry.Enums.Axis.y,
        _ => NetCraft.Registry.Enums.Axis.z,
    };

    //CanSurvive 该状态能否留在当前位置 形状更新时判定 对应原版 canSurvive 默认恒可存活
    public virtual bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state) => true;

    //IsFaceSturdy 某面能否作为依附面 火把拉杆这类贴附方块靠它判定 对应原版 isFaceSturdy
    //必须用调用方给的真实世界: 移动活塞的形状由方块实体提供 栅栏围墙要读邻居区块
    //换成空世界视图会把这些方块判成没有形状 活塞收回当刻上方的附着物就会被判掉
    //持久化关卡实现了 BlockGetter 直接用它 内存关卡这类给不出形状的退回空视图
    public virtual bool IsFaceSturdy(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        => IsFaceSturdy(level as BlockGetter ?? EmptyBlockGetter.Instance, pos, state, direction, SupportType.Full);

    public virtual bool IsFaceSturdy(BlockGetter level, BlockPos pos, BlockState state, Direction direction,
        SupportType supportType)
        => supportType.IsSupporting(state, level, pos, direction);

    //HasCollision 是否参与碰撞 空气与流体一类要关掉 对应原版 Properties.hasCollision
    public virtual bool HasCollision => true;

    //GetShape 视觉形状默认整块 对应原版 getShape
    public virtual VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
        => Shapes.Block();

    //GetCollisionShape 碰撞形状默认取视觉形状 对应原版 getCollisionShape
    public virtual VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => HasCollision ? state.GetShape(level, pos) : Shapes.Empty();

    //GetInteractionShape 准星射线命中盒默认空 对应原版 getInteractionShape
    public virtual VoxelShape GetInteractionShape(BlockState state, BlockGetter level, BlockPos pos)
        => Shapes.Empty();

    //GetBlockSupportShape 依附判定用形状默认取碰撞形状 对应原版 getBlockSupportShape
    public virtual VoxelShape GetBlockSupportShape(BlockState state, BlockGetter level, BlockPos pos)
        => GetCollisionShape(state, level, pos, CollisionContext.Empty);

    //GetOcclusionShape 遮挡形状默认取视觉形状 对应原版 getOcclusionShape
    public virtual VoxelShape GetOcclusionShape(BlockState state, BlockGetter level, BlockPos pos)
        => state.GetShape(level, pos);

    //GetOcclusionShape 光照判定只用得到形状本身 按原版取空世界视图下的视觉形状
    public override VoxelShape GetOcclusionShape(BlockState state)
        => state.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero);

    //PropagatesSkylightDown 天光能否垂直穿过该状态 对应原版 propagatesSkylightDown
    //原版默认看视觉形状是否占满整格 并且该处没有流体
    public override bool PropagatesSkylightDown(BlockState state)
        => !IsShapeFullBlock(state.GetShape(EmptyBlockGetter.Instance, BlockPos.Zero)) && state.FluidState.IsEmpty;

    //GetVisualShape 视觉形状默认取碰撞形状 对应原版 getVisualShape
    public virtual VoxelShape GetVisualShape(BlockState state, BlockGetter level, BlockPos pos,
        CollisionContext context)
        => GetCollisionShape(state, level, pos, context);

    //IsCollisionShapeFullBlock 碰撞形状是否占满整格 对应原版 isCollisionShapeFullBlock
    public virtual bool IsCollisionShapeFullBlock(BlockState state, BlockGetter level, BlockPos pos)
        => NetCraft.Registry.Block.IsShapeFullBlock(state.GetCollisionShape(level, pos));

    //IsAir 是否为空气方块 对应原版 BlockState.isAir 地表规则靠它判断空列
    public override bool IsAir => false;

    //HasFluidState 是否带流体 对应原版 getFluidState().isEmpty() 取反 水/岩浆返回 true
    public virtual bool HasFluidState => false;

    //NeighborChanged 邻接位置方块变化后回调 供红石与支撑类方块响应 对应原版 neighborChanged 默认无行为
    //changedBlock 是发生变化的那一方块 不是本方块
    public virtual void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
        NetCraft.Registry.Block changedBlock, bool movedByPiston) { }

    //UpdateShape 邻接方块形状变化后重算自身 对应原版 updateShape 默认返回原状态
    //directionToNeighbour 是从本方块指向邻接的方向
    public virtual BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
        Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState) => state;

    //UpdateIndirectNeighbourShapes 间接形状更新 对应原版 updateIndirectNeighbourShapes 默认无行为
    public virtual void UpdateIndirectNeighbourShapes(ServerLevel level, BlockPos pos, BlockState state,
        int updateFlags, int updateLimit) { }

    //AffectNeighborsAfterRemoval 本方块被移除后对邻接的额外影响 对应原版同名方法 默认无行为
    public virtual void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
        bool movedByPiston) { }

    //Destroy 玩家把本方块破坏掉后的收尾回调 对应原版 Block.destroy 默认无行为
    //原版在方块已置空之后才调 且只有真的换掉了才调
    public virtual void Destroy(ServerLevel level, BlockPos pos, BlockState state) { }

    //Tick 调度刻回调 对应原版 Block.tick 默认无行为
    public virtual void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random) { }

    //OnEntityInside 有实体进入本方块所在格 对应原版 entityInside 默认无行为
    public virtual void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state) { }

    //OnProjectileHit 被投射物命中时的回调 对应原版 onProjectileHit 默认无行为
    //hit 带着命中点与进入面 标靶这类方块靠命中点算输出强度
    public virtual void OnProjectileHit(ServerLevel level, BlockState state, BlockHitResult hit,
        NetCraft.Game.World.Entity.Projectile projectile) { }

    //TriggerEvent 方块事件回调 对应原版 Block.triggerEvent 默认未处理
    public virtual bool TriggerEvent(ServerLevel level, BlockPos pos, BlockState state, int paramA, int paramB)
        => false;

    //IsSignalSource 是否红石信号源 对应原版 isSignalSource 默认不是
    public virtual bool IsSignalSource => false;

    //IsDiode 是否二极管也就是中继器与比较器 对应原版 DiodeBlock.isDiode
    public virtual bool IsDiode => false;

    //CreateBlockEntity 方块被放下时创建方块实体 没有方块实体的返回 null 对应原版 EntityBlock.newBlockEntity
    public virtual BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => null;

    //HasBlockEntity 本方块是否带方块实体 对应原版 BlockState.hasBlockEntity
    //活塞判定可推性要用它 方块实体方块原版一律不可推
    public virtual bool HasBlockEntity => false;

    //OwnSignal 方块自身的信号强度 对应原版 ownSignal 默认 0
    public virtual int OwnSignal(ServerLevel level, BlockPos pos, BlockState state) => 0;

    //GetSignal 对指定方向输出的信号强度 默认取自身强度 对应原版 getSignal
    //direction 是从接收者指向本方块的方向
    public virtual int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        => OwnSignal(level, pos, state);

    //GetDirectSignal 直接信号 导体方块传导的就是它 对应原版 getDirectSignal 默认 0
    public virtual int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction) => 0;

    //IsRedstoneConductor 是否红石导体 对应原版 isRedstoneConductor
    //原版默认按碰撞形状满不满格算 玻璃 树叶 观察者 TNT 这类形状满格却显式设成 never；灵魂沙 泥巴形状不满格却设成 always
    //这两类特例由内嵌方块表的 conductor= 段给出 没写的就是默认的形状判定
    public virtual bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state)
        => _redstoneConductor ?? IsCollisionShapeFullBlock(state, EmptyBlockGetter.Instance, pos);

    //HasAnalogOutputSignal 是否有模拟输出 比较器靠它决定读不读 对应原版 hasAnalogOutputSignal
    public virtual bool HasAnalogOutputSignal => false;

    //GetAnalogOutputSignal 模拟输出强度 对应原版 getAnalogOutputSignal 默认 0
    public virtual int GetAnalogOutputSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        => 0;

    //DestroySpeed 方块硬度 挖掘进度的分母 负值表示不可破坏 对应原版 destroyTime
    public virtual float DestroySpeed => 1f;

    //RequiresCorrectToolForDrops 是否需要对应工具才能正常挖掘 对应原版 requiresCorrectToolForDrops
    //本作没有工具系统 需要的方块一律按空手处理 挖掘耗时是能挖的方块的数倍
    public virtual bool RequiresCorrectToolForDrops => false;

    //GetDestroyProgress 每刻推进的破坏进度 对应原版 BlockBehaviour.getDestroyProgress
    //进度按"玩家挖掘速度 / 硬度 / 工具除数"线性增长 累计到 1 即破坏
    //空手挖掘速度取 1 需要工具的方块除 100 其余除 30 不可破坏的方块恒为 0
    public static float GetDestroyProgress(BlockState state)
        => state.Owner is not BlockBehaviour behaviour || behaviour.DestroySpeed < 0
            ? 0f
            : 1f / behaviour.DestroySpeed / (behaviour.RequiresCorrectToolForDrops ? 100f : 30f);

    //RandomTicks 是否参与随机刻 对应原版 Properties.randomTicks 默认关闭
    public override bool RandomTicks => false;

    //RandomTick 随机刻回调 只对 RandomTicks 为 true 的方块按 randomTickSpeed 抽样调用
    public virtual void RandomTick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random) { }

    //BuildStateDefinition 用 Properties 构建状态定义
    private BlockStateDefinition BuildStateDefinition()
        => new(this, Properties);
}

//PlacementKind 放置朝向的通用实现类别 对应原版各方块 getStateForPlacement 里的朝向表达式
//只归纳出原版能原样照搬的三种 其余方块带邻居判定或双层判定 要按方块单独移植
public enum PlacementKind
{
    None,

    //facing 取玩家水平朝向的反向 对应 context.getHorizontalDirection().getOpposite()
    HorizontalFacing,

    //facing 取玩家视线最近方向的反向 对应 context.getNearestLookingDirection().getOpposite()
    SixFacing,

    //facing 直接取玩家视线最近方向 原版观察者写成两次取反 对应 getNearestLookingDirection().getOpposite().getOpposite()
    LookingFacing,

    //axis 取点击面的轴 对应 context.getClickedFace().getAxis()
    PillarAxis,
}

internal static class PlacementKindExtensions
{
    //ParsePlacementKind 解析内嵌方块表 place= 段给的类别名
    public static PlacementKind ParsePlacementKind(string? text) => text switch
    {
        "horizontal" => PlacementKind.HorizontalFacing,
        "directional" => PlacementKind.SixFacing,
        "looking" => PlacementKind.LookingFacing,
        "pillar" => PlacementKind.PillarAxis,
        _ => PlacementKind.None,
    };
}
