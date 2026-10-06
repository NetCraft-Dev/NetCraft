using NetCraft.Config;

namespace NetCraft.Optimizations.Storage;

//Storage optimization module, covers core optimization points 2.8 / 2.11
//2.8 RegionFile MemoryMapped reads are integrated in GetChunkDataInputStreamWithMemoryMapped in NetCraft.Storage/RegionFile.cs
//2.11 IOWorker Channels async pipeline is integrated in ChannelConsumeLoop in NetCraft.Storage/IOWorker.cs
//Borrows from the C2ME ChunkIoWorker rewrite, already validated
public static class StorageOptimizations
{
    public const string ModuleName = "Storage IO Optimization";
    public const string TargetSubsystem = "NetCraft.Storage";

    //Optimization point 2.8, RegionFile reads go through a MemoryMappedFile view
    //When the toggle is on, RegionFileStorage.OpenChunkInputStream picks the MMF entry point
    public static bool IsRegionFileMemoryMappedEnabled => OptimizationFlags.RegionFileMemoryMapped;

    //Optimization point 2.11, IOWorker background task dispatch goes through System.Threading.Channels
    //When the toggle is on, IOWorker builds a Channel and starts the consume loop
    public static bool IsIoWorkerChannelsEnabled => OptimizationFlags.IoWorkerChannels;

    //Optimization point 2.8, zero-copy Span writes for chunk serialization
    //BinaryNbtWriter in NbtIo already uses Span<byte> to write directly into the underlying BinaryWriter
    public static bool IsChunkSerializeZeroCopyEnabled => OptimizationFlags.ChunkSerializeZeroCopy;

    //IsOptimized checks whether all three toggles are on to decide if Storage optimization is enabled
    public static bool IsOptimized =>
        IsRegionFileMemoryMappedEnabled && IsIoWorkerChannelsEnabled && IsChunkSerializeZeroCopyEnabled;

    //GetStats returns the Storage optimization stats for diagnostics
    public static StorageOptimizationStats GetStats() => new(
        RegionFileMemoryMapped: IsRegionFileMemoryMappedEnabled,
        IoWorkerChannels: IsIoWorkerChannelsEnabled,
        ChunkSerializeZeroCopy: IsChunkSerializeZeroCopyEnabled,
        IsOptimized: IsOptimized);
}

//Storage optimization stats snapshot
public readonly record struct StorageOptimizationStats(
    bool RegionFileMemoryMapped,
    bool IoWorkerChannels,
    bool ChunkSerializeZeroCopy,
    bool IsOptimized);
