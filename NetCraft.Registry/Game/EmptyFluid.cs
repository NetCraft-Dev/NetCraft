using NetCraft.Registry.State;

namespace NetCraft.Registry;

//EmptyFluid 空流体 对应原版 net.minecraft.world.level.material.EmptyFluid
//原版把它当作注册表里 empty 那一项 所有判定都取"没有流体"这一侧
//退回方块原版指向空气 本作空流体在方块状态里没有对应物 被调用即说明调用方漏判了 IsEmpty
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
        => throw new InvalidOperationException("空流体没有对应的方块状态");

    public override float ExplosionResistance => 0f;
}
