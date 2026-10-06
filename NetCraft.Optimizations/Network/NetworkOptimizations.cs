using NetCraft.Config;

namespace NetCraft.Optimizations.Network;

//Network optimization module, covers core optimization points 2.6 / 2.7
//2.6 StreamCodec static dispatch is integrated in the sealed FuncCodec in NetCraft.Network/StreamCodec.cs
//The .NET JIT devirtualizes virtual calls on sealed classes, equivalent to static dispatch
//2.7 VarInt BitOperations Span bulk writes are integrated in NetCraft.Network/FriendlyByteBuf.cs
public static class NetworkOptimizations
{
    public const string ModuleName = "Network Optimization";
    public const string TargetSubsystem = "NetCraft.Network";

    //Optimization point 2.6, StreamCodec uses a sealed class + JIT devirtualization as a static dispatch equivalent
    //When the toggle is on, FuncCodec is sealed and triggers JIT devirtualization
    public static bool IsStreamCodecStaticDispatchEnabled => OptimizationFlags.StreamCodecStaticDispatch;

    //Optimization point 2.7, VarInt writes use Span bulk writes to avoid repeated _writer.Write calls
    //When the toggle is on, FriendlyByteBuf.WriteVarInt/WriteVarLong are Span based
    public static bool IsVarIntBitOperationsEnabled => OptimizationFlags.VarIntBitOperations;

    //Optimization point 2.6, packet byte buffers are pooled with ArrayPool
    //When the toggle is on, the underlying MemoryStream of FriendlyByteBuf can go through the ArrayPool path
    public static bool IsPacketBufferPooledEnabled => OptimizationFlags.PacketBufferPooled;

    //IsOptimized checks whether all three toggles are on to decide if Network optimization is enabled
    public static bool IsOptimized =>
        IsStreamCodecStaticDispatchEnabled && IsVarIntBitOperationsEnabled && IsPacketBufferPooledEnabled;

    //GetStats returns the Network optimization stats for diagnostics
    public static NetworkOptimizationStats GetStats() => new(
        StreamCodecStaticDispatch: IsStreamCodecStaticDispatchEnabled,
        VarIntBitOperations: IsVarIntBitOperationsEnabled,
        PacketBufferPooled: IsPacketBufferPooledEnabled,
        IsOptimized: IsOptimized);
}

//Network optimization stats snapshot
public readonly record struct NetworkOptimizationStats(
    bool StreamCodecStaticDispatch,
    bool VarIntBitOperations,
    bool PacketBufferPooled,
    bool IsOptimized);
