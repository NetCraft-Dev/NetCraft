using NetCraft.Config;

namespace NetCraft.Optimizations.Network;

//Network 优化模块对应核心优化点 2.6 / 2.7
//2.6 StreamCodec 静态分发集成于 NetCraft.Network/StreamCodec.cs 的 sealed FuncCodec
//.NET JIT 对 sealed 类的虚方法调用做去虚化等价静态分发效果
//2.7 VarInt BitOperations Span 批量写入集成于 NetCraft.Network/FriendlyByteBuf.cs
public static class NetworkOptimizations
{
    public const string ModuleName = "Network Optimization";
    public const string TargetSubsystem = "NetCraft.Network";

    //对应优化点 2.6 StreamCodec 用 sealed 类 + JIT 去虚化等价静态分发
    //开关启用表示 FuncCodec 已 sealed 化触发 JIT devirtualization
    public static bool IsStreamCodecStaticDispatchEnabled => OptimizationFlags.StreamCodecStaticDispatch;

    //对应优化点 2.7 VarInt 写入用 Span 批量写入避免多次 _writer.Write 调用
    //开关启用表示 FriendlyByteBuf.WriteVarInt/WriteVarLong 已 Span 化
    public static bool IsVarIntBitOperationsEnabled => OptimizationFlags.VarIntBitOperations;

    //对应优化点 2.6 packet 字节缓冲用 ArrayPool 池化
    //开关启用表示 FriendlyByteBuf 底层 MemoryStream 可走 ArrayPool 池化路径
    public static bool IsPacketBufferPooledEnabled => OptimizationFlags.PacketBufferPooled;

    //IsOptimized 检查三开关是否全开判断 Network 优化是否启用
    public static bool IsOptimized =>
        IsStreamCodecStaticDispatchEnabled && IsVarIntBitOperationsEnabled && IsPacketBufferPooledEnabled;

    //GetStats 返回 Network 优化统计信息用于诊断
    public static NetworkOptimizationStats GetStats() => new(
        StreamCodecStaticDispatch: IsStreamCodecStaticDispatchEnabled,
        VarIntBitOperations: IsVarIntBitOperationsEnabled,
        PacketBufferPooled: IsPacketBufferPooledEnabled,
        IsOptimized: IsOptimized);
}

//Network 优化统计快照
public readonly record struct NetworkOptimizationStats(
    bool StreamCodecStaticDispatch,
    bool VarIntBitOperations,
    bool PacketBufferPooled,
    bool IsOptimized);
