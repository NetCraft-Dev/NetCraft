using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//ProtoChunkTicks, the tick container a chunk carries before it starts ticking, maps to vanilla net.minecraft.world.tick.ProtoChunkTicks
//Ticks are held in their saved form rather than as absolute times, because the game tick they should fire at is not
//known until the chunk is loaded into a level and the save data is read against a running clock
public sealed class ProtoChunkTicks<T> : ITickContainerAccess<T>, ISerializableTickContainer<T> where T : class
{
    private readonly List<SavedTick<T>> _ticks = new();
    private readonly HashSet<SavedTick<T>> _ticksPerPosition = new(new SavedTick<T>.UniqueTickComparer());

    public int Count => _ticks.Count;

    //A tick scheduled here has no firing time yet, so the delay starts at zero and is recalculated on load
    public void Schedule(ScheduledTick<T> tick)
        => Schedule(new SavedTick<T>(tick.Type, tick.Pos, 0, tick.Priority));

    private void Schedule(SavedTick<T> tick)
    {
        if (_ticksPerPosition.Add(tick)) _ticks.Add(tick);
    }

    public bool HasScheduledTick(BlockPos pos, T type)
        => _ticksPerPosition.Contains(SavedTick<T>.Probe(type, pos));

    //A container cannot answer this before it is part of a level, and nothing asks
    public bool WillTickThisTick(BlockPos pos, T type) => false;

    //Pack hands back the stored list itself, matching vanilla: the entries are already in saved form and need no work
    public List<SavedTick<T>> Pack(long currentTick) => _ticks;

    //ScheduledTicks, a copy for callers that must not see later changes, maps to vanilla scheduledTicks
    public List<SavedTick<T>> ScheduledTicks => new(_ticks);

    //Load builds a container from a saved list, maps to vanilla load
    public static ProtoChunkTicks<T> Load(List<SavedTick<T>> ticks)
    {
        var result = new ProtoChunkTicks<T>();
        foreach (var tick in ticks) result.Schedule(tick);
        return result;
    }
}
