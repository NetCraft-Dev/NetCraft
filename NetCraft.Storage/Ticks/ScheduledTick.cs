using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//ScheduledTick, a scheduled tick, maps to vanilla net.minecraft.world.tick.ScheduledTick
//Stores the absolute trigger tick rather than a delay; the delay is converted at creation
public sealed class ScheduledTick<T> where T : class
{
    public T Type { get; }
    public BlockPos Pos { get; }
    public long TriggerTick { get; }
    public TickPriority Priority { get; }
    //SubTickOrder, the enqueue order within the same tick, deciding order of equal priority
    public long SubTickOrder { get; }

    public ScheduledTick(T type, BlockPos pos, long triggerTick, TickPriority priority, long subTickOrder)
    {
        Type = type;
        Pos = pos;
        TriggerTick = triggerTick;
        Priority = priority;
        SubTickOrder = subTickOrder;
    }

    //DrainOrder, in-container ordering: trigger tick, then priority, then sub-order, maps to vanilla DRAIN_ORDER
    public static readonly IComparer<ScheduledTick<T>> DrainOrder = Comparer<ScheduledTick<T>>.Create(
        static (a, b) =>
        {
            var byTick = a.TriggerTick.CompareTo(b.TriggerTick);
            if (byTick != 0) return byTick;
            var byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.SubTickOrder.CompareTo(b.SubTickOrder);
        });

    //IntraTickDrainOrder, cross-container merge within a tick: priority then sub-order, maps to vanilla INTRA_TICK_DRAIN_ORDER
    public static readonly IComparer<ScheduledTick<T>> IntraTickDrainOrder = Comparer<ScheduledTick<T>>.Create(
        static (a, b) =>
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.SubTickOrder.CompareTo(b.SubTickOrder);
        });

    //UniqueTickComparer dedups by pos and type only, maps to vanilla UNIQUE_TICK_HASH
    //Only one scheduled tick per pos and type; later ones are dropped
    public sealed class UniqueTickComparer : IEqualityComparer<ScheduledTick<T>>
    {
        public bool Equals(ScheduledTick<T>? a, ScheduledTick<T>? b)
            => ReferenceEquals(a, b)
                || (a is not null && b is not null
                    && ReferenceEquals(a.Type, b.Type) && a.Pos.Equals(b.Pos));

        public int GetHashCode(ScheduledTick<T> o)
            => (31 * o.Pos.GetHashCode()) + o.Type.GetHashCode();
    }

    //ToSavedTick converts the absolute trigger tick into a delay relative to the current tick, maps to vanilla toSavedTick
    public SavedTick<T> ToSavedTick(long currentTick)
        => new(Type, Pos, (int)(TriggerTick - currentTick), Priority);
}

//SavedTick, the saved form of a scheduled tick, maps to vanilla net.minecraft.world.tick.SavedTick
//Stores a delay rather than an absolute tick; on load it is recomputed against the then-current game tick
public sealed class SavedTick<T> where T : class
{
    public T Type { get; }
    public BlockPos Pos { get; }
    public int Delay { get; }
    public TickPriority Priority { get; }

    public SavedTick(T type, BlockPos pos, int delay, TickPriority priority)
    {
        Type = type;
        Pos = pos;
        Delay = delay;
        Priority = priority;
    }

    //Unpack converts to an absolute trigger tick, maps to vanilla unpack
    public ScheduledTick<T> Unpack(long currentTick, long currentSubTick)
        => new(Type, Pos, currentTick + Delay, Priority, currentSubTick);

    //ToCompoundTag serializes into save form, maps to the vanilla SavedTick codec encoding
    //Field names align with vanilla: i type name, x y z coords, t delay, p priority
    public CompoundTag ToCompoundTag(string typeName)
    {
        var tag = new CompoundTag();
        tag.PutString("i", typeName);
        tag.PutInt("x", Pos.X);
        tag.PutInt("y", Pos.Y);
        tag.PutInt("z", Pos.Z);
        tag.PutInt("t", Delay);
        tag.PutInt("p", (int)Priority);
        return tag;
    }

    //FromCompoundTag restores from save form; the type name is resolved by the caller through the registry
    //Returns null when the type name is missing or unresolvable, matching vanilla dropping ticks whose type cannot be resolved on load
    public static SavedTick<T>? FromCompoundTag(CompoundTag tag, Func<string, T?> resolveType)
    {
        var name = tag.GetStringValue("i");
        if (string.IsNullOrEmpty(name)) return null;
        var type = resolveType(name);
        if (type is null) return null;
        return new SavedTick<T>(type,
            new BlockPos(tag.GetIntValue("x"), tag.GetIntValue("y"), tag.GetIntValue("z")),
            tag.GetIntValue("t"), TickPriorities.ByValue(tag.GetIntValue("p")));
    }

    //UniqueTickComparer, same dedup rule as ScheduledTick
    public sealed class UniqueTickComparer : IEqualityComparer<SavedTick<T>>
    {
        public bool Equals(SavedTick<T>? a, SavedTick<T>? b)
            => ReferenceEquals(a, b)
                || (a is not null && b is not null
                    && ReferenceEquals(a.Type, b.Type) && a.Pos.Equals(b.Pos));

        public int GetHashCode(SavedTick<T> o)
            => (31 * o.Pos.GetHashCode()) + o.Type.GetHashCode();
    }
}
