using NetCraft.Codec;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Timers;

//TimerQueue 计划事件队列对应原版 net.minecraft.world.level.timers.TimerQueue
//存 data/minecraft/scheduled_events.dat 事件按触发时间与流水号排序
//同 id 同刻的重复排程直接忽略 移除按 id 清全部刻
public class TimerQueue<T> : SavedData
{
    private readonly PriorityQueue<TimerQueueEvent<T>, (long TriggerTime, ulong SequentialId)> _queue = new();
    private ulong _sequentialId;
    private readonly Dictionary<string, Dictionary<long, TimerQueueEvent<T>>> _events = new();

    //CreateCodec 队列编解码对应原版 TimerQueue.codec
    public static Codec<TimerQueue<T>> CreateCodec(TimerCallbacks<T> callbacks)
        => Packed.Codec(callbacks.Codec()).ComapFlatMap(
            packed => DataResult<TimerQueue<T>>.Success(new TimerQueue<T>(packed)),
            queue => queue.Pack());

    public TimerQueue()
    {
    }

    //Packed 构造从打包数据还原事件流
    public TimerQueue(Packed packed)
    {
        _sequentialId = 0;
        foreach (var (triggerTime, id, callback) in packed.Events)
        {
            Schedule(id, triggerTime, callback);
        }
    }

    //Tick 触发所有到点事件对应原版 tick
    public void Tick(T context, long currentTick)
    {
        while (_queue.TryPeek(out var @event, out _) && @event.TriggerTime <= currentTick)
        {
            _queue.Dequeue();
            if (_events.TryGetValue(@event.Id, out var byTime))
            {
                byTime.Remove(@event.TriggerTime);
                if (byTime.Count == 0) _events.Remove(@event.Id);
            }
            SetDirty();
            @event.Callback.Handle(context, this, currentTick);
        }
    }

    //Schedule 排一个事件同键已存在则忽略对应原版 schedule
    public void Schedule(string id, long time, TimerCallback<T> callback)
    {
        if (_events.TryGetValue(id, out var byTime) && byTime.ContainsKey(time))
        {
            return;
        }
        _sequentialId++;
        var newEvent = new TimerQueueEvent<T>(time, _sequentialId, id, callback);
        if (!_events.TryGetValue(id, out var bucket))
        {
            bucket = [];
            _events[id] = bucket;
        }
        bucket[time] = newEvent;
        _queue.Enqueue(newEvent, (time, _sequentialId));
        SetDirty();
    }

    //Remove 按 id 移除全部刻上的事件返回移除数对应原版 remove
    public int Remove(string id)
    {
        if (!_events.TryGetValue(id, out var byTime))
        {
            return 0;
        }
        var count = byTime.Count;
        _events.Remove(id);
        RebuildQueue();
        SetDirty();
        return count;
    }

    //RebuildQueue 移除后重建优先队列 .NET 队列不支持按元素删除
    private void RebuildQueue()
    {
        _queue.Clear();
        foreach (var byTime in _events.Values)
        {
            foreach (var @event in byTime.Values)
            {
                _queue.Enqueue(@event, (@event.TriggerTime, @event.SequentialId));
            }
        }
    }

    //GetEventsIds 事件 id 快照
    public IReadOnlyCollection<string> GetEventsIds() => _events.Keys.ToArray();

    //Pack 按触发时间与流水号排序打包对应原版 pack
    private Packed Pack()
    {
        var events = new List<TimerQueueEvent<T>>();
        foreach (var byTime in _events.Values)
        {
            foreach (var @event in byTime.Values)
            {
                events.Add(@event);
            }
        }
        events.Sort((a, b) => a.TriggerTime != b.TriggerTime
            ? a.TriggerTime.CompareTo(b.TriggerTime)
            : a.SequentialId.CompareTo(b.SequentialId));
        return new Packed(events.Select(@event => new PackedEvent(@event.TriggerTime, @event.Id, @event.Callback)).ToList());
    }

    //Id 存档标识
    public override string Id => "minecraft:scheduled_events";

    //Save 写盘字段名对齐原版 codec
    public override CompoundTag Save(CompoundTag tag)
    {
        var encoded = CreateCodec(TimerCallbacks.ServerCallbacks).EncodeStart(NbtOps.Instance, this).GetOrThrow();
        return encoded as CompoundTag ?? throw new InvalidOperationException("scheduled_events must encode to a compound tag");
    }

    //Packed 打包数据对应原版 TimerQueue.Packed record
    public sealed record Packed(List<PackedEvent> Events)
    {
        public static Codec<Packed> Codec(Codec<TimerCallback<T>> callbackCodec)
            => RecordCodecBuilder.Of1<Packed, IReadOnlyList<PackedEvent>>(
                PackedEvent.Codec(callbackCodec).ListOf().FieldOf("events")
                    .ForGetter<Packed, IReadOnlyList<PackedEvent>>(p => p.Events),
                events => new Packed(events.ToList()));
    }

    //PackedEvent 落盘单条事件对应原版 TimerQueue.Event.Packed record
    public sealed record PackedEvent(long TriggerTime, string Id, TimerCallback<T> Callback)
    {
        public static Codec<PackedEvent> Codec(Codec<TimerCallback<T>> callbackCodec)
            => RecordCodecBuilder.Of3<PackedEvent, long, string, TimerCallback<T>>(
                Codecs.Long.FieldOf("trigger_time").ForGetter<PackedEvent, long>(e => e.TriggerTime),
                Codecs.String.FieldOf("id").ForGetter<PackedEvent, string>(e => e.Id),
                callbackCodec.FieldOf("callback").ForGetter<PackedEvent, TimerCallback<T>>(e => e.Callback),
                (triggerTime, id, callback) => new PackedEvent(triggerTime, id, callback));
    }
}

//TimerQueueEvent 队列内事件对应原版 TimerQueue.Event record
public sealed record TimerQueueEvent<T>(long TriggerTime, ulong SequentialId, string Id, TimerCallback<T> Callback);

//TimerQueueTypes 服务端计划事件存档类型对应原版 TimerQueue.TYPE
//C# 泛型类的静态成员要带类型参数 故放到独立的非泛型持有者
public static class TimerQueueTypes
{
    //TypeId 存档文件名
    public const string TypeId = "minecraft:scheduled_events";

    //Instance SavedDataType 工厂
    public static readonly SavedDataType<TimerQueue<MinecraftServer>> Instance = new ScheduledEventsType();

    //Codec 队列编解码复用静态表
    public static readonly Codec<TimerQueue<MinecraftServer>> ServerCodec =
        TimerQueue<MinecraftServer>.CreateCodec(TimerCallbacks<MinecraftServer>.ServerCallbacks);

    private sealed class ScheduledEventsType : SavedDataType<TimerQueue<MinecraftServer>>
    {
        public string Id => TypeId;

        public TimerQueue<MinecraftServer> Create(CompoundTag tag, RegistryAccess registryAccess)
        {
            var decoded = ServerCodec.Parse(NbtOps.Instance, tag);
            return decoded.Result().IsPresent ? decoded.GetOrThrow() : new TimerQueue<MinecraftServer>();
        }
    }
}
