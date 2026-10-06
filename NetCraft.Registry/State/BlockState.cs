namespace NetCraft.Registry.State;

//BlockState readonly struct, maps to vanilla net.minecraft.world.level.block.state.BlockState
//The struct optimization corresponds to vanilla optimization 2.5, aligning with FerriteCore FastMap
//Holds only an int Id and looks up data in BlockStateRegistry, avoiding a 10000+ instances each holding arrays
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

    //GetLightEmission the block's own light emission for the lighting engine; it is per-state for blocks like redstone lamps that only emit light when lit
    public int GetLightEmission() => Owner.GetLightEmission(this);

    //GetLightDampening the light attenuation of this state, computed per state rather than per block
    //Vanilla occlusion shapes change with state, so the lower slab and the full double slab attenuate differently
    public int GetLightDampening() => Owner.GetLightDampening(this);

    //FluidState the fluid state for this state; non-fluid blocks return empty, maps to vanilla getFluidState
    public FluidState FluidState => Owner.GetFluidState(this);

    public bool Equals(BlockState other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is BlockState s && Equals(s);
    public override int GetHashCode() => Id;
    public static bool operator ==(BlockState a, BlockState b) => a.Id == b.Id;
    public static bool operator !=(BlockState a, BlockState b) => a.Id != b.Id;

    public override string ToString() => BlockStateRegistry.ToString(Id);
}
