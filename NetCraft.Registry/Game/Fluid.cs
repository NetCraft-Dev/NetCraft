using NetCraft.Registry.State;

namespace NetCraft.Registry;

//Fluid 流体抽象 对应原版 net.minecraft.world.level.material.Fluid
//只保留服务端判定需要的那部分 原版挂在 Fluid 上的流动与扩散需要关卡 那部分拆到 Storage 的 IFluidBehaviour
//实体交互 粒子 拾取音效等依赖业务子系统的成员暂不声明 等对应子系统就绪再补
public abstract class Fluid
{
    //Empty 空流体单例 对应原版 Fluids.EMPTY
    //Game 层注册表用它登记 empty 这一项 使 FluidState.Empty.Type 与注册表默认项是同一个实例
    public static readonly Fluid Empty = new EmptyFluid();

    protected Fluid()
    {
        DefaultFluidState = GetStateOf(GetDefaultAmount(), false);
    }

    //_stateCache 该流体各液面高度与下落标记下的状态实例表
    //原版靠状态表单例保证引用比较等价于值比较 扩散时判断"状态变没变"靠的就是这个
    private readonly FluidState[,] _stateCache = new FluidState[9, 2];

    //GetStateOf 取指定液面高度与下落标记下的状态 同参数返回同一实例
    public FluidState GetStateOf(int amount, bool falling)
    {
        var cached = _stateCache[amount, falling ? 1 : 0];
        if (cached is not null) return cached;
        var state = new FluidState(this, amount, falling);
        _stateCache[amount, falling ? 1 : 0] = state;
        return state;
    }

    //Id 流体的注册名 子类必须实现
    public abstract Identifier Id { get; }

    //DefaultFluidState 该流体的默认状态 对应原版 defaultFluidState
    public FluidState DefaultFluidState { get; }

    //GetDefaultAmount 默认状态的液面高度 源与空流体分别为 8 与 0 流动流体取最低档 1
    protected virtual int GetDefaultAmount() => 8;

    //IsEmpty 是不是空流体 对应原版 isEmpty
    public virtual bool IsEmpty => false;

    //IsSame 与另一种流体是否同族 对应原版 isSame 水与流动水算同族
    public virtual bool IsSame(Fluid other) => ReferenceEquals(other, this);

    //IsSource 该状态是不是无限源 对应原版 isSource
    public abstract bool IsSource(FluidState state);

    //GetAmount 该状态的液面高度 1-8 对应原版 getAmount
    public abstract int GetAmount(FluidState state);

    //GetOwnHeight 该状态自身的液面高度比例 对应原版 getOwnHeight
    public virtual float GetOwnHeight(FluidState state) => GetAmount(state) / 9f;

    //CreateLegacyBlock 退回成方块时的状态 对应原版 createLegacyBlock
    public abstract BlockState CreateLegacyBlock(FluidState state);

    //ExplosionResistance 流体自身的爆炸抗性 对应原版 getExplosionResistance
    public abstract float ExplosionResistance { get; }
}
