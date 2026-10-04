using NetCraft.Primitives;

namespace NetCraft.Storage.Ticks;

//ScheduledTick 调度刻 对应原版 net.minecraft.world.tick.ScheduledTick
//存绝对触发刻而不是延迟 延迟在创建时就换算掉了
public sealed class ScheduledTick<T> where T : class
{
    public T Type { get; }
    public BlockPos Pos { get; }
    public long TriggerTick { get; }
    public TickPriority Priority { get; }
    //SubTickOrder 同一刻内的排入序号 决定同优先级下的先后
    public long SubTickOrder { get; }

    public ScheduledTick(T type, BlockPos pos, long triggerTick, TickPriority priority, long subTickOrder)
    {
        Type = type;
        Pos = pos;
        TriggerTick = triggerTick;
        Priority = priority;
        SubTickOrder = subTickOrder;
    }

    //DrainOrder 容器内排序 触发刻然后优先级然后子序号 对应原版 DRAIN_ORDER
    public static readonly IComparer<ScheduledTick<T>> DrainOrder = Comparer<ScheduledTick<T>>.Create(
        static (a, b) =>
        {
            var byTick = a.TriggerTick.CompareTo(b.TriggerTick);
            if (byTick != 0) return byTick;
            var byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.SubTickOrder.CompareTo(b.SubTickOrder);
        });

    //IntraTickDrainOrder 同刻内跨容器归并 优先级然后子序号 对应原版 INTRA_TICK_DRAIN_ORDER
    public static readonly IComparer<ScheduledTick<T>> IntraTickDrainOrder = Comparer<ScheduledTick<T>>.Create(
        static (a, b) =>
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.SubTickOrder.CompareTo(b.SubTickOrder);
        });

    //UniqueTickComparer 判重只看位置与类型 对应原版 UNIQUE_TICK_HASH
    //同一位置同一类型只会有一个调度刻 后来的会被丢掉
    public sealed class UniqueTickComparer : IEqualityComparer<ScheduledTick<T>>
    {
        public bool Equals(ScheduledTick<T>? a, ScheduledTick<T>? b)
            => ReferenceEquals(a, b)
                || (a is not null && b is not null
                    && ReferenceEquals(a.Type, b.Type) && a.Pos.Equals(b.Pos));

        public int GetHashCode(ScheduledTick<T> o)
            => (31 * o.Pos.GetHashCode()) + o.Type.GetHashCode();
    }

    //ToSavedTick 把绝对触发刻换算成相对当前刻的延迟 对应原版 toSavedTick
    public SavedTick<T> ToSavedTick(long currentTick)
        => new(Type, Pos, (int)(TriggerTick - currentTick), Priority);
}

//SavedTick 存档形态的调度刻 对应原版 net.minecraft.world.tick.SavedTick
//存的是延迟而不是绝对刻 读档时按当时的游戏刻重新换算
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

    //Unpack 换算成绝对触发刻 对应原版 unpack
    public ScheduledTick<T> Unpack(long currentTick, long currentSubTick)
        => new(Type, Pos, currentTick + Delay, Priority, currentSubTick);

    //UniqueTickComparer 与 ScheduledTick 判重规则一致
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
