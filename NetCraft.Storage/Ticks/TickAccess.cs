using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//ITickAccess, the base scheduled tick interface, maps to vanilla net.minecraft.world.tick.TickAccess
//Scheduling, a lookup and a size; the container and level shapes are both layered on this
public interface ITickAccess<T> where T : class
{
    void Schedule(ScheduledTick<T> tick);
    bool HasScheduledTick(BlockPos pos, T type);
    int Count { get; }
}

//ILevelTickAccess, the level-wide view, maps to vanilla net.minecraft.world.tick.LevelTickAccess
//Adds the one question a chunk container cannot answer: whether a tick is due in the current tick
public interface ILevelTickAccess<T> : ITickAccess<T> where T : class
{
    bool WillTickThisTick(BlockPos pos, T type);
}

//ISerializableTickContainer, the save side of a container, maps to vanilla net.minecraft.world.tick.SerializableTickContainer
//Pack turns the live ticks back into the delay form the save file holds
public interface ISerializableTickContainer<T> where T : class
{
    List<SavedTick<T>> Pack(long currentTick);
}
