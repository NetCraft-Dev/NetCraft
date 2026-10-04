using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using NetCraft.Logging;
using NetCraft.Network.Protocol;
using NetCraft.Registry;

namespace NetCraft.Network;

//Connection 协议连接对应原版 net.minecraft.network.Connection
//用双向 Stream 替代 netty Channel 持有当前 inbound/outbound 协议状态
//提供 Send/Receive/Tick/Disconnect 完整生命周期对齐原版 API
//加密压缩通过阈值开关控制原版 netty pipeline 这里简化为同步读写
public sealed class Connection : IDisposable
{
    private readonly Stream _readStream;
    private readonly Stream _writeStream;
    private readonly PacketFlow _receiving;
    private readonly PacketProcessor _processor;
    private readonly ConcurrentQueue<Action<Connection>> _pendingActions = new();

    //读循环后台线程阻塞 ReceiveRaw 流结束即 Disconnect
    //真实 TCP 场景由 ConnectionAcceptor 调 StartReadLoop 启动测试不调保持手动 Receive
    private Thread? _readThread;
    private volatile bool _readLoopRunning;

    //Transport 真实 TCP 传输层 Dispose 时关闭 TcpClient 触发读循环退出
    //测试用 QueueStream 不设置 Dispose 不关任何流
    private IDisposable? _transport;

    //RawPackets 读循环入队的原始 payload 队列 只存放需要主线程处理的阶段(configuration 与 play)的包
    //握手/状态/登录三阶段不进这个队列 由读线程直接解码处理 见 ReadLoopHandlesPackets
    private readonly ConcurrentQueue<byte[]> _rawPackets = new();

    private INonGenericProtocol? _inboundProtocol;
    private INonGenericProtocol? _outboundProtocol;
    private PacketListener? _packetListener;

    //ReadLoopHandlesPackets 当前协议阶段的包是否由读线程直接解码处理
    //对应原版握手/状态/登录三阶段在 netty 线程直接处理 只有 configuration/play 才调度回主线程
    //主 Tick 循环启动前(出生点预生成窗口)这几步必须能自行推进 否则服务器列表查询无人响应
    //阶段单向推进 从握手起为真 切到 configuration 即转假 之后不再回转 读线程读到的值始终与解码用的协议一致
    private volatile bool _readLoopHandlesPackets;
    private PacketListener? _disconnectListener;
    private DisconnectionDetails? _disconnectionDetails;
    private bool _disconnectionHandled;
    private bool _sendLoginDisconnect = true;
    private int _receivedPackets;
    private int _sentPackets;

    //_sendQueue 出站包队列 编码压缩加密写流都算重活统统计到固定写线程
    //主线程 Send 只入队 对齐原版把这三步放在 netty 线程的行为
    //_flushQueued=1 表示该连接已在全局待排空队列里或正被排空 既保证包序也避免重复投递
    private readonly ConcurrentQueue<PendingSend> _sendQueue = new();
    private int _flushQueued;
    private volatile bool _sendClosed;

    //PendingSend 待写出项 协议与注册表访问在入队时快照
    //协议会随 Tick 切换 后台线程不能再去读那些可变字段
    //Packet 为 null 表示纯刷盘标记 只用于 Flush 等待前序包写完
    private readonly record struct PendingSend(
        INonGenericProtocol? Protocol, object? Packet, RegistryAccess Access, TaskCompletionSource? Flushed);

    //CompressionThreshold 压缩阈值 -1 禁用包数据长度超过阈值才压缩
    public int CompressionThreshold { get; set; } = -1;

    //EncryptionEnabled 是否启用加密启用后所有读写经 CryptoHelper 处理
    public bool EncryptionEnabled { get; private set; }
    private byte[]? _encryptKey;
    private bool _disposed;

    //_encodeBuf 出站编码缓冲 整连接复用一份
    //同一连接同一时刻只有一个排空者(见 Enqueue 的 _flushQueued CAS) 复用不会跨线程撞车
    private RegistryFriendlyByteBuf? _encodeBuf;

