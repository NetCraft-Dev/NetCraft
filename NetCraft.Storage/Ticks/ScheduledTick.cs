using NetCraft.Nbt;
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

    //ToCompoundTag 序列化成存档形态 对应原版 SavedTick codec 的编码
    //字段名对齐原版 i 类型名 x y z 坐标 t 延迟 p 优先级
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

    //FromCompoundTag 从存档形态还原 类型名交给调用方按注册表找回
    //类型名缺失或查不到类型返回 null 与原版读档解不出类型的刻直接丢弃一致
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
