using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Redstone;

//Block-side signal contract, maps to the protected signal methods on vanilla BlockBehaviour
//Kept in the Storage layer so the level need not depend back on Game-layer block implementations
public interface IBlockSignalBehaviour
{
    //IsSignalSource, whether it is a signal source, maps to vanilla isSignalSource
    bool IsSignalSource { get; }

    //IsDiode, whether it is a diode, i.e. repeater or comparator, maps to vanilla DiodeBlock.isDiode
    //Repeater side locking and comparator side input both filter on it
    bool IsDiode { get; }

    //HasAnalogOutputSignal, whether it has an analog output; comparators read it, maps to vanilla hasAnalogOutputSignal
    bool HasAnalogOutputSignal { get; }

    //OwnSignal, the block's own signal strength, maps to vanilla ownSignal
    int OwnSignal(ServerLevel level, BlockPos pos, BlockState state);

    //GetSignal, the signal strength the block outputs in the given direction, defaulting to its own strength, maps to vanilla getSignal
    int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction);

    //GetDirectSignal, the direct signal that conducting blocks propagate, maps to vanilla getDirectSignal
    int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction);

    //IsRedstoneConductor, whether it is a redstone conductor; if so, neighbors' direct signals merge into this position's signal
    bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state);

    //GetAnalogOutputSignal, the analog output strength, read by comparators
    int GetAnalogOutputSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction);
}
