using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//WorldGenTickAccess, the level tick list standing in during world generation, maps to vanilla net.minecraft.world.tick.WorldGenTickAccess
//No chunk container exists yet while terrain is being built, so every call goes through a lookup that finds or creates
//the container for the position it is asked about
public sealed class WorldGenTickAccess<T> : ILevelTickAccess<T> where T : class
{
    private readonly Func<BlockPos, ITickContainerAccess<T>> _containerGetter;

    public WorldGenTickAccess(Func<BlockPos, ITickContainerAccess<T>> containerGetter)
        => _containerGetter = containerGetter;

    public void Schedule(ScheduledTick<T> tick) => _containerGetter(tick.Pos).Schedule(tick);

    public bool HasScheduledTick(BlockPos pos, T type) => _containerGetter(pos).HasScheduledTick(pos, type);

    //World generation never asks whether a tick is due, so this answers without paying for a lookup
    public bool WillTickThisTick(BlockPos pos, T type) => false;

    //Counting would mean walking every container; vanilla reports zero here for the same reason
    public int Count => 0;
}
