namespace NetCraft.Registry.State;

//BlockState readonly struct 对应原版 net.minecraft.world.level.block.state.BlockState
//struct 化优化对应原版优化点2.5对齐 FerriteCore FastMap
//内部只持 int Id 数据查 BlockStateRegistry 避免 10000+ 实例每实例持数组
public readonly struct BlockState : IEquatable<BlockState>
{
    public int Id { get; }

    internal BlockState(int id) => Id = id;

    public Block Owner => BlockStateRegistry.Owner(Id);
    public IReadOnlyCollection<PropertyBase> GetProperties() => BlockStateRegistry.GetProperties(Id);
    public bool IsSingletonState => BlockStateRegistry.IsSingletonState(Id);
    public bool HasProperty(PropertyBase property) => BlockStateRegistry.HasProperty(Id, property);

    public T GetValue<T>(Property<T> property) where T : IComparable
        => BlockStateRegistry.GetValue(Id, property);

    public T? GetOptionalValue<T>(Property<T> property) where T : IComparable
        => BlockStateRegistry.GetOptionalValue(Id, property);

    public T GetValueOrElse<T>(Property<T> property, T defaultValue) where T : IComparable
        => BlockStateRegistry.GetValueOrElse(Id, property, defaultValue);

    public BlockState SetValue<T>(Property<T> property, T value) where T : IComparable
        => BlockStateRegistry.SetValue(Id, property, value);

    public BlockState TrySetValue<T>(Property<T> property, T value) where T : IComparable
        => BlockStateRegistry.TrySetValue(Id, property, value);

    public BlockState SetValue(PropertyBase property, object value)
        => BlockStateRegistry.SetValue(Id, property, value);

    public BlockState Cycle<T>(Property<T> property) where T : IComparable
        => BlockStateRegistry.Cycle(Id, property);

    public IEnumerable<PropertyValue> GetValues() => BlockStateRegistry.GetValues(Id);

    //GetLightEmission 方块自身发光等级供光照引擎读取 按状态算是为了红石灯这类点亮才发光的方块
    public int GetLightEmission() => Owner.GetLightEmission(this);

    //GetLightDampening 该状态对光的衰减 按状态算而不是按方块算
    //原版遮挡形状随状态变化 台阶下半砖与整块双层砖的衰减不同
    public int GetLightDampening() => Owner.GetLightDampening(this);

    //FluidState 该状态对应的流体状态 非流体方块返回空对应原版 getFluidState
    public FluidState FluidState => Owner.GetFluidState(this);

    public bool Equals(BlockState other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is BlockState s && Equals(s);
    public override int GetHashCode() => Id;
    public static bool operator ==(BlockState a, BlockState b) => a.Id == b.Id;
    public static bool operator !=(BlockState a, BlockState b) => a.Id != b.Id;

    public override string ToString() => BlockStateRegistry.ToString(Id);
}
