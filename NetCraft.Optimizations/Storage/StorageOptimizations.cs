using NetCraft.Config;

namespace NetCraft.Optimizations.Storage;

//Storage 优化模块对应核心优化点 2.8 / 2.11
//2.8 RegionFile MemoryMapped 读取集成于 NetCraft.Storage/RegionFile.cs 的 GetChunkDataInputStreamWithMemoryMapped
//2.11 IOWorker Channels 异步管道集成于 NetCraft.Storage/IOWorker.cs 的 ChannelConsumeLoop
//借鉴 C2ME 重写 ChunkIoWorker 方案已验证
public static class StorageOptimizations
{
    public const string ModuleName = "Storage IO Optimization";
    public const string TargetSubsystem = "NetCraft.Storage";

    //对应优化点 2.8 RegionFile 读取走 MemoryMappedFile 视图
    //开关启用时 RegionFileStorage.OpenChunkInputStream 选择 MMF 入口
    public static bool IsRegionFileMemoryMappedEnabled => OptimizationFlags.RegionFileMemoryMapped;

    //对应优化点 2.11 IOWorker Background 任务派发走 System.Threading.Channels
    //开关启用时 IOWorker 构造 Channel 并启动消费循环
    public static bool IsIoWorkerChannelsEnabled => OptimizationFlags.IoWorkerChannels;

    //对应优化点 2.8 chunk 序列化零拷贝 Span 写入
    //NbtIo 的 BinaryNbtWriter 已用 Span<byte> 直接写入 BinaryWriter 底层
    public static bool IsChunkSerializeZeroCopyEnabled => OptimizationFlags.ChunkSerializeZeroCopy;

    //IsOptimized 检查三个开关是否全开判断 Storage 优化是否启用
    public static bool IsOptimized =>
        IsRegionFileMemoryMappedEnabled && IsIoWorkerChannelsEnabled && IsChunkSerializeZeroCopyEnabled;

    //GetStats 返回 Storage 优化统计信息用于诊断
    public static StorageOptimizationStats GetStats() => new(
        RegionFileMemoryMapped: IsRegionFileMemoryMappedEnabled,
        IoWorkerChannels: IsIoWorkerChannelsEnabled,
        ChunkSerializeZeroCopy: IsChunkSerializeZeroCopyEnabled,
        IsOptimized: IsOptimized);
}

//Storage 优化统计快照
public readonly record struct StorageOptimizationStats(
    bool RegionFileMemoryMapped,
    bool IoWorkerChannels,
    bool ChunkSerializeZeroCopy,
    bool IsOptimized);
