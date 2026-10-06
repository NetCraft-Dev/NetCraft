using NetCraft.Registry.State;

namespace NetCraft.Registry;

//EmptyFluid empty fluid, maps to vanilla net.minecraft.world.level.material.EmptyFluid
//Vanilla treats it as the empty entry in the registry and every check takes the "no fluid" side
//Vanilla's legacy block points to air; empty fluid has no counterpart in block states here, so being called means the caller missed an IsEmpty check
public sealed class EmptyFluid : Fluid
{
    public override Identifier Id => Identifier.WithDefaultNamespace("empty");

    protected override int GetDefaultAmount() => 0;

    public override bool IsEmpty => true;

    public override bool IsSame(Fluid other) => ReferenceEquals(other, this);

    public override bool IsSource(FluidState state) => false;

    public override int GetAmount(FluidState state) => 0;

    public override float GetOwnHeight(FluidState state) => 0f;

    public override BlockState CreateLegacyBlock(FluidState state)
        => throw new InvalidOperationException("Empty fluid has no corresponding block state");

    public override float ExplosionResistance => 0f;
}
