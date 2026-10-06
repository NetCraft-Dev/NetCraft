using NetCraft.Codec;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Level.Timers;

//TimerQueue scheduled event queue, maps to vanilla net.minecraft.world.level.timers.TimerQueue
//Stored in data/minecraft/scheduled_events.dat; events sorted by trigger time and sequence id
//Duplicate scheduling with the same id and tick is ignored; removal clears all ticks for an id
public class TimerQueue<T> : SavedData
{
    private readonly PriorityQueue<TimerQueueEvent<T>, (long TriggerTime, ulong SequentialId)> _queue = new();
    private ulong _sequentialId;
    private readonly Dictionary<string, Dictionary<long, TimerQueueEvent<T>>> _events = new();

    //CreateCodec queue codec, maps to vanilla TimerQueue.codec
    public static Codec<TimerQueue<T>> CreateCodec(TimerCallbacks<T> callbacks)
        => Packed.Codec(callbacks.Codec()).ComapFlatMap(
            packed => DataResult<TimerQueue<T>>.Success(new TimerQueue<T>(packed)),
            queue => queue.Pack());

    public TimerQueue()
    {
    }

    //Packed constructor restores the event stream from packed data
    public TimerQueue(Packed packed)
    {
        _sequentialId = 0;
        foreach (var (triggerTime, id, callback) in packed.Events)
        {
            Schedule(id, triggerTime, callback);
        }
    }

    //Tick fires all due events, maps to vanilla tick
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

    //Schedule queue an event, ignored if the same key already exists, maps to vanilla schedule
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

    //Remove removes all events for an id, returns the count removed, maps to vanilla remove
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

    //RebuildQueue rebuilds the priority queue after removal; the .NET queue does not support removing by element
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

    //GetEventsIds snapshot of event ids
    public IReadOnlyCollection<string> GetEventsIds() => _events.Keys.ToArray();

    //Pack packs sorted by trigger time and sequence id, maps to vanilla pack
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

    //Id save identifier
    public override string Id => "minecraft:scheduled_events";

    //Save disk field names align with the vanilla codec
    //Only the server queue is persisted; queues of other T are not persisted
    public override CompoundTag Save(CompoundTag tag)
    {
        if (this is not TimerQueue<MinecraftServer> serverQueue)
            throw new InvalidOperationException("Only the server timer queue persists to disk");
        var encoded = TimerQueueTypes.ServerCodec.EncodeStart(NbtOps.Instance, serverQueue).GetOrThrow();
        return encoded as CompoundTag ?? throw new InvalidOperationException("scheduled_events must encode to a compound tag");
    }

    //Packed packed data, maps to vanilla TimerQueue.Packed record
    public sealed record Packed(List<PackedEvent> Events)
    {
        public static Codec<Packed> Codec(Codec<TimerCallback<T>> callbackCodec)
            => RecordCodecBuilder.Of1<Packed, IReadOnlyList<PackedEvent>>(
                PackedEvent.Codec(callbackCodec).ListOf().FieldOf("events")
                    .ForGetter<Packed, IReadOnlyList<PackedEvent>>(p => p.Events),
                events => new Packed(events.ToList()));
    }

    //PackedEvent a single persisted event, maps to vanilla TimerQueue.Event.Packed record
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

//TimerQueueEvent event within the queue, maps to vanilla TimerQueue.Event record
public sealed record TimerQueueEvent<T>(long TriggerTime, ulong SequentialId, string Id, TimerCallback<T> Callback);

//TimerQueueTypes server scheduled event save type, maps to vanilla TimerQueue.TYPE
//Static members of a C# generic class need a type parameter, so they live in a separate non-generic holder
public static class TimerQueueTypes
{
    //TypeId save file name
    public const string TypeId = "minecraft:scheduled_events";

    //Instance SavedDataType factory
    public static readonly SavedDataType<TimerQueue<MinecraftServer>> Instance = new ScheduledEventsType();

    //Codec queue codec reusing the static table
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
