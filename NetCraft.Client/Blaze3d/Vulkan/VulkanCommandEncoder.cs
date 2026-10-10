using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Pipeline;
namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanCommandEncoder Vulkan backend command encoder implementing CommandEncoderBackend
//Wraps VkCommandBuffer to record copy/clear/render pass commands; Submit submits to the GPU queue
//Render passes are created from a RenderPassDescriptor and bind resources by declared names
public sealed unsafe class VulkanCommandEncoder : CommandEncoderBackend
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanDevice _gpuDevice;
    private readonly CommandPool _commandPool;
    private readonly Queue _graphicsQueue;
    //DynRenderingExt the KHR_dynamic_rendering extension instance passed to VulkanRenderPass to call CmdBeginRendering
    private readonly KhrDynamicRendering _dynRenderingExt;
    private readonly CommandBuffer _handle;
    private readonly Fence _submitFence;
    //Staging buffers created by WriteToTexture live until after Submit and are released together
    private readonly List<VulkanBuffer> _stagingBuffers = new();
    //_pendingStagingBuffers during SubmitAsync the staging buffers move to the pending-release list until WaitForCompletion
    private readonly List<VulkanBuffer> _pendingStagingBuffers = new();
    //Callbacks queued by CopyTextureToBuffer, fired after the GPU has finished the submit
    private readonly List<System.Action> _pendingCallbacks = new();
    //The render pass currently open, closed by SubmitRenderPass
    private VulkanRenderPass? _currentRenderPass;
    private bool _pendingSubmit;
    private bool _disposed;
    private bool _recording;

    //Handle the underlying VkCommandBuffer, used by the surface when blitting into the swapchain
    internal CommandBuffer Handle => _handle;

    internal VulkanCommandEncoder(Vk vk, Device device, VulkanDevice gpuDevice, CommandPool commandPool, Queue graphicsQueue, KhrDynamicRendering dynRenderingExt)
    {
        _vk = vk;
        _device = device;
        _gpuDevice = gpuDevice;
        _commandPool = commandPool;
        _graphicsQueue = graphicsQueue;
        _dynRenderingExt = dynRenderingExt;
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        if (_vk.AllocateCommandBuffers(_device, &allocInfo, out _handle) != Result.Success)
            throw new InvalidOperationException("Command buffer allocation failed");
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        if (_vk.CreateFence(_device, &fenceInfo, null, out _submitFence) != Result.Success)
            throw new InvalidOperationException("Submit fence creation failed");
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        if (_vk.BeginCommandBuffer(_handle, &beginInfo) != Result.Success)
            throw new InvalidOperationException("Failed to begin command buffer recording");
        _recording = true;
    }

    public RenderPassBackend CreateRenderPass(RenderPassDescriptor descriptor)
    {
        EnsureRecording();
        _currentRenderPass = new VulkanRenderPass(_vk, _dynRenderingExt, _handle, _gpuDevice, descriptor);
        return _currentRenderPass;
    }

    public void SubmitRenderPass()
    {
        _currentRenderPass?.End();
        _currentRenderPass = null;
    }

    public void ClearColorTexture(GpuTexture texture, Vector4 color)
    {
        EnsureRecording();
        if (texture is not VulkanImage vkImage)
            throw new ArgumentException("texture must be a VulkanImage", nameof(texture));
        vkImage.TransitionLayout(_handle, ImageLayout.TransferDstOptimal);
        var clearValue = new ClearColorValue
        {
            Float32_0 = color.X,
            Float32_1 = color.Y,
            Float32_2 = color.Z,
            Float32_3 = color.W
        };
        var range = new ImageSubresourceRange
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };
        _vk.CmdClearColorImage(_handle, vkImage.Handle, ImageLayout.TransferDstOptimal, &clearValue, 1, &range);
        vkImage.TransitionLayout(_handle, ImageLayout.ShaderReadOnlyOptimal);
    }

    public void ClearColorAndDepthTextures(GpuTexture colorTexture, Vector4 color, GpuTexture depthTexture, double depth)
        => ClearColorAndDepthTextures(colorTexture, color, depthTexture, depth, 0, 0, colorTexture.Width, colorTexture.Height);

    public void ClearColorAndDepthTextures(GpuTexture colorTexture, Vector4 color, GpuTexture depthTexture, double depth, int regionX, int regionY, int regionWidth, int regionHeight)
    {
        //Region clears need an explicit render pass; the full-texture path is the one NetCraft uses
        if (regionX != 0 || regionY != 0 || regionWidth != colorTexture.Width || regionHeight != colorTexture.Height)
            throw new NotSupportedException("Region clear of color and depth attachments is not implemented");
        ClearColorTexture(colorTexture, color);
        ClearDepthTexture(depthTexture, depth);
    }

    public void ClearDepthTexture(GpuTexture depthTexture, double depth)
    {
        EnsureRecording();
        if (depthTexture is not VulkanImage vkImage)
            throw new ArgumentException("depthTexture must be a VulkanImage", nameof(depthTexture));
        vkImage.TransitionLayout(_handle, ImageLayout.TransferDstOptimal);
        var clearValue = new ClearDepthStencilValue { Depth = (float)depth, Stencil = 0 };
        var range = new ImageSubresourceRange
        {
            AspectMask = ImageAspectFlags.DepthBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };
        _vk.CmdClearDepthStencilImage(_handle, vkImage.Handle, ImageLayout.TransferDstOptimal, &clearValue, 1, &range);
        vkImage.TransitionLayout(_handle, ImageLayout.DepthStencilAttachmentOptimal);
    }

    public void WriteToBuffer(GpuBufferSlice destination, ReadOnlySpan<byte> data)
    {
        EnsureRecording();
        if (destination.Buffer is not VulkanBuffer vkDst)
            throw new ArgumentException("destination must be a VulkanBuffer", nameof(destination));
        var staging = (VulkanBuffer)_gpuDevice.CreateBuffer(null, GpuBuffer.UsageCopySrc | GpuBuffer.UsageCopyDst | GpuBuffer.UsageMapWrite, data.Length);
        staging.Upload(data.ToArray());
        _stagingBuffers.Add(staging);
        var region = new BufferCopy
        {
            SrcOffset = 0,
            DstOffset = (ulong)destination.Offset,
            Size = (ulong)data.Length
        };
        _vk.CmdCopyBuffer(_handle, staging.Handle, vkDst.Handle, 1, &region);
    }

    public void CopyToBuffer(GpuBufferSlice source, GpuBufferSlice target)
    {
        EnsureRecording();
        if (source.Buffer is not VulkanBuffer vkSrc) throw new ArgumentException("source must be a VulkanBuffer", nameof(source));
        if (target.Buffer is not VulkanBuffer vkDst) throw new ArgumentException("target must be a VulkanBuffer", nameof(target));
        var region = new BufferCopy
        {
            SrcOffset = (ulong)source.Offset,
            DstOffset = (ulong)target.Offset,
            Size = (ulong)source.Length
        };
        _vk.CmdCopyBuffer(_handle, vkSrc.Handle, vkDst.Handle, 1, &region);
    }

    //WriteToTexture records a pixel upload into the current command buffer via a staging buffer
    //The staging buffer lives until after Submit and is released then; Submit waits on the fence so the GPU has read it
    public void WriteToTexture(GpuTexture destination, ReadOnlySpan<byte> data, int mipLevel, int depthOrLayer, int destX, int destY, int width, int height)
    {
        EnsureRecording();
        if (destination is not VulkanImage vkDst)
            throw new ArgumentException("destination must be a VulkanImage", nameof(destination));
        if (vkDst.Format.HasDepthAspect())
            throw new InvalidOperationException("Depth textures do not support WriteToTexture");
        var staging = (VulkanBuffer)_gpuDevice.CreateBuffer(null, GpuBuffer.UsageCopySrc | GpuBuffer.UsageCopyDst | GpuBuffer.UsageMapWrite, data.Length);
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
                MipLevel = (uint)mipLevel,
                BaseArrayLayer = (uint)depthOrLayer,
                LayerCount = 1
            },
            ImageOffset = new Offset3D { X = destX, Y = destY, Z = 0 },
            ImageExtent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 }
        };
        _vk.CmdCopyBufferToImage(_handle, staging.Handle, vkDst.Handle, ImageLayout.TransferDstOptimal, 1, &region);
        vkDst.TransitionLayout(_handle, ImageLayout.ShaderReadOnlyOptimal);
    }

    public void CopyBufferToTexture(GpuBufferSlice source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, GpuTexture destination, int destinationX, int destinationY, int copyWidth, int copyHeight, int mipLevel, int arrayLayer)
    {
        EnsureRecording();
        if (source.Buffer is not VulkanBuffer vkSrc) throw new ArgumentException("source must be a VulkanBuffer", nameof(source));
        if (destination is not VulkanImage vkDst) throw new ArgumentException("destination must be a VulkanImage", nameof(destination));
        vkDst.TransitionLayout(_handle, ImageLayout.TransferDstOptimal);
        var region = new BufferImageCopy
        {
            BufferOffset = (ulong)source.Offset,
            BufferRowLength = (uint)sourceWidth,
            BufferImageHeight = (uint)sourceHeight,
            ImageSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = (uint)mipLevel,
                BaseArrayLayer = (uint)arrayLayer,
                LayerCount = 1
            },
            ImageOffset = new Offset3D { X = destinationX, Y = destinationY, Z = 0 },
            ImageExtent = new Extent3D { Width = (uint)copyWidth, Height = (uint)copyHeight, Depth = 1 }
        };
        _vk.CmdCopyBufferToImage(_handle, vkSrc.Handle, vkDst.Handle, ImageLayout.TransferDstOptimal, 1, &region);
        vkDst.TransitionLayout(_handle, ImageLayout.ShaderReadOnlyOptimal);
    }

    public void CopyTextureToBuffer(GpuTexture source, GpuBuffer destination, long offset, System.Action? callback, int mipLevel)
        => CopyTextureToBuffer(source, destination, offset, callback, mipLevel, 0, 0, source.GetWidth(mipLevel), source.GetHeight(mipLevel));

    public void CopyTextureToBuffer(GpuTexture source, GpuBuffer destination, long offset, System.Action? callback, int mipLevel, int x, int y, int width, int height)
    {
        EnsureRecording();
        if (source is not VulkanImage vkSrc) throw new ArgumentException("source must be a VulkanImage", nameof(source));
        if (destination is not VulkanBuffer vkDst) throw new ArgumentException("destination must be a VulkanBuffer", nameof(destination));
        vkSrc.TransitionLayout(_handle, ImageLayout.TransferSrcOptimal);
        var region = new BufferImageCopy
        {
            BufferOffset = (ulong)offset,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = (uint)mipLevel,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new Offset3D { X = x, Y = y, Z = 0 },
            ImageExtent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 }
        };
        _vk.CmdCopyImageToBuffer(_handle, vkSrc.Handle, ImageLayout.TransferSrcOptimal, vkDst.Handle, 1, &region);
        vkSrc.TransitionLayout(_handle, ImageLayout.ShaderReadOnlyOptimal);
        //The callback fires once the copy is scheduled; the synchronous Submit waits for the GPU right after
        if (callback != null)
            _pendingCallbacks.Add(callback);
    }

    public void CopyTextureToTexture(GpuTexture source, GpuTexture destination, int mipLevel, int destX, int destY, int sourceX, int sourceY, int width, int height)
    {
        EnsureRecording();
        if (source is not VulkanImage vkSrc) throw new ArgumentException("source must be a VulkanImage", nameof(source));
        if (destination is not VulkanImage vkDst) throw new ArgumentException("destination must be a VulkanImage", nameof(destination));
        vkSrc.TransitionLayout(_handle, ImageLayout.TransferSrcOptimal);
        vkDst.TransitionLayout(_handle, ImageLayout.TransferDstOptimal);
        var region = new ImageCopy
        {
            SrcSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = (uint)mipLevel,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            SrcOffset = new Offset3D { X = sourceX, Y = sourceY, Z = 0 },
            DstSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = (uint)mipLevel,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            DstOffset = new Offset3D { X = destX, Y = destY, Z = 0 },
            Extent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 }
        };
        _vk.CmdCopyImage(_handle, vkSrc.Handle, ImageLayout.TransferSrcOptimal, vkDst.Handle, ImageLayout.TransferDstOptimal, 1, &region);
        vkSrc.TransitionLayout(_handle, ImageLayout.ShaderReadOnlyOptimal);
        vkDst.TransitionLayout(_handle, ImageLayout.ShaderReadOnlyOptimal);
    }

    public GpuFence CreateFence() => new VulkanFence(_vk, _device);

    public TransientMemory TransientMemory()
        => throw new NotSupportedException("TransientMemory is not implemented in the Vulkan backend");

    public void WriteTimestamp(GpuQueryPool pool, int index)
        => throw new NotSupportedException("Timestamp queries are not implemented yet");

    //TransitionImageLayout records an image layout transition into the current command buffer
    //A NetCraft extension used by the offscreen PIP path; vanilla has no explicit layout management
    public void TransitionImageLayout(GpuTexture image, GpuImageLayout newLayout)
    {
        EnsureRecording();
        if (image is not VulkanImage vkImg)
            throw new ArgumentException("image must be a VulkanImage", nameof(image));
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
            throw new InvalidOperationException("Failed to end command buffer recording");
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
            throw new InvalidOperationException("QueueSubmit failed");
        _vk.WaitForFences(_device, 1, &fence, Vk.True, ulong.MaxValue);
        _vk.ResetFences(_device, 1, &fence);
        foreach (var sb in _stagingBuffers) sb.Dispose();
        _stagingBuffers.Clear();
        foreach (var cb in _pendingCallbacks) cb();
        _pendingCallbacks.Clear();
    }

    //SubmitAsync submits commands to the GPU queue without waiting, for the PIP double-buffered async path
    public void SubmitAsync()
    {
        if (!_recording) return;
        if (_vk.EndCommandBuffer(_handle) != Result.Success)
            throw new InvalidOperationException("Failed to end command buffer recording");
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
            throw new InvalidOperationException("QueueSubmit failed");
        _pendingSubmit = true;
        _pendingStagingBuffers.AddRange(_stagingBuffers);
        _stagingBuffers.Clear();
    }

    //WaitForCompletion waits for the GPU commands submitted by SubmitAsync to finish
    public void WaitForCompletion()
    {
        if (!_pendingSubmit) return;
        var fence = _submitFence;
        _vk.WaitForFences(_device, 1, &fence, Vk.True, ulong.MaxValue);
        _vk.ResetFences(_device, 1, &fence);
        foreach (var sb in _pendingStagingBuffers) sb.Dispose();
        _pendingStagingBuffers.Clear();
        foreach (var cb in _pendingCallbacks) cb();
        _pendingCallbacks.Clear();
        _pendingSubmit = false;
    }

    //BeginRecording restarts command recording so the encoder can be reused across frames
    public void BeginRecording()
    {
        if (_recording) return;
        if (_pendingSubmit)
            throw new InvalidOperationException("Cannot BeginRecording after SubmitAsync without WaitForCompletion");
        _vk.ResetCommandBuffer(_handle, CommandBufferResetFlags.None);
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        if (_vk.BeginCommandBuffer(_handle, &beginInfo) != Result.Success)
            throw new InvalidOperationException("Failed to begin command buffer recording");
        _recording = true;
    }

    private void EnsureRecording()
    {
        if (!_recording) throw new InvalidOperationException("CommandEncoder already Submitted and cannot record again");
    }

    public void Dispose()
    {
        if (_disposed) return;
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
