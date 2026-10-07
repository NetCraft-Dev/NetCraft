using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//BlackholeTickAccess, containers that swallow every tick and report nothing, maps to vanilla net.minecraft.world.tick.BlackholeTickAccess
//Used where ticks have no meaning: a chunk that is not loaded, or a level still being assembled
//Each shape holds no state, so one instance per type argument is enough
public static class BlackholeTickAccess
{
    //EmptyContainer, a container that drops everything, maps to vanilla emptyContainer
    public static ITickContainerAccess<T> EmptyContainer<T>() where T : class => EmptyContainerAccess<T>.Instance;

    //EmptyLevelList, a level list that drops everything, maps to vanilla emptyLevelList
    public static ILevelTickAccess<T> EmptyLevelList<T>() where T : class => EmptyLevelAccess<T>.Instance;

    private sealed class EmptyContainerAccess<T> : ITickContainerAccess<T> where T : class
    {
        public static readonly EmptyContainerAccess<T> Instance = new();

        public void Schedule(ScheduledTick<T> tick) { }
        public bool HasScheduledTick(BlockPos pos, T type) => false;
        public bool WillTickThisTick(BlockPos pos, T type) => false;
        public int Count => 0;
    }

    private sealed class EmptyLevelAccess<T> : ILevelTickAccess<T> where T : class
    {
        public static readonly EmptyLevelAccess<T> Instance = new();

        public void Schedule(ScheduledTick<T> tick) { }
        public bool HasScheduledTick(BlockPos pos, T type) => false;
        public bool WillTickThisTick(BlockPos pos, T type) => false;
        public int Count => 0;
    }
}
