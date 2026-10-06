using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using NetCraft.Logging;
using NetCraft.Network.Protocol;
using NetCraft.Registry;

namespace NetCraft.Network;

//Connection protocol connection, maps to vanilla net.minecraft.network.Connection
//Uses a bidirectional Stream instead of a netty Channel and holds the current inbound/outbound protocol state
//Provides the full Send/Receive/Tick/Disconnect lifecycle aligned with the vanilla API
//Encryption and compression are toggled by thresholds; the vanilla netty pipeline is simplified to synchronous reads and writes here
public sealed class Connection : IDisposable
{
    private readonly Stream _readStream;
    private readonly Stream _writeStream;
    private readonly PacketFlow _receiving;
    private readonly PacketProcessor _processor;
    private readonly ConcurrentQueue<Action<Connection>> _pendingActions = new();

    //Read loop background thread blocks on ReceiveRaw and calls Disconnect when the stream ends
    //In real TCP scenarios ConnectionAcceptor calls StartReadLoop; tests skip it and keep calling Receive manually
    private Thread? _readThread;
    private volatile bool _readLoopRunning;

    //Transport is the real TCP transport layer; Dispose closes the TcpClient to make the read loop exit
    //Tests use QueueStream without setting Dispose, so no stream is closed
    private IDisposable? _transport;

    //RawPackets is the queue of raw payloads enqueued by the read loop, holding only packets from phases that need the main thread (configuration and play)
    //Handshake/status/login phases never enter this queue; the read thread decodes them directly, see ReadLoopHandlesPackets
    private readonly ConcurrentQueue<byte[]> _rawPackets = new();

    private INonGenericProtocol? _inboundProtocol;
    private INonGenericProtocol? _outboundProtocol;
    private PacketListener? _packetListener;

    //ReadLoopHandlesPackets reports whether packets of the current protocol phase are decoded directly by the read thread
    //Maps to vanilla, where the handshake/status/login phases are handled on the netty thread and only configuration/play are scheduled back to the main thread
    //These steps must run on their own before the main Tick loop starts (spawn pre-generation window), otherwise server list queries get no response
    //The phase advances one way: true from handshake, false once it switches to configuration, never flipping back, so the read thread always sees a value matching the protocol used for decoding
    private volatile bool _readLoopHandlesPackets;
    private PacketListener? _disconnectListener;
    private DisconnectionDetails? _disconnectionDetails;
    private bool _disconnectionHandled;
    private bool _sendLoginDisconnect = true;
    private int _receivedPackets;
    private int _sentPackets;

    //_sendQueue is the outbound packet queue; encoding, compression, encryption, and stream writes are all heavy work and are counted onto fixed write threads
    //The main thread only enqueues on Send, matching vanilla's behavior of putting these three steps on the netty thread
    //_flushQueued=1 means the connection is in the global drain queue or currently being drained, which preserves packet order and avoids duplicate submission
    private readonly ConcurrentQueue<PendingSend> _sendQueue = new();
    private int _flushQueued;
    private volatile bool _sendClosed;

    //PendingSend is a pending write item; the protocol and registry access are snapshotted at enqueue time
    //The protocol switches with Tick, so background threads must not read those mutable fields
    //A null Packet is a flush-only marker used so Flush can wait for prior packets to finish writing
    private readonly record struct PendingSend(
        INonGenericProtocol? Protocol, object? Packet, RegistryAccess Access, TaskCompletionSource? Flushed);

    //CompressionThreshold is the compression threshold; -1 disables it, and packet data is compressed only when its length exceeds the threshold
    public int CompressionThreshold { get; set; } = -1;

    //EncryptionEnabled indicates whether encryption is on; once enabled all reads and writes go through CryptoHelper
    public bool EncryptionEnabled { get; private set; }
    private byte[]? _encryptKey;
    private bool _disposed;

    //_encodeBuf is the outbound encode buffer, reused as a single instance for the whole connection
    //Only one drainer exists per connection at a time (see the _flushQueued CAS in Enqueue), so reuse never races across threads
    private RegistryFriendlyByteBuf? _encodeBuf;

    //RegistryAccess is the registry access, empty by default since the Login phase has no registry context
    //After entering the Configuration/Play phases the caller of SetupOutboundProtocol sets the real registryAccess
    public RegistryAccess RegistryAccess { get; set; } = RegistryAccess.Empty;

