using NetCraft.Registry.State;

namespace NetCraft.Registry;

//Fluid fluid abstraction, maps to vanilla net.minecraft.world.level.material.Fluid
//Only the part needed for server-side checks is kept; vanilla's flow and spread on Fluid need a level, so that part is split into Storage's IFluidBehaviour
//Members depending on business subsystems, such as entity interaction, particles and pickup sounds, are not declared yet and will be added once those subsystems are ready
public abstract class Fluid
{
    //Empty empty fluid singleton, maps to vanilla Fluids.EMPTY
    //The Game layer registry uses it to register the empty entry, so FluidState.Empty.Type is the same instance as the registry default
    public static readonly Fluid Empty = new EmptyFluid();

    protected Fluid()
    {
        DefaultFluidState = GetStateOf(GetDefaultAmount(), false);
    }

    //_stateCache state instance table for each fluid level and falling flag
    //Vanilla relies on state table singletons so reference comparison equals value comparison; that is what spread uses to tell whether the state changed
    private readonly FluidState[,] _stateCache = new FluidState[9, 2];

    //GetStateOf gets the state for the given fluid level and falling flag; identical arguments return the same instance
    public FluidState GetStateOf(int amount, bool falling)
    {
        var cached = _stateCache[amount, falling ? 1 : 0];
        if (cached is not null) return cached;
        var state = new FluidState(this, amount, falling);
        _stateCache[amount, falling ? 1 : 0] = state;
        return state;
    }

    //Id the fluid's registry name, must be implemented by subclasses
    public abstract Identifier Id { get; }

    //DefaultFluidState the fluid's default state, maps to vanilla defaultFluidState
    public FluidState DefaultFluidState { get; }

    //GetDefaultAmount the default state's fluid level; sources and empty fluid are 8 and 0 and flowing fluid takes the lowest level 1
    protected virtual int GetDefaultAmount() => 8;

    //IsEmpty whether it is empty fluid, maps to vanilla isEmpty
    public virtual bool IsEmpty => false;

    //IsSame whether it is the same kind as another fluid, maps to vanilla isSame; water and flowing water count as the same kind
    public virtual bool IsSame(Fluid other) => ReferenceEquals(other, this);

    //IsSource whether the state is an infinite source, maps to vanilla isSource
    public abstract bool IsSource(FluidState state);

    //GetAmount the state's fluid level 1-8, maps to vanilla getAmount
    public abstract int GetAmount(FluidState state);

    //GetOwnHeight the ratio of the state's own fluid surface, maps to vanilla getOwnHeight
    public virtual float GetOwnHeight(FluidState state) => GetAmount(state) / 9f;

    //CreateLegacyBlock the state when falling back to a block, maps to vanilla createLegacyBlock
    public abstract BlockState CreateLegacyBlock(FluidState state);

    //ExplosionResistance the fluid's own explosion resistance, maps to vanilla getExplosionResistance
    public abstract float ExplosionResistance { get; }
}
