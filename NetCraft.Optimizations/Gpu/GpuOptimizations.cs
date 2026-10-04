using NetCraft.Config;

namespace NetCraft.Optimizations.Gpu;

//Gpu 优化模块对应 Vulkan 后端相关优化（[C#内核重写计划.md] 第四节）
//NetCraft.Gpu 子系统尚未实现本模块仅暴露开关查询 API
//待 Vulkan 后端就绪后填充实际集成
public static class GpuOptimizations
{
    public const string ModuleName = "GPU Backend Optimization";
    public const string TargetSubsystem = "NetCraft.Gpu";

    //对应 Vulkan 命令缓冲用 ObjectPool 池化
    //开关启用表示命令缓冲分配将走 ObjectPool 路径避免 GC
    public static bool IsVulkanCommandBufferPooledEnabled => OptimizationFlags.VulkanCommandBufferPooled;

    //对应 Vulkan 资源上传用 Span 直接 memcpy
    //开关启用表示资源上传路径将走 Span 零拷贝
    public static bool IsVulkanResourceUploadSpanEnabled => OptimizationFlags.VulkanResourceUploadSpan;

    //对应 Vulkan 多线程命令录制每线程独立 CommandPool
    //开关启用表示命令录制将走每线程独立 CommandPool 路径
    public static bool IsVulkanMultiThreadedRecordingEnabled => OptimizationFlags.VulkanMultiThreadedRecording;

    //对应 framegraph 自动屏障插入
    //开关启用表示 framegraph 将走自动屏障插入路径
    public static bool IsFrameGraphAutoBarrierEnabled => OptimizationFlags.FrameGraphAutoBarrier;

    //对应 shaderc SPIR-V 编译产物磁盘缓存
    //开关启用表示 shader 编译将走磁盘缓存路径
    public static bool IsSpirvCacheDiskEnabled => OptimizationFlags.SpirvCacheDisk;

    //IsOptimized 检查五开关是否全开判断 Gpu 优化是否启用
    public static bool IsOptimized =>
        IsVulkanCommandBufferPooledEnabled
        && IsVulkanResourceUploadSpanEnabled
        && IsVulkanMultiThreadedRecordingEnabled
        && IsFrameGraphAutoBarrierEnabled
        && IsSpirvCacheDiskEnabled;

    //GetStats 返回 Gpu 优化统计信息用于诊断
    public static GpuOptimizationStats GetStats() => new(
        VulkanCommandBufferPooled: IsVulkanCommandBufferPooledEnabled,
        VulkanResourceUploadSpan: IsVulkanResourceUploadSpanEnabled,
        VulkanMultiThreadedRecording: IsVulkanMultiThreadedRecordingEnabled,
        FrameGraphAutoBarrier: IsFrameGraphAutoBarrierEnabled,
        SpirvCacheDisk: IsSpirvCacheDiskEnabled,
        IsOptimized: IsOptimized);
}

//Gpu 优化统计快照
public readonly record struct GpuOptimizationStats(
    bool VulkanCommandBufferPooled,
    bool VulkanResourceUploadSpan,
    bool VulkanMultiThreadedRecording,
    bool FrameGraphAutoBarrier,
    bool SpirvCacheDisk,
    bool IsOptimized);