    //RemoteAddress is the client IP text, written by ConnectionAcceptor on accept
    //Tests assign it directly from an in-memory stream; when unset the IP ban check lets the connection through
    public string? RemoteAddress { get; set; }

    //The Connection constructor aligns with vanilla Connection(PacketFlow) but takes an explicit Stream
    public Connection(Stream readStream, Stream writeStream, PacketFlow receiving, Thread? runningThread = null)
    {
        _readStream = readStream;
        _writeStream = writeStream;
        _receiving = receiving;
        _processor = new PacketProcessor(runningThread);
    }

    //SetTransport sets the real TCP transport layer, closed on Dispose to make the read loop exit
    //ConnectionAcceptor passes in a TcpClient; tests using QueueStream do not call it
    internal void SetTransport(IDisposable transport) => _transport = transport;

    //StartReadLoop starts a background read thread looping on Receive until the stream ends or Dispose is called
    //In real TCP scenarios ConnectionAcceptor calls it after installing the listener
    //Tests using QueueStream do not call it and call Receive manually instead for compatibility
    //The read thread blocks on the Read inside Receive; Dispose closes the Transport, making Read throw and the thread exit
    public void StartReadLoop()
    {
        if (_readThread != null) return;
        _readLoopRunning = true;
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ConnectionReadLoop" };
        _readThread.Start();
    }

