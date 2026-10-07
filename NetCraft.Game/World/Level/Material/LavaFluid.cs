using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Material;

//LavaFluid lava, maps to vanilla net.minecraft.world.level.material.LavaFluid
//Drop-off 2, backward slope search only 2 layers, ticks every 30, flowing down into water turns that cell to stone
//Vanilla also has the fast lava dimension property; the dimension property system is not wired up here so it is always normal lava
public abstract class LavaFluid : FlowingFluid
{
    public const int LightEmission = 15;

    public override Fluid FlowingType => Fluids.FlowingLava;

    public override Fluid SourceType => Fluids.Lava;

    public override bool IsSame(Fluid other)
        => ReferenceEquals(other, Fluids.Lava) || ReferenceEquals(other, Fluids.FlowingLava);

    public override int GetDropOff(ServerLevel level) => 2;

    public override int GetSlopeFindDistance(ServerLevel level) => 2;

    public override int GetTickDelay(ServerLevel level) => 30;

    public override float ExplosionResistance => 100f;

    public override bool IsRandomlyTicking => true;

    //RandomTick vanilla ignites nearby flammables here, relying on the fire block and flammability checks; both subsystems are missing so it is left empty
    public override void RandomTick(ServerLevel level, BlockPos pos, FluidState fluidState, RandomSource random) { }

    public override float GetHeight(FluidState state, ServerLevel level, BlockPos pos)
        => HasSameAbove(state, level, pos) ? 1f : state.OwnHeight;

    public override Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state) => Vec3.Zero;

    //CanBeReplacedWith yields when the surface is high enough and water is flowing in, maps to vanilla canBeReplacedWith
    public override bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction)
        => state.OwnHeight >= 0.44444445f && other.Id.Path is "water" or "flowing_water";

    public override BlockState CreateLegacyBlock(FluidState state)
        => Blocks.LAVA.DefaultBlockState.SetValue(BlockStateProperties.Level15, GetLegacyLevel(state));

    //CanConvertToSource controlled by the lava_source_conversion game rule in vanilla, off by default; this takes the default value
    protected override bool CanConvertToSource(ServerLevel level) => false;

    //SpreadTo when flowing down into water the water vanishes and congeals into stone, maps to vanilla LavaFluid.spreadTo
    protected override void SpreadTo(ServerLevel level, BlockPos pos, BlockState state, Direction direction, FluidState target)
    {
        if (direction == Direction.Down && level.GetFluidState(pos).IsWater)
        {
            //Only a fluid block congeals; vanilla checks instanceof LiquidBlock, here we check that the cell carries a fluid state
            if (!state.FluidState.IsEmpty) level.SetBlock(pos, Blocks.STONE.DefaultBlockState, 3);
            level.LevelEvent(1501, pos, 0);
            return;
        }
        base.SpreadTo(level, pos, state, direction, target);
    }

    //Source lava source, maps to vanilla LavaFluid.Source
    public sealed class Source : LavaFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("lava");

        public override bool IsSource(FluidState state) => true;

        public override int GetAmount(FluidState state) => 8;
    }

    //Flowing flowing lava, maps to vanilla LavaFluid.Flowing
    public sealed class Flowing : LavaFluid
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("flowing_lava");

        protected override int GetDefaultAmount() => 1;

        public override bool IsSource(FluidState state) => false;

        public override int GetAmount(FluidState state) => state.Amount;
    }
}
