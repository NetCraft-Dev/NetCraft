using NetCraft.Config;

namespace NetCraft.Optimizations.Gpu;

//Gpu optimization module, covers the Vulkan backend optimizations (section 4 of the C# kernel rewrite plan)
//The NetCraft.Gpu subsystem is not implemented yet, this module only exposes the toggle query API
//The actual integration lands once the Vulkan backend is ready
public static class GpuOptimizations
{
    public const string ModuleName = "GPU Backend Optimization";
    public const string TargetSubsystem = "NetCraft.Gpu";

    //Vulkan command buffers are pooled with ObjectPool
    //When the toggle is on, command buffer allocation goes through the ObjectPool path and avoids GC
    public static bool IsVulkanCommandBufferPooledEnabled => OptimizationFlags.VulkanCommandBufferPooled;

    //Vulkan resource uploads use Span and a direct memcpy
    //When the toggle is on, resource uploads go through the zero-copy Span path
    public static bool IsVulkanResourceUploadSpanEnabled => OptimizationFlags.VulkanResourceUploadSpan;

    //Vulkan multithreaded command recording, one CommandPool per thread
    //When the toggle is on, command recording goes through the per-thread CommandPool path
    public static bool IsVulkanMultiThreadedRecordingEnabled => OptimizationFlags.VulkanMultiThreadedRecording;

    //Automatic barrier insertion in the framegraph
    //When the toggle is on, the framegraph goes through the automatic barrier insertion path
    public static bool IsFrameGraphAutoBarrierEnabled => OptimizationFlags.FrameGraphAutoBarrier;

    //On-disk cache for shaderc SPIR-V compilation output
    //When the toggle is on, shader compilation goes through the on-disk cache path
    public static bool IsSpirvCacheDiskEnabled => OptimizationFlags.SpirvCacheDisk;

    //IsOptimized checks whether all five toggles are on to decide if Gpu optimization is enabled
    public static bool IsOptimized =>
        IsVulkanCommandBufferPooledEnabled
        && IsVulkanResourceUploadSpanEnabled
        && IsVulkanMultiThreadedRecordingEnabled
        && IsFrameGraphAutoBarrierEnabled
        && IsSpirvCacheDiskEnabled;

    //GetStats returns the Gpu optimization stats for diagnostics
    public static GpuOptimizationStats GetStats() => new(
        VulkanCommandBufferPooled: IsVulkanCommandBufferPooledEnabled,
        VulkanResourceUploadSpan: IsVulkanResourceUploadSpanEnabled,
        VulkanMultiThreadedRecording: IsVulkanMultiThreadedRecordingEnabled,
        FrameGraphAutoBarrier: IsFrameGraphAutoBarrierEnabled,
        SpirvCacheDisk: IsSpirvCacheDiskEnabled,
        IsOptimized: IsOptimized);
}

//Gpu optimization stats snapshot
public readonly record struct GpuOptimizationStats(
    bool VulkanCommandBufferPooled,
    bool VulkanResourceUploadSpan,
    bool VulkanMultiThreadedRecording,
    bool FrameGraphAutoBarrier,
    bool SpirvCacheDisk,
    bool IsOptimized);
