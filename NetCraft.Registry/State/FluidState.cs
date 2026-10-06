namespace NetCraft.Registry.State;

//FluidState fluid state, maps to vanilla net.minecraft.world.level.material.FluidState
//Vanilla extends StateHolder with LEVEL and FALLING properties; here it directly holds the level height and falling flag fields
//Fluid state has only these two dimensions and needs no neighbor table; the simplification does not change behavior
public sealed class FluidState
{
    //Empty empty fluid state, maps to vanilla Fluids.EMPTY.defaultFluidState
    //Goes through Fluid.Empty's state table, guaranteeing it is the same instance as the empty entry in the registry
    public static FluidState Empty => Fluid.Empty.DefaultFluidState;

    public FluidState(Fluid type, int amount, bool falling)
    {
        Type = type;
        Amount = amount;
        Falling = falling;
    }

    //Type which fluid this state belongs to
    public Fluid Type { get; }

    //Amount fluid level 0-8, with a source at 8, maps to vanilla LEVEL
    public int Amount { get; }

    //Falling whether it is falling; water poured straight from above has a higher surface, maps to vanilla FALLING
    public bool Falling { get; }

    //IsEmpty this cell has no fluid
    public bool IsEmpty => Type.IsEmpty;

    //IsSource this cell is an infinite source, maps to vanilla isSource
    public bool IsSource => Type.IsSource(this);

    //IsFull the fluid level is full, maps to vanilla isFull
    public bool IsFull => Amount >= 8;

    //IsWater whether this fluid is water, maps to vanilla fluidState.is(FluidTags.WATER)
    //The fluid tag system is not wired up yet, so it checks registry names; water has only the names water and flowing_water
    public bool IsWater => Type.Id.Path is "water" or "flowing_water";

    //OwnHeight the ratio of its own fluid surface, ignoring stacked fluid of the same kind above, maps to vanilla getOwnHeight
    public float OwnHeight => Type.GetOwnHeight(this);

    //CreateLegacyBlock falls back to a block state, maps to vanilla createLegacyBlock
    public BlockState CreateLegacyBlock() => Type.CreateLegacyBlock(this);

    //SetAmount copies a state with a different fluid level; fluid spreading is stepwise level reduction
    public FluidState SetAmount(int amount) => new(Type, amount, Falling);

    //SetFalling copies a state with the falling flag
    public FluidState SetFalling(bool falling) => new(Type, Amount, falling);

    public override string ToString() => IsEmpty ? "empty" : $"{Type.Id}[level={Amount},falling={Falling}]";
}