    //ReadLoop is the background read loop blocking on ReceiveRaw
    //Packets from the handshake/status/login phases are decoded directly on this thread, matching vanilla handling of these phases on the netty thread
    //Other phases only enqueue raw payloads for the main thread Tick to decode, avoiding the read loop misdecoding new-protocol packets with the old protocol
    //For example, when Handshake switches to Status, StatusRequest would be misdecoded by the read loop using the old Handshake protocol
    private void ReadLoop()
    {
        while (_readLoopRunning && !_disposed)
        {
            try
            {
                var payload = ReceiveRaw();
                if (payload == null)
                {
                    Disconnect(new DisconnectionDetails("remote closed the connection"));
                    break;
                }
                if (_readLoopHandlesPackets) DecodePayload(payload, direct: true);
                else _rawPackets.Enqueue(payload);
            }
            catch (IOException) { Disconnect(new DisconnectionDetails("connection IO error")); break; }
            catch (SocketException) { Disconnect(new DisconnectionDetails("connection socket error")); break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    //Receiving is the receive direction, aligns with vanilla getReceiving
    public PacketFlow Receiving => _receiving;

    //Sending is the send direction, aligns with vanilla getSending
    public PacketFlow Sending => _receiving.GetOpposite();

    //ReceivingDirection converts the receive direction to FlowDirection for comparison with PacketListener.Flow
    public FlowDirection ReceivingDirection
        => _receiving == PacketFlow.Serverbound ? FlowDirection.Serverbound : FlowDirection.Clientbound;

    //IsConnected indicates whether the connection is established, aligns with vanilla isConnected
    public bool IsConnected => !_disposed;

    //IsConnecting indicates whether the connection is in progress, aligns with vanilla isConnecting
    public bool IsConnecting => !_disposed && _inboundProtocol == null;

    //PacketListener is the current packet listener, aligns with vanilla getPacketListener
    public PacketListener? Listener => _packetListener;

    //DisconnectionDetails holds disconnection details
    public DisconnectionDetails? DisconnectionDetails => _disconnectionDetails;

    //SetupInboundProtocol configures the inbound protocol and listener, aligns with vanilla setupInboundProtocol
    public void SetupInboundProtocol<THandler>(ProtocolInfo<THandler> protocol, THandler listener)
        where THandler : class, PacketListener
    {
        ValidateListener(protocol, listener);
        _packetListener = listener;
        _disconnectListener = null;
        _inboundProtocol = protocol;
        _readLoopHandlesPackets = HandlesOnReadLoop(protocol.Id);
    }

    //SetupOutboundProtocol configures the outbound protocol, aligns with vanilla setupOutboundProtocol
    public void SetupOutboundProtocol(INonGenericProtocol protocol)
    {
        _outboundProtocol = protocol;
        _sendLoginDisconnect = protocol.Id == ConnectionProtocol.Login;
    }

    //SetInitialInboundProtocolInternal sets the initial inbound protocol and listener
    //Used by the Game layer extension method SetListenerForServerboundHandshake
    //Not exposed as public because the Game layer is responsible for business packet dependencies
    internal void SetInitialInboundProtocolInternal(PacketListener listener, INonGenericProtocol inboundProtocol)
    {
        if (_packetListener != null)
            throw new InvalidOperationException("listener already set");
        _packetListener = listener;
        _inboundProtocol = inboundProtocol;
        _readLoopHandlesPackets = HandlesOnReadLoop(inboundProtocol.Id);
    }

    //HandlesOnReadLoop indicates whether this protocol's packets are handled directly by the read thread
    //Vanilla handles the handshake/status/login phases on the netty thread; these three only validate and send/receive without touching world state
    //configuration and play access main-thread-exclusive world state, so they must be handed back to the main Tick
    private static bool HandlesOnReadLoop(ConnectionProtocol protocol)
        => protocol is ConnectionProtocol.Handshake or ConnectionProtocol.Status or ConnectionProtocol.Login;

    //DisconnectListenerInternal sets the disconnect listener for Game extension methods
    internal void SetDisconnectListenerInternal(PacketListener? listener) => _disconnectListener = listener;

    //Send sends a packet, aligns with vanilla send(Packet)
    //EncodePacket already writes packetId+payload internally, so packetId is not written again here
    //Uses RegistryFriendlyByteBuf so business codecs such as ItemStack can access the registry
    //Send only enqueues the packet; the background write thread performs encoding, compression, and writing
    //The calling thread is no longer held up by encoding and compressing large packets, matching vanilla's semantics of handing send to the netty thread
    public void Send<THandler>(Packet<THandler> packet) where THandler : class
    {
        //Silently drops packets when the connection is closed, matching vanilla where sending after disconnect just fails to write without interrupting the caller
        //The kick command disconnects the target first and then replies to itself, so throwing here would interrupt the whole command handling
        if (_disposed) return;
        if (_outboundProtocol == null)
            throw new InvalidOperationException("outbound protocol not configured");
        Enqueue(new PendingSend(_outboundProtocol, packet, RegistryAccess, null));
    }

    //Flush waits for all enqueued packets to be written, called before shutdown and test assertions
    //Returns false when it times out before finishing writing, so data may be incomplete
    public bool Flush(int timeoutMillis = 5000)
    {
        if (_sendClosed) return true;
        var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new PendingSend(null, null, RegistryAccess.Empty, flushed));
        return flushed.Task.Wait(timeoutMillis);
    }

    //Enqueue queues a pending write item and passes it through when the queue is closed, avoiding blocking the caller during shutdown
    private void Enqueue(PendingSend send)
    {
        if (_sendClosed)
        {
            send.Flushed?.TrySetResult();
            return;
        }
        _sendQueue.Enqueue(send);
        //Only submits after a successful CAS; a connection has a single drainer at a time to preserve packet order
        //A connection that has already been submitted is not re-added to the global queue; when broadcasting to thousands of connections this layer filters out the vast majority of submissions
        if (Interlocked.CompareExchange(ref _flushQueued, 1, 0) == 0)
            OutboundWriters.Mark(this);
    }

    //DrainOutbound drains the outbound queue, called by the global write thread; single-threaded serial consumption preserves packet order
    //A single packet failing to encode is dropped with a warning, so one bad packet does not take down the whole connection
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
            //Flushes the stream once after a full drain instead of once per packet; for both network and memory streams this is a no-op
            try { _writeStream.Flush(); }
            catch (Exception e) { Log.Warning($"Outbound stream flush failed {e.Message}"); }
            //After draining, clears the flag and rechecks to prevent new packets from being left in the queue when clearing interleaves with concurrent enqueue
            Volatile.Write(ref _flushQueued, 0);
            if (_sendQueue.IsEmpty) return;
            if (Interlocked.CompareExchange(ref _flushQueued, 1, 0) != 0) return;
        }
    }

