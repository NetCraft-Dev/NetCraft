using System.Collections.Concurrent;
using System.Reflection;
using NetCraft.Logging;

namespace NetCraft.Network;

//PacketProcessor packet processor, maps to vanilla net.minecraft.network.PacketProcessor
//Schedules and executes packet handling on the main thread to avoid concurrency issues
//runningThread is the main thread reference used for the IsSameThread check
public sealed class PacketProcessor : IDisposable
{
    private readonly Thread _runningThread;
    private readonly ConcurrentQueue<ListenerAndPacket> _packetsToBeHandled = new();
    private readonly ConcurrentDictionary<(Type, Type), Action<object, object>> _handleCache = new();
    private bool _closed;

    public PacketProcessor(Thread? runningThread = null)
    {
        _runningThread = runningThread ?? Thread.CurrentThread;
    }

    //IsSameThread indicates whether the current thread is the main thread
    public bool IsSameThread => ReferenceEquals(Thread.CurrentThread, _runningThread);

    //IsClosed indicates whether it is closed
    public bool IsClosed => _closed;

    //ScheduleIfPossible generic version schedules a packet for main-thread handling
    public void ScheduleIfPossible<THandler>(THandler listener, Packet<THandler> packet)
        where THandler : class
        => ScheduleIfPossible((object)listener, (object)packet);

    //ScheduleIfPossible non-generic version schedules a packet for main-thread handling
    //Connection.Receive calls this method after decoding, boxing with object
    public void ScheduleIfPossible(object listener, object packet)
    {
        if (_closed)
            throw new InvalidOperationException("PacketProcessor is closed");
        _packetsToBeHandled.Enqueue(new ListenerAndPacket(listener, packet));
    }

    //ProcessQueuedPackets handles all queued packets
    //Returns directly when closed
    public void ProcessQueuedPackets()
    {
        if (_closed) return;
        while (_packetsToBeHandled.TryDequeue(out var item))
            item.Handle(this);
    }

    //HandleNow handles one packet directly on the calling thread without going through the queue
    //Handshake and status phases map to vanilla handling on the netty thread and skip main-thread scheduling
    //Packets from these two phases must still be answered before the main Tick starts (spawn pre-generation window)
    public void HandleNow(object listener, object packet)
    {
        if (_closed) return;
        new ListenerAndPacket(listener, packet).Handle(this);
    }

    public void Dispose()
    {
        _closed = true;
        _packetsToBeHandled.Clear();
        _handleCache.Clear();
    }

    //ListenerAndPacket associates a listener with a packet
    //Stored type-erased, invoking Handle via reflection
    private readonly struct ListenerAndPacket
    {
        private readonly object _listener;
        private readonly object _packet;

        public ListenerAndPacket(object listener, object packet)
        {
            _listener = listener;
            _packet = packet;
        }

        public void Handle(PacketProcessor processor)
        {
            try
            {
                var packetType = _packet.GetType();
                var listenerType = _listener.GetType();
                var invoker = processor._handleCache.GetOrAdd(
                    (packetType, listenerType),
                    key => BuildHandleInvoker(key.Item1, key.Item2));
                invoker(_listener, _packet);
            }
            catch (Exception ex)
            {
                //Reflection Invoke wraps the real exception from the handler in TargetInvocationException, so it is unwrapped here before reporting
                //Not switching to CreateDelegate because handler exceptions must be fully exposed to operations
                var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                Log.Warning($"Packet handling failed {_packet.GetType().Name}: {cause.GetType().Name} {cause.Message}");
            }
        }
    }

    //BuildHandleInvoker builds a delegate (listener, packet) -> calls packet.Handle(listener)
    //The Handle method is looked up by reflection once and then invoked directly via the delegate to avoid repeated reflection overhead
    private static Action<object, object> BuildHandleInvoker(Type packetType, Type listenerType)
    {
        var handleMethod = packetType.GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { listenerType },
            null);
        if (handleMethod == null)
        {
            return (listener, packet) =>
            {
                var listenerInterfaces = listener.GetType().GetInterfaces();
                foreach (var iface in listenerInterfaces)
                {
                    var ifaceMethod = packetType.GetMethod(
                        "Handle",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] { iface },
                        null);
                    if (ifaceMethod != null)
                    {
                        ifaceMethod.Invoke(packet, new[] { listener });
                        return;
                    }
                }
                throw new MissingMethodException(packetType.Name, "Handle(" + listenerType.Name + ")");
            };
        }
        return (listener, packet) => handleMethod.Invoke(packet, new[] { listener });
    }
}
