using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Material;

//WaterFluid water, maps to vanilla net.minecraft.world.level.material.WaterFluid
//Drop-off 1, backward slope search 4 layers, ticks every 5; source and flowing water are the same family and can displace each other
public abstract class WaterFluid : FlowingFluid
{
    public override Fluid FlowingType => Fluids.FlowingWater;

    public override Fluid SourceType => Fluids.Water;

    //IsSame source and flowing water are the same family, maps to vanilla isSame
    public override bool IsSame(Fluid other)
        => ReferenceEquals(other, Fluids.Water) || ReferenceEquals(other, Fluids.FlowingWater);

    public override int GetDropOff(ServerLevel level) => 1;

    public override int GetSlopeFindDistance(ServerLevel level) => 4;

    public override int GetTickDelay(ServerLevel level) => 5;

    public override float ExplosionResistance => 100f;

    //GetHeight counts as full when same-family fluid presses from above, maps to vanilla getHeight
    public override float GetHeight(FluidState state, ServerLevel level, BlockPos pos)
        => HasSameAbove(state, level, pos) ? 1f : state.OwnHeight;

    //GetFlow flow direction only feeds client fluid render and entity push, neither is wired up here
    public override Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state) => Vec3.Zero;

    //CanBeReplacedWith flows down and displaces anything below that is not water, maps to vanilla canBeReplacedWith
    public override bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction)
        => direction == Direction.Down && other.Id.Path is not ("water" or "flowing_water");

    //CreateLegacyBlock writes the water block back with the surface level, maps to vanilla createLegacyBlock
    public override BlockState CreateLegacyBlock(FluidState state)
        => Blocks.WATER.DefaultBlockState.SetValue(BlockStateProperties.Level15, GetLegacyLevel(state));

    //CanConvertToSource forms a new source in place when two or more adjacent sources exist; vanilla gates this on the water_source_conversion game rule
    //The game rule read path is not connected to the save layer yet so this takes vanilla's default of true
    protected override bool CanConvertToSource(ServerLevel level) => true;

    //Source water source, maps to vanilla WaterFluid.Source
    public sealed class Source : WaterFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("water");

        public override bool IsSource(FluidState state) => true;

        public override int GetAmount(FluidState state) => 8;
    }

    //Flowing flowing water, maps to vanilla WaterFluid.Flowing
    public sealed class Flowing : WaterFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("flowing_water");

        //Surface height comes from the state itself; the default state takes the lowest level 1, aligning with the first entry of vanilla's state table
        protected override int GetDefaultAmount() => 1;

        public override bool IsSource(FluidState state) => false;

        public override int GetAmount(FluidState state) => state.Amount;
    }
}
