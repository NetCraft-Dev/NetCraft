using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace NetCraft.Gpu.Vulkan;

//VulkanCommandEncoder Vulkan 后端命令编码器实现 ICommandEncoder
//封装 VkCommandBuffer 录制 copy/render pass 命令 Submit 提交 GPU 队列
//替代旧 VulkanCommandBuffer 的混合录制分离命令编码和渲染通道
//4.3 改造 CreateRenderPass 走 dynamic rendering 传 colorImage/depthImage 的 ImageView 给 VulkanRenderPass
public sealed unsafe class VulkanCommandEncoder : ICommandEncoder
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanGpuDevice _gpuDevice;
    private readonly CommandPool _commandPool;
    private readonly Queue _graphicsQueue;
    //DynRenderingExt KHR_dynamic_rendering 扩展实例传给 VulkanRenderPass 调 CmdBeginRendering
    private readonly KhrDynamicRendering _dynRenderingExt;
    private readonly CommandBuffer _handle;
    private readonly Fence _submitFence;
    //WriteToTexture 创建的 staging buffer 生命周期延到 Submit 后统一释放
    //Submit 调 WaitForFences 等 GPU 执行完才能安全释放 staging buffer
    private readonly List<VulkanBuffer> _stagingBuffers = new();
    //_pendingStagingBuffers SubmitAsync 时 staging 转入待释放列表等 WaitForCompletion 释放
    //异步 Submit 不能立即释放 staging GPU 还在读需等 fence 完成
    private readonly List<VulkanBuffer> _pendingStagingBuffers = new();
    //_pendingSubmit SubmitAsync 后未 WaitForCompletion 标记 BeginRecording 前必须先 WaitForCompletion
    private bool _pendingSubmit;
    private bool _disposed;
    private bool _recording;

    internal VulkanCommandEncoder(Vk vk, Device device, VulkanGpuDevice gpuDevice, CommandPool commandPool, Queue graphicsQueue, KhrDynamicRendering dynRenderingExt)
    {
        _vk = vk;
        _device = device;
        _gpuDevice = gpuDevice;
        _commandPool = commandPool;
        _graphicsQueue = graphicsQueue;
        _dynRenderingExt = dynRenderingExt;
        //分配 command buffer
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        if (_vk.AllocateCommandBuffers(_device, &allocInfo, out _handle) != Result.Success)
            throw new InvalidOperationException("命令缓冲分配失败");
        //创建 submit fence
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        if (_vk.CreateFence(_device, &fenceInfo, null, out _submitFence) != Result.Success)
            throw new InvalidOperationException("Submit fence 创建失败");
        //开始录制
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        if (_vk.BeginCommandBuffer(_handle, &beginInfo) != Result.Success)
            throw new InvalidOperationException("命令缓冲开始录制失败");
        _recording = true;
    }

    public IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor)
    {
        EnsureRecording();
        if (colorImage is not VulkanImage vkColor)
            throw new ArgumentException("colorImage 必须是 VulkanImage", nameof(colorImage));
        return new VulkanRenderPass(_vk, _dynRenderingExt, _handle, pipeline, vkColor.View, clearColor, null, 0f);
    }

    public IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor, GpuImage depthImage, float clearDepth)
    {
        EnsureRecording();
        if (colorImage is not VulkanImage vkColor)
            throw new ArgumentException("colorImage 必须是 VulkanImage", nameof(colorImage));
        return new VulkanRenderPass(_vk, _dynRenderingExt, _handle, pipeline, vkColor.View, clearColor, depthImage, clearDepth);
    }

    public IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor, GpuImage depthImage, float clearDepth, GpuLoadOp colorLoadOp)
    {
        EnsureRecording();
        if (colorImage is not VulkanImage vkColor)
            throw new ArgumentException("colorImage 必须是 VulkanImage", nameof(colorImage));
        var vkLoadOp = colorLoadOp switch
        {
            GpuLoadOp.Clear => AttachmentLoadOp.Clear,
            GpuLoadOp.Load => AttachmentLoadOp.Load,
            _ => AttachmentLoadOp.Clear
        };
        return new VulkanRenderPass(_vk, _dynRenderingExt, _handle, pipeline, vkColor.View, clearColor, vkLoadOp, depthImage, clearDepth);
    }

    public void CopyBuffer(GpuBuffer src, GpuBuffer dst, ulong srcOffset, ulong dstOffset, ulong size)
    {
        EnsureRecording();
        if (src is not VulkanBuffer vkSrc) throw new ArgumentException("src 必须是 VulkanBuffer", nameof(src));
        if (dst is not VulkanBuffer vkDst) throw new ArgumentException("dst 必须是 VulkanBuffer", nameof(dst));
        var region = new BufferCopy
        {
            SrcOffset = srcOffset,
            DstOffset = dstOffset,
            Size = size
        };
        _vk.CmdCopyBuffer(_handle, vkSrc.Handle, vkDst.Handle, 1, &region);
    }

    //WriteToTexture 录制像素上传命令到当前 command buffer 走 staging buffer 中转
    //与 VulkanImage.UploadRegion 区别 不立即 Submit 而是录制到当前 cmd 可与其他命令批量提交
    //staging buffer 生命周期延到 Submit 后统一释放 Submit 调 WaitForFences 保证 GPU 已读完
    //layout 转换 currentLayout→TransferDstOptimal→拷贝→ShaderReadOnlyOptimal
    public void WriteToTexture(GpuImage dst, ReadOnlySpan<byte> data, int dstX, int dstY, int width, int height)
    {
        EnsureRecording();
        if (dst is not VulkanImage vkDst)
            throw new ArgumentException("dst 必须是 VulkanImage", nameof(dst));
        if (vkDst.Usage == GpuImageUsage.DepthAttachment)
            throw new InvalidOperationException("DepthAttachment 不支持 WriteToTexture");
        var staging = (VulkanBuffer)_gpuDevice.CreateBuffer(data.Length, GpuBufferUsage.StagingBuffer);
        staging.Upload(data.ToArray());
        _stagingBuffers.Add(staging);
        vkDst.TransitionLayout(_handle, ImageLayout.TransferDstOptimal);
        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = (uint)width,
            BufferImageHeight = (uint)height,
            ImageSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new Offset3D { X = dstX, Y = dstY, Z = 0 },
            ImageExtent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 }
        };
        _vk.CmdCopyBufferToImage(_handle, staging.Handle, vkDst.Handle, ImageLayout.TransferDstOptimal, 1, &region);
        vkDst.TransitionLayout(_handle, ImageLayout.ShaderReadOnlyOptimal);
    }

    //TransitionImageLayout 录制图像布局转换到当前 command buffer
    //PIP offscreen 渲染后 ColorAttachmentOptimal→ShaderReadOnlyOptimal 供 blit 采样
    //跨帧复用下帧渲染前 ShaderReadOnlyOptimal→ColorAttachmentOptimal VulkanImage.TransitionLayout 已处理跳过
    public void TransitionImageLayout(GpuImage image, GpuImageLayout newLayout)
    {
        EnsureRecording();
        if (image is not VulkanImage vkImg)
            throw new ArgumentException("image 必须是 VulkanImage", nameof(image));
        vkImg.TransitionLayout(_handle, ToVkLayout(newLayout));
    }

    private static ImageLayout ToVkLayout(GpuImageLayout layout) => layout switch
    {
        GpuImageLayout.ColorAttachment => ImageLayout.ColorAttachmentOptimal,
        GpuImageLayout.ShaderReadOnly => ImageLayout.ShaderReadOnlyOptimal,
        GpuImageLayout.TransferDst => ImageLayout.TransferDstOptimal,
        GpuImageLayout.TransferSrc => ImageLayout.TransferSrcOptimal,
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    public void Submit()
    {
        if (!_recording) return;
        if (_vk.EndCommandBuffer(_handle) != Result.Success)
            throw new InvalidOperationException("命令缓冲结束录制失败");
        _recording = false;
        var cmd = _handle;
        var fence = _submitFence;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1
        };
        submitInfo.PCommandBuffers = &cmd;
        if (_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, fence) != Result.Success)
            throw new InvalidOperationException("QueueSubmit 失败");
        _vk.WaitForFences(_device, 1, &fence, Vk.True, ulong.MaxValue);
        _vk.ResetFences(_device, 1, &fence);
        //Submit 已等 fence 完成 GPU 读完 staging buffer 可安全释放
        foreach (var sb in _stagingBuffers) sb.Dispose();
        _stagingBuffers.Clear();
    }

    //SubmitAsync 提交命令到 GPU 队列不等完成供 PIP 双缓冲异步渲染
    //staging buffer 转入 _pendingStagingBuffers 延迟到 WaitForCompletion 释放
    //调用方下次复用本 encoder 前必须 WaitForCompletion 保证 GPU 完成才能 Reset command buffer
    public void SubmitAsync()
    {
        if (!_recording) return;
        if (_vk.EndCommandBuffer(_handle) != Result.Success)
            throw new InvalidOperationException("命令缓冲结束录制失败");
        _recording = false;
        var cmd = _handle;
        var fence = _submitFence;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1
        };
        submitInfo.PCommandBuffers = &cmd;
        if (_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, fence) != Result.Success)
            throw new InvalidOperationException("QueueSubmit 失败");
        _pendingSubmit = true;
        _pendingStagingBuffers.AddRange(_stagingBuffers);
        _stagingBuffers.Clear();
    }

    //WaitForCompletion 等 SubmitAsync 提交的 GPU 命令完成
    //未 SubmitAsync 过（_pendingSubmit=false）直接返回首次复用 encoder 安全跳过
    //完成后 ResetFences 让 fence 可供下次 QueueSubmit 复用并释放待释放 staging
    public void WaitForCompletion()
    {
        if (!_pendingSubmit) return;
        var fence = _submitFence;
        _vk.WaitForFences(_device, 1, &fence, Vk.True, ulong.MaxValue);
        _vk.ResetFences(_device, 1, &fence);
        foreach (var sb in _pendingStagingBuffers) sb.Dispose();
        _pendingStagingBuffers.Clear();
        _pendingSubmit = false;
    }

    //BeginRecording 重新开始命令录制供 encoder 跨帧复用
    //首次调用幂等（构造已 BeginCommandBuffer）_recording=true 直接返回
    //后续调用需先 WaitForCompletion 保证 GPU 不再使用 command buffer 再 Reset+Begin
    public void BeginRecording()
    {
        if (_recording) return;
        if (_pendingSubmit)
            throw new InvalidOperationException("SubmitAsync 后未 WaitForCompletion 不能 BeginRecording");
        _vk.ResetCommandBuffer(_handle, CommandBufferResetFlags.None);
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        if (_vk.BeginCommandBuffer(_handle, &beginInfo) != Result.Success)
            throw new InvalidOperationException("命令缓冲开始录制失败");
        _recording = true;
    }

    private void EnsureRecording()
    {
        if (!_recording) throw new InvalidOperationException("CommandEncoder 已 Submit 不能再录制");
    }

    public void Dispose()
    {
        if (_disposed) return;
        //异步 Submit 未等完成时先等 fence 保证 GPU 不再使用 command buffer 再 Free
        if (_pendingSubmit)
        {
            var waitFence = _submitFence;
            _vk.WaitForFences(_device, 1, &waitFence, Vk.True, ulong.MaxValue);
        }
        foreach (var sb in _stagingBuffers) sb.Dispose();
        _stagingBuffers.Clear();
        foreach (var sb in _pendingStagingBuffers) sb.Dispose();
        _pendingStagingBuffers.Clear();
        var cmd = _handle;
        _vk.FreeCommandBuffers(_device, _commandPool, 1, &cmd);
        var fence = _submitFence;
        _vk.DestroyFence(_device, fence, null);
        _disposed = true;
    }
}