    //RegistryAccess 注册表访问默认空 Login 阶段无注册表上下文
    //进入 Configuration/Play 阶段后由 SetupOutboundProtocol 调用方设置真实 registryAccess
    public RegistryAccess RegistryAccess { get; set; } = RegistryAccess.Empty;

    //RemoteAddress 客户端 IP 文本 由 ConnectionAcceptor 在 accept 时写入
    //测试用内存流直接赋值 未设置时 IP 封禁检查放行
    public string? RemoteAddress { get; set; }

    //Connection 构造对齐原版 Connection(PacketFlow) 但显式传 Stream
    public Connection(Stream readStream, Stream writeStream, PacketFlow receiving, Thread? runningThread = null)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _receiving = receiving;
        _processor = new PacketProcessor(runningThread);
    }

    //SetTransport 设置真实 TCP 传输层 Dispose 时关闭触发读循环退出
    //ConnectionAcceptor 传入 TcpClient 测试用 QueueStream 不调用
    internal void SetTransport(IDisposable transport) => _transport = transport;

    //StartReadLoop 启动后台读线程循环 Receive 直到流结束或 Dispose
    //真实 TCP 场景由 ConnectionAcceptor 在装好监听器后调用
    //测试用 QueueStream 不调用由测试手动 Receive 保持兼容
    //读线程阻塞在 Receive 的 Read 上 Dispose 关闭 Transport 会让 Read 抛异常退出
    public void StartReadLoop()
    {
        if (_readThread != null) return;
        _readLoopRunning = true;
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ConnectionReadLoop" };
        _readThread.Start();
    }

    //ReadLoop 后台读循环阻塞 ReceiveRaw
    //握手/状态/登录三阶段的包在本线程直接解码处理 对齐原版 netty 线程处理这几个阶段
    //其余阶段只入队原始 payload 交主线程 Tick 解码 避免读循环用旧协议误解码新协议的包
    //如 Handshake 切 Status 时 StatusRequest 被读循环用旧 Handshake 协议误解码
    private void ReadLoop()
    {
        while (_readLoopRunning && !_disposed)
        {
            try
            {
                var payload = ReceiveRaw();
                if (payload == null)
                {
                    Disconnect(new DisconnectionDetails("对端关闭连接"));
                    break;
                }
                if (_readLoopHandlesPackets) DecodePayload(payload, direct: true);
                else _rawPackets.Enqueue(payload);
            }
            catch (IOException) { Disconnect(new DisconnectionDetails("连接 IO 异常")); break; }
            catch (SocketException) { Disconnect(new DisconnectionDetails("连接 Socket 异常")); break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    //Receiving 接收方向对齐原版 getReceiving
    public PacketFlow Receiving => _receiving;

    //Sending 发送方向对齐原版 getSending
    public PacketFlow Sending => _receiving.GetOpposite();

    //ReceivingDirection 接收方向转为 FlowDirection 用于和 PacketListener.Flow 比较
    public FlowDirection ReceivingDirection
        => _receiving == PacketFlow.Serverbound ? FlowDirection.Serverbound : FlowDirection.Clientbound;

    //IsConnected 是否已连接对齐原版 isConnected
    public bool IsConnected => !_disposed;

    //IsConnecting 是否正在连接对齐原版 isConnecting
    public bool IsConnecting => !_disposed && _inboundProtocol == null;

    //PacketListener 当前包监听器对齐原版 getPacketListener
    public PacketListener? Listener => _packetListener;

    //DisconnectionDetails 断连详情
    public DisconnectionDetails? DisconnectionDetails => _disconnectionDetails;

    //SetupInboundProtocol 配置入站协议和监听器对齐原版 setupInboundProtocol
    public void SetupInboundProtocol<THandler>(ProtocolInfo<THandler> protocol, THandler listener)
        where THandler : class, PacketListener
    {
        ValidateListener(protocol, listener);
        _packetListener = listener;
        _disconnectListener = null;
        _inboundProtocol = protocol;
        _readLoopHandlesPackets = HandlesOnReadLoop(protocol.Id);
    }

    //SetupOutboundProtocol 配置出站协议对齐原版 setupOutboundProtocol
    public void SetupOutboundProtocol(INonGenericProtocol protocol)
    {
        _outboundProtocol = protocol;
        _sendLoginDisconnect = protocol.Id == ConnectionProtocol.Login;
    }

    //SetInitialInboundProtocolInternal 设置初始入站协议和监听器
    //供 Game 层扩展方法 SetListenerForServerboundHandshake 使用
    //不暴露 public 是因为业务包依赖由 Game 层负责
    internal void SetInitialInboundProtocolInternal(PacketListener listener, INonGenericProtocol inboundProtocol)
    {
        if (_packetListener != null)
            throw new InvalidOperationException("监听器已设置");
        _packetListener = listener;
        _inboundProtocol = inboundProtocol;
        _readLoopHandlesPackets = HandlesOnReadLoop(inboundProtocol.Id);
    }

    //HandlesOnReadLoop 该协议的包是否由读线程直接处理
    //握手/状态/登录三阶段原版都在 netty 线程处理 这三段只做校验与收发不碰世界状态
    //configuration 与 play 要访问主线程独占的世界状态 必须交回主 Tick
    private static bool HandlesOnReadLoop(ConnectionProtocol protocol)
        => protocol is ConnectionProtocol.Handshake or ConnectionProtocol.Status or ConnectionProtocol.Login;

    //DisconnectListenerInternal 设置断连监听器供 Game 扩展方法使用
    internal void SetDisconnectListenerInternal(PacketListener? listener) => _disconnectListener = listener;

    //Send 发包对齐原版 send(Packet)
    //EncodePacket 内部已写 packetId+payload 这里不重复写 packetId
    //用 RegistryFriendlyByteBuf 让 ItemStack 等业务 codec 访问注册表
    //Send 发送包 只入队由后台写线程完成编码压缩与写出
    //调用线程不再被大包的编码压缩拖住 对齐原版 send 交 netty 线程的语义
    public void Send<THandler>(Packet<THandler> packet) where THandler : class
    {
        //连接已关闭时静默丢弃 对应原版断连后发包只写不出去不会打断调用方
        //踢人命令先断开目标再给自己回执 这里抛异常会把整个命令处理打断
        if (_disposed) return;
        if (_outboundProtocol == null)
            throw new InvalidOperationException("未配置出站协议");
        Enqueue(new PendingSend(_outboundProtocol, packet, RegistryAccess, null));
    }

    //Flush 等待已入队的包全部写出 关服与测试断言前调用
    //返回 false 表示超时未写完 数据可能不完整
    public bool Flush(int timeoutMillis = 5000)
    {
        if (_sendClosed) return true;
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new PendingSend(null, null, RegistryAccess.Empty, flushed));
        return flushed.Task.Wait(timeoutMillis);
    }

    //Enqueue 入队待写项 队列已关闭时直接放过避免关闭期间卡住调用方
    private void Enqueue(PendingSend send)
    {
        if (_sendClosed)
        {
            send.Flushed?.TrySetResult();
            return;
        }
        _sendQueue.Enqueue(send);
        //CAS 成功才投递 同一连接同时只有一个排空者保证包序
        //已被投递过的连接不再重复入全局队列 广播上千个连接时这一层挡掉绝大部分投递
        if (Interlocked.CompareExchange(ref _flushQueued, 1, 0) == 0)
            OutboundWriters.Mark(this);
    }

    //DrainOutbound 排空出站队列 由全局写线程调用 单线程串行消费保证包序
    //单个包编码失败只丢弃它并告警 不让一个坏包拖垮整条连接
    internal void DrainOutbound()
    {
        while (true)
        {
            while (_sendQueue.TryDequeue(out var send))
            {
                try
                {
                    if (send.Packet is not null && send.Protocol is not null)
                        EncodeAndWrite(send.Protocol, send.Packet, send.Access);
                }
                catch (ObjectDisposedException) { }
                catch (Exception e)
                {
                    Log.Warning($"Outbound packet write failed, dropping {send.Packet?.GetType().Name}: {e.Message}");
                }
                finally
                {
                    send.Flushed?.TrySetResult();
                }
            }
            //整轮排空后刷一次流 原来每个包都刷一次 对网络流与内存流都是空操作
            try { _writeStream.Flush(); }
            catch (Exception e) { Log.Warning($"Outbound stream flush failed {e.Message}"); }
            //排空后先放开标志再复查 防止放开与并发入队交错时把新包漏在队列里
            Volatile.Write(ref _flushQueued, 0);
            if (_sendQueue.IsEmpty) return;
            if (Interlocked.CompareExchange(ref _flushQueued, 1, 0) != 0) return;
        }
    }

    //EncodeAndWrite 编码压缩加密写流 顺序与原同步实现一致只是挪到写线程
    //编码缓冲整连接复用 长度前缀在栈上拼 这两处原来每个包都要新建一整套缓冲对象
    private void EncodeAndWrite(INonGenericProtocol protocol, object packet, RegistryAccess access)
    {
        var buf = _encodeBuf ??= new RegistryFriendlyByteBuf(access);
        buf.RegistryAccess = access;
        buf.Reset();
        protocol.EncodePacket(buf, packet);

        //未压缩未加密是局域网与测试的常态 这条路径直接把复用缓冲的内容写出去
        //既不复制中间数组 也不用为几个字节的长度前缀单建缓冲
        if (CompressionThreshold < 0 && !(EncryptionEnabled && _encryptKey != null))
        {
            WriteLengthPrefix(buf.Length);
            buf.WriteTo(_writeStream);
            Interlocked.Increment(ref _sentPackets);
            return;
        }

        //压缩与加密都要拿到完整字节 这里仍需一次复制
        byte[] payload = buf.ToArray();
        if (CompressionThreshold >= 0)
            payload = CompressionHelper.CompressIfNeeded(payload, CompressionThreshold);
        if (EncryptionEnabled && _encryptKey != null)
            payload = CryptoHelper.Encrypt(payload, _encryptKey);
        WriteLengthPrefix(payload.Length);
        _writeStream.Write(payload);
        Interlocked.Increment(ref _sentPackets);
    }

    //WriteLengthPrefix 写 VarInt 长度前缀 直接在栈上拼字节
    private void WriteLengthPrefix(int length)
    {
        Span<byte> prefix = stackalloc byte[5];
        var index = 0;
        var value = (uint)length;
        while ((value & ~0x7Fu) != 0)
        {
            prefix[index++] = (byte)((value & 0x7F) | 0x80);
            value >>>= 7;
        }
        prefix[index++] = (byte)value;
        _writeStream.Write(prefix[..index]);
    }

    //SendAll 批量发包对齐原版 sendAll
    public void SendAll<THandler>(IEnumerable<Packet<THandler>> packets) where THandler : class
    {
        foreach (var p in packets) Send(p);
    }

    //Receive 阻塞读取一个包并立即解码调度到 PacketProcessor
    //对齐原版 channelRead0 解码后调用 genericsFtw 调 packet.handle(listener)
    //测试场景手动调用真实 TCP 场景由 ReadLoop 入队 Tick 解码
    //返回 false 表示流已结束无包可读
    public bool Receive()
    {
        if (_disposed || _inboundProtocol == null || _packetListener == null) return false;
        byte[]? payload;
        try
        {
            payload = ReceiveRaw();
        }
        catch (IOException)
        {
            return false;
        }
        if (payload == null) return false;
        DecodePayload(payload);
        return true;
    }

    //ReceiveRaw 读取一个原始包返回 payload 不解码
    //读取 VarInt 长度前缀 + 指定长度 payload 解密解压后返回
    //返回 null 表示流已结束无包可读
    private byte[]? ReceiveRaw()
    {
        //读 VarInt 长度前缀 1-5 字节读到 MSB=0 为止
        int length = 0;
        int shift = 0;
        int b;
        do
        {
            b = _readStream.ReadByte();
            if (b < 0) return null;
            length |= (b & 0x7F) << shift;
            shift += 7;
            if (shift > 35) throw new IOException($"VarInt 长度前缀超长");
        } while ((b & 0x80) != 0);

        if (length <= 0 || length > 0x200000)
            throw new IOException($"非法包长度 {length}");
        byte[] payload = new byte[length];
        if (!ReadFill(payload, length)) return null;
        if (EncryptionEnabled && _encryptKey != null)
            payload = CryptoHelper.Decrypt(payload, _encryptKey);
        if (CompressionThreshold >= 0)
            payload = CompressionHelper.Decompress(payload);
        return payload;
    }

    //DecodePayload 解码单个 payload 并派发给监听器
    //direct=true 表示调用方是读线程且当前阶段允许 直接在本线程处理不再排进主线程队列
    //解码失败丢弃该包不抛异常避免读循环退出
    private void DecodePayload(byte[] payload, bool direct = false)
    {
        if (_inboundProtocol == null || _packetListener == null) return;
        var dataBuf = new RegistryFriendlyByteBuf(RegistryAccess, payload);
        object? packet;
        try
        {
            packet = _inboundProtocol.DecodePacket(dataBuf);
        }
        catch (Exception ex)
        {
            //带上包网络 ID 便于定位是哪个包的解码问题 PeekVarInt 自身永不抛异常
            //再带上远端与当前入站协议 前者区分玩家连接与外部探测流量 后者区分协议阶段
            //最后带 payload 头几个字节: 非本协议流量打进来时看头几字节就能认出来
            Log.Warning($"Failed to decode packet, dropping it remote={RemoteAddress ?? "unknown"} protocol={_inboundProtocol.Id} id={dataBuf.PeekVarInt()} length={payload.Length} head={HexPrefix(payload)} {ex.Message}");
            return;
        }
        if (packet == null)
        {
            Log.Warning("Decode returned null, dropping the packet");
            return;
        }
        if (direct) _processor.HandleNow(_packetListener, packet);
        else _processor.ScheduleIfPossible(_packetListener, packet);
        _receivedPackets++;
    }

    //HexPrefix 取 payload 前 16 字节的十六进制
    //排查非本协议流量打到本端口时(扫描器/别的明文服务) 看开头几个字节就能认出来
    private static string HexPrefix(byte[] payload)
    {
        var count = Math.Min(16, payload.Length);
        return count == 0 ? "" : Convert.ToHexString(payload, 0, count);
    }

    //DecodeRawPackets 解码 _rawPackets 队列中的所有原始包
    //解码一个立即 ProcessQueuedPackets 确保协议切换在解码下一个包前生效
    //避免 Handshake 包切换 Status 协议后 StatusRequest 仍用旧协议解码的竞态
    private void DecodeRawPackets()
    {
        while (_rawPackets.TryDequeue(out var payload))
        {
            DecodePayload(payload);
            //立即处理让协议切换生效再解码下一个包
            _processor.ProcessQueuedPackets();
        }
    }

    //Tick 每帧调用对齐原版 tick
    //先 DecodeRawPackets 解码读循环入队的原始包并处理触发协议切换
    //再 FlushQueue 执行待处理动作最后兜底处理排队包和断连
    //末尾调 TickablePacketListener.tick 对齐原版 Connection.tick 的监听器 tick
    public void Tick()
    {
        DecodeRawPackets();
        FlushQueue();
        _processor.ProcessQueuedPackets();
        if (_packetListener is TickablePacketListener tickable)
            tickable.TickListener();
        if (!IsConnected && !_disconnectionHandled)
            HandleDisconnection();
    }

    //RunOnceConnected 连接建立后执行动作对齐原版 runOnceConnected
    public void RunOnceConnected(Action<Connection> action)
    {
        if (IsConnected)
        {
            FlushQueue();
            action(this);
        }
        else
        {
            _pendingActions.Enqueue(action);
        }
    }

    //FlushQueue 执行所有待处理动作对齐原版 flushQueue
    private void FlushQueue()
    {
        while (_pendingActions.TryDequeue(out var action))
            action(this);
    }

    //Disconnect 断开连接对齐原版 disconnect
    public void Disconnect(string reason)
        => Disconnect(new DisconnectionDetails(reason));

    //Disconnect 断开连接对齐原版 disconnect(DisconnectionDetails)
    public void Disconnect(DisconnectionDetails details)
    {
        _disconnectionDetails = details;
        _disposed = true;
    }

    //HandleDisconnection 处理断连事件对齐原版 handleDisconnection
    public void HandleDisconnection()
    {
        if (_disconnectionHandled) return;
        _disconnectionHandled = true;
        var listener = _packetListener ?? _disconnectListener;
        if (listener == null) return;
        var details = _disconnectionDetails ?? new DisconnectionDetails("连接已断开");
        listener.OnDisconnect(details.Reason);
    }

    //EnableEncryption 启用 AES 加密对齐原版 setEncryptionKey
    public void EnableEncryption(byte[] key)
    {
        _encryptKey = key;
        EncryptionEnabled = true;
    }

    //SetupCompression 启用压缩对齐原版 setupCompression
    public void SetupCompression(int threshold)
    {
        CompressionThreshold = threshold;
    }

    //ValidateListener 校验监听器方向和协议与 inbound 协议匹配
    private void ValidateListener<THandler>(INonGenericProtocol protocol, THandler listener)
        where THandler : class, PacketListener
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (listener.Flow != ReceivingDirection)
            throw new InvalidOperationException($"监听器方向 {listener.Flow} 与连接接收方向 {ReceivingDirection} 不匹配");
        if (protocol.Id != listener.Protocol)
            throw new InvalidOperationException($"监听器协议 {listener.Protocol} 与 inbound 协议 {protocol.Id} 不匹配");
    }

    //ReadFill 从底层流读满指定字节数到 buffer 返回 false 若流提前结束
    private bool ReadFill(byte[] buffer, int length)
    {
        int read = 0;
        while (read < length)
        {
            int n = _readStream.Read(buffer, read, length - read);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _readLoopRunning = false;
        //先把已入队的包写完再收队列 关服踢人包不能因为异步而丢
        Flush();
        _sendClosed = true;
        //关闭 Transport 让读循环的阻塞 Read 抛 IOException 退出线程
        //测试无 Transport 不影响 QueueStream 由测试自行管理
        _transport?.Dispose();
        _processor.Dispose();
        _packetListener = null;
        _inboundProtocol = null;
        _outboundProtocol = null;
        _encodeBuf?.Dispose();
        _encodeBuf = null;
    }
}

//OutboundWriters 全局出站排空调度 对应原版 netty 的 eventLoop 写线程
//原先每次发包都往线程池提交任务 上千连接广播时会与调度器争锁 主线程每包要花几十微秒
//改为固定几个写线程消费全局待排空队列 主线程单次 Send 只剩一次入队加一次 CAS
internal static class OutboundWriters
{
    private static readonly ConcurrentQueue<Connection> Dirty = new();
    private static readonly SemaphoreSlim Signal = new(0);
    //写线程数取核数一半 编码写流是 CPU 与 IO 混合 并发太高反而互相抢核
    private static readonly int WorkerCount = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

    static OutboundWriters()
    {
        for (var i = 0; i < WorkerCount; i++)
        {
            var thread = new Thread(Loop) { IsBackground = true, Name = $"NetCraftOutbound{i}" };
            thread.Start();
        }
    }

    //Mark 把连接加入待排空队列并唤醒写线程
    public static void Mark(Connection connection)
    {
        Dirty.Enqueue(connection);
        Signal.Release();
    }

    //Loop 取一个连接排空它 取不到就等信号
    //信号计数多于实际连接时最多空转几轮 既不忙等也不会漏唤醒
    private static void Loop()
    {
        while (true)
        {
            if (Dirty.TryDequeue(out var connection))
            {
                connection.DrainOutbound();
                continue;
            }
            Signal.Wait();
        }
    }
}
