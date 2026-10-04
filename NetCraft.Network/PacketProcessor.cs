using System.Collections.Concurrent;
using System.Reflection;
using NetCraft.Logging;

namespace NetCraft.Network;

//PacketProcessor 包处理器对应原版 net.minecraft.network.PacketProcessor
//在主线程上调度和执行包处理避免并发问题
//runningThread 是主线程引用用于 IsSameThread 判断
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

    //IsSameThread 当前线程是否为主线程
    public bool IsSameThread => ReferenceEquals(Thread.CurrentThread, _runningThread);

    //IsClosed 是否已关闭
    public bool IsClosed => _closed;

    //ScheduleIfPossible 泛型版调度包到主线程处理
    public void ScheduleIfPossible<THandler>(THandler listener, Packet<THandler> packet)
        where THandler : class
        => ScheduleIfPossible((object)listener, (object)packet);

    //ScheduleIfPossible 非泛型版调度包到主线程处理
    //Connection.Receive 解码后用 object 装箱调用此方法
    public void ScheduleIfPossible(object listener, object packet)
    {
        if (_closed)
            throw new InvalidOperationException("PacketProcessor 已关闭");
        _packetsToBeHandled.Enqueue(new ListenerAndPacket(listener, packet));
    }

    //ProcessQueuedPackets 处理所有排队包
    //关闭后直接返回
    public void ProcessQueuedPackets()
    {
        if (_closed) return;
        while (_packetsToBeHandled.TryDequeue(out var item))
            item.Handle(this);
    }

    //HandleNow 在调用线程直接处理一个包 不经过队列
    //握手与状态阶段对应原版在 netty 线程处理 不走主线程调度
    //主 Tick 未启动时(出生点预生成窗口)也要能应答这两个阶段的包
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

    //ListenerAndPacket 监听器和包的关联
    //类型擦除存储用反射调用 Handle
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
                //反射Invoke把处理器内的真实异常包成TargetInvocationException 这里解包再报
                //不改用CreateDelegate是因为处理器异常需要完整暴露给运维
                var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                Log.Warning($"Packet handling failed {_packet.GetType().Name}: {cause.GetType().Name} {cause.Message}");
            }
        }
    }

    //BuildHandleInvoker 构造 (listener, packet) -> 调用 packet.Handle(listener) 的委托
    //首次反射查找 Handle 方法后续直接委托调用避免重复反射开销
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