    //EncodeAndWrite encodes, compresses, encrypts, and writes the stream in the same order as the original synchronous implementation, just moved to the write thread
    //The encode buffer is reused per connection and the length prefix is assembled on the stack; both used to allocate a whole set of buffer objects per packet
    private void EncodeAndWrite(INonGenericProtocol protocol, object packet, RegistryAccess access)
    {
        var buf = _encodeBuf ??= new RegistryFriendlyByteBuf(access);
        buf.RegistryAccess = access;
        buf.Reset();
        protocol.EncodePacket(buf, packet);

        //Uncompressed and unencrypted is the norm for LAN and tests; this path writes the reused buffer content out directly
        //It neither copies an intermediate array nor allocates a separate buffer for the few bytes of the length prefix
        if (CompressionThreshold < 0 && !(EncryptionEnabled && _encryptKey != null))
        {
            WriteLengthPrefix(buf.Length);
            buf.WriteTo(_writeStream);
            Interlocked.Increment(ref _sentPackets);
            return;
        }

        //Compression and encryption need the full bytes, so a copy is still required here
        byte[] payload = buf.ToArray();
        if (CompressionThreshold >= 0)
            payload = CompressionHelper.CompressIfNeeded(payload, CompressionThreshold);
        if (EncryptionEnabled && _encryptKey != null)
            payload = CryptoHelper.Encrypt(payload, _encryptKey);
        WriteLengthPrefix(payload.Length);
        _writeStream.Write(payload);
        Interlocked.Increment(ref _sentPackets);
    }

    //WriteLengthPrefix writes the VarInt length prefix, assembling bytes directly on the stack
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

    //SendAll sends packets in bulk, aligns with vanilla sendAll
    public void SendAll<THandler>(IEnumerable<Packet<THandler>> packets) where THandler : class
    {
        foreach (var p in packets) Send(p);
    }

    //Receive blocks, reads one packet, and immediately decodes and dispatches it to PacketProcessor
    //Aligns with vanilla channelRead0: after decoding it calls genericsFtw, which invokes packet.handle(listener)
    //Tests call it manually; in real TCP scenarios ReadLoop enqueues and Tick decodes
    //Returns false when the stream has ended and no packet can be read
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

    //ReceiveRaw reads one raw packet and returns the payload without decoding
    //Reads the VarInt length prefix + payload of the given length, decrypts and decompresses it, then returns
    //Returns null when the stream has ended and no packet can be read
    private byte[]? ReceiveRaw()
    {
        //Reads the VarInt length prefix, 1-5 bytes until MSB=0
        int length = 0;
        int shift = 0;
        int b;
        do
        {
            b = _readStream.ReadByte();
            if (b < 0) return null;
            length |= (b & 0x7F) << shift;
            shift += 7;
            if (shift > 35) throw new IOException($"VarInt length prefix too long");
        } while ((b & 0x80) != 0);

        if (length <= 0 || length > 0x200000)
            throw new IOException($"invalid packet length {length}");
        byte[] payload = new byte[length];
        if (!ReadFill(payload, length)) return null;
        if (EncryptionEnabled && _encryptKey != null)
            payload = CryptoHelper.Decrypt(payload, _encryptKey);
        if (CompressionThreshold >= 0)
            payload = CompressionHelper.Decompress(payload);
        return payload;
    }

    //DecodePayload decodes a single payload and dispatches it to the listener
    //direct=true means the caller is the read thread and the current phase allows it, so it is handled on this thread instead of being queued to the main thread
    //A decode failure drops the packet without throwing so the read loop does not exit
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
            //Includes the packet network ID to locate which packet failed to decode; PeekVarInt itself never throws
            //Also includes the remote address and current inbound protocol; the former distinguishes player connections from external probe traffic, the latter distinguishes protocol phases
            //Finally includes the first few payload bytes: when non-protocol traffic hits this port, the leading bytes make it recognizable
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

    //HexPrefix takes the hex of the first 16 payload bytes
    //When diagnosing non-protocol traffic hitting this port (scanners/other plaintext services), the leading bytes make it recognizable
    private static string HexPrefix(byte[] payload)
    {
        var count = Math.Min(16, payload.Length);
        return count == 0 ? "" : Convert.ToHexString(payload, 0, count);
    }

    //DecodeRawPackets decodes all raw packets in the _rawPackets queue
    //Processes queued packets immediately after decoding one, ensuring a protocol switch takes effect before the next packet is decoded
    //Avoids the race where StatusRequest is still decoded with the old protocol after a Handshake packet switches to the Status protocol
    private void DecodeRawPackets()
    {
        while (_rawPackets.TryDequeue(out var payload))
        {
            DecodePayload(payload);
            //Immediate processing lets the protocol switch take effect before decoding the next packet
            _processor.ProcessQueuedPackets();
        }
    }

