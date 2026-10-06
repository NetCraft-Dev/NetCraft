using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util.Random;

namespace NetCraft.Storage;

//IFluidBehaviour, the part of fluid behavior that needs the level, maps to the Level-taking methods on vanilla Fluid
//Vanilla hangs these directly on Fluid; here Fluid is in the registry layer and cannot depend back on the storage layer, so they are split into an interface implemented by Game-layer fluids
public interface IFluidBehaviour
{
    //IsRandomlyTicking, whether it takes part in random ticks; only lava does, maps to vanilla isRandomlyTicking
    bool IsRandomlyTicking { get; }

    //Tick, fluid scheduled tick, maps to vanilla Fluid.tick
    void Tick(ServerLevel level, BlockPos pos, BlockState state, FluidState fluidState);

    //RandomTick, fluid random tick, maps to vanilla Fluid.randomTick
    void RandomTick(ServerLevel level, BlockPos pos, FluidState fluidState, RandomSource random);

    //GetTickDelay, ticks between two flows, maps to vanilla getTickDelay
    int GetTickDelay(ServerLevel level);

    //GetHeight, the actual fluid surface height in this cell; counts as full when the same fluid presses from above, maps to vanilla getHeight
    float GetHeight(FluidState state, ServerLevel level, BlockPos pos);

    //GetFlow, the flow direction of the fluid in this cell, maps to vanilla getFlow
    Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state);

    //CanBeReplacedWith, whether the fluid in this cell can be displaced by another fluid, maps to vanilla canBeReplacedWith
    bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction);
}