    //Tick is called every frame, aligns with vanilla tick
    //First DecodeRawPackets decodes raw packets enqueued by the read loop and processes them, triggering any protocol switch
    //Then FlushQueue runs pending actions, and finally queued packets and disconnections are handled as a fallback
    //Finally calls TickablePacketListener.tick, aligning with the listener tick in vanilla Connection.tick
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

    //RunOnceConnected runs an action once the connection is established, aligns with vanilla runOnceConnected
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

    //FlushQueue runs all pending actions, aligns with vanilla flushQueue
    private void FlushQueue()
    {
        while (_pendingActions.TryDequeue(out var action))
            action(this);
    }

    //Disconnect disconnects the connection, aligns with vanilla disconnect
    public void Disconnect(string reason)
        => Disconnect(new DisconnectionDetails(reason));

    //Disconnect disconnects the connection, aligns with vanilla disconnect(DisconnectionDetails)
    public void Disconnect(DisconnectionDetails details)
    {
        _disconnectionDetails = details;
        _disposed = true;
    }

    //HandleDisconnection handles the disconnection event, aligns with vanilla handleDisconnection
    public void HandleDisconnection()
    {
        if (_disconnectionHandled) return;
        _disconnectionHandled = true;
        var listener = _packetListener ?? _disconnectListener;
        if (listener == null) return;
        var details = _disconnectionDetails ?? new DisconnectionDetails("connection closed");
        listener.OnDisconnect(details.Reason);
    }

    //EnableEncryption enables AES encryption, aligns with vanilla setEncryptionKey
    public void EnableEncryption(byte[] key)
    {
        _encryptKey = key;
        EncryptionEnabled = true;
    }

    //SetupCompression enables compression, aligns with vanilla setupCompression
    public void SetupCompression(int threshold)
    {
        CompressionThreshold = threshold;
    }

    //ValidateListener checks that the listener direction and protocol match the inbound protocol
    private void ValidateListener<THandler>(INonGenericProtocol protocol, THandler listener)
        where THandler : class, PacketListener
    {
        ArgumentNullException.ThrowIfNull(listener);
        if (listener.Flow != ReceivingDirection)
            throw new InvalidOperationException($"listener direction {listener.Flow} does not match connection receive direction {ReceivingDirection}");
        if (protocol.Id != listener.Protocol)
            throw new InvalidOperationException($"listener protocol {listener.Protocol} does not match inbound protocol {protocol.Id}");
    }

    //ReadFill reads the specified number of bytes from the underlying stream into buffer, returns false if the stream ends early
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
        //Writes enqueued packets out before tearing down the queue, so shutdown or kick packets are not lost to asynchrony
        Flush();
        _sendClosed = true;
        //Closes the Transport so the read loop's blocking Read throws IOException and the thread exits
        //Tests have no Transport, which does not affect QueueStream and is managed by the tests themselves
        _transport?.Dispose();
        _processor.Dispose();
        _packetListener = null;
        _inboundProtocol = null;
        _outboundProtocol = null;
        _encodeBuf?.Dispose();
        _encodeBuf = null;
    }
}

//OutboundWriters is the global outbound drain scheduler, maps to the netty eventLoop write thread in vanilla
//Previously every send submitted a task to the thread pool; broadcasting to thousands of connections contended on the scheduler lock and cost the main thread tens of microseconds per packet
//Changed to a few fixed write threads consuming the global drain queue, so a single Send on the main thread is just one enqueue plus one CAS
internal static class OutboundWriters
{
    private static readonly ConcurrentQueue<Connection> Dirty = new();
    private static readonly SemaphoreSlim Signal = new(0);
    //The write thread count is half the core count; encoding and stream writes mix CPU and IO, and too much concurrency just makes threads steal cores from each other
    private static readonly int WorkerCount = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

    static OutboundWriters()
    {
        for (var i = 0; i < WorkerCount; i++)
        {
            var thread = new Thread(Loop) { IsBackground = true, Name = $"NetCraftOutbound{i}" };
            thread.Start();
        }
    }

    //Mark adds the connection to the drain queue and wakes a write thread
    public static void Mark(Connection connection)
    {
        Dirty.Enqueue(connection);
        Signal.Release();
    }

    //Loop takes one connection and drains it, waiting on the signal when none is available
    //When the signal count exceeds actual connections it spins a few rounds at most, neither busy-waiting nor missing a wakeup
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
