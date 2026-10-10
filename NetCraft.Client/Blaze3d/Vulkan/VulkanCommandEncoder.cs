using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;

using CompiledRenderPipeline = NetCraft.Client.Blaze3d.Pipeline.CompiledRenderPipeline;
using RenderPipelineDescription = NetCraft.Client.Blaze3d.Pipeline.RenderPipelineDescription;
using RenderPipeline = NetCraft.Client.Blaze3d.Pipeline.RenderPipeline;
using PipelineCache = NetCraft.Client.Blaze3d.Pipeline.PipelineCache;
using BindGroupLayout = NetCraft.Client.Blaze3d.Pipeline.BindGroupLayout;
using ColorTargetState = NetCraft.Client.Blaze3d.Pipeline.ColorTargetState;
using DepthStencilState = NetCraft.Client.Blaze3d.Pipeline.DepthStencilState;
using NetCraft.Client.Blaze3d.Pipeline;
using CompareOp = Silk.NET.Vulkan.CompareOp;
using BlendFactor = Silk.NET.Vulkan.BlendFactor;
using BlendOp = Silk.NET.Vulkan.BlendOp;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;
using PrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;
namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanCommandEncoder Vulkan backend command encoder implementing ICommandEncoder
//Wraps VkCommandBuffer to record copy/render pass commands; Submit submits to the GPU queue
//Replaces the mixed recording of the legacy VulkanCommandBuffer, separating command encoding from render passes
//4.3 rework makes CreateRenderPass use dynamic rendering, passing the ImageViews of colorImage/depthImage to VulkanRenderPass
public sealed unsafe class VulkanCommandEncoder : ICommandEncoder
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
    //Submit calls WaitForFences and waits for the GPU to finish before safely releasing the staging buffer
    private readonly List<VulkanBuffer> _stagingBuffers = new();
    //_pendingStagingBuffers during SubmitAsync the staging buffers move to the pending-release list until WaitForCompletion
    //An async Submit cannot release staging immediately since the GPU is still reading; wait for the fence
    private readonly List<VulkanBuffer> _pendingStagingBuffers = new();
    //_pendingSubmit marks that WaitForCompletion has not run after SubmitAsync; WaitForCompletion must precede BeginRecording
    private bool _pendingSubmit;
    private bool _disposed;
    private bool _recording;

    internal VulkanCommandEncoder(Vk vk, Device device, VulkanDevice gpuDevice, CommandPool commandPool, Queue graphicsQueue, KhrDynamicRendering dynRenderingExt)
    {
        _vk = vk;
        _device = device;
        _gpuDevice = gpuDevice;
        _commandPool = commandPool;
        _graphicsQueue = graphicsQueue;
        _dynRenderingExt = dynRenderingExt;
        //Allocate a command buffer
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1
        };
        if (_vk.AllocateCommandBuffers(_device, &allocInfo, out _handle) != Result.Success)
            throw new InvalidOperationException("Command buffer allocation failed");
        //Create the submit fence
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        if (_vk.CreateFence(_device, &fenceInfo, null, out _submitFence) != Result.Success)
            throw new InvalidOperationException("Submit fence creation failed");
        //Begin recording
        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        if (_vk.BeginCommandBuffer(_handle, &beginInfo) != Result.Success)
            throw new InvalidOperationException("Failed to begin command buffer recording");
        _recording = true;
    }

    public IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuTexture colorImage, Vector4 clearColor)
    {
        EnsureRecording();
        if (colorImage is not VulkanImage vkColor)
            throw new ArgumentException("colorImage must be a VulkanImage", nameof(colorImage));
        return new VulkanRenderPass(_vk, _dynRenderingExt, _handle, pipeline, vkColor.View, clearColor, null, 0f);
    }

    public IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuTexture colorImage, Vector4 clearColor, GpuTexture depthImage, float clearDepth)
    {
        EnsureRecording();
        if (colorImage is not VulkanImage vkColor)
            throw new ArgumentException("colorImage must be a VulkanImage", nameof(colorImage));
        return new VulkanRenderPass(_vk, _dynRenderingExt, _handle, pipeline, vkColor.View, clearColor, depthImage, clearDepth);
    }

    public IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuTexture colorImage, Vector4 clearColor, GpuTexture depthImage, float clearDepth, GpuLoadOp colorLoadOp)
    {
        EnsureRecording();
        if (colorImage is not VulkanImage vkColor)
            throw new ArgumentException("colorImage must be a VulkanImage", nameof(colorImage));
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
        if (src is not VulkanBuffer vkSrc) throw new ArgumentException("src must be a VulkanBuffer", nameof(src));
        if (dst is not VulkanBuffer vkDst) throw new ArgumentException("dst must be a VulkanBuffer", nameof(dst));
        var region = new BufferCopy
        {
            SrcOffset = srcOffset,
            DstOffset = dstOffset,
            Size = size
        };
        _vk.CmdCopyBuffer(_handle, vkSrc.Handle, vkDst.Handle, 1, &region);
    }

    //WriteToTexture records a pixel upload into the current command buffer via a staging buffer
    //Unlike VulkanImage.UploadRegion it does not Submit immediately but records into the current cmd, batchable with other commands
    //The staging buffer lives until after Submit and is released then; Submit calls WaitForFences to ensure the GPU has read it
    //Layout transitions currentLayout→TransferDstOptimal→copy→ShaderReadOnlyOptimal
    public void WriteToTexture(GpuTexture dst, ReadOnlySpan<byte> data, int dstX, int dstY, int width, int height)
    {
        EnsureRecording();
        if (dst is not VulkanImage vkDst)
            throw new ArgumentException("dst must be a VulkanImage", nameof(dst));
        if (vkDst.Format.HasDepthAspect())
            throw new InvalidOperationException("DepthAttachment does not support WriteToTexture");
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

    //TransitionImageLayout records an image layout transition into the current command buffer
    //After PIP offscreen rendering ColorAttachmentOptimal→ShaderReadOnlyOptimal for blit sampling
    //With cross-frame reuse it goes ShaderReadOnlyOptimal→ColorAttachmentOptimal before the next frame's render; VulkanImage.TransitionLayout already handles skipping
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
        //Submit already waited for the fence so the GPU has read the staging buffer and it can be released safely
        foreach (var sb in _stagingBuffers) sb.Dispose();
        _stagingBuffers.Clear();
    }

    //SubmitAsync submits commands to the GPU queue without waiting, for PIP double-buffered async rendering
    //The staging buffer moves into _pendingStagingBuffers and is released lazily at WaitForCompletion
    //The caller must WaitForCompletion before reusing this encoder so the GPU is done before the command buffer is Reset
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
    //Returns immediately if SubmitAsync was never called (_pendingSubmit=false), safe to skip when reusing the encoder the first time
    //After completion ResetFences makes the fence reusable for the next QueueSubmit and releases the pending staging buffers
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

    //BeginRecording restarts command recording so the encoder can be reused across frames
    //The first call is idempotent (already BeginCommandBuffer in the constructor) and returns immediately with _recording=true
    //Later calls require WaitForCompletion first so the GPU no longer uses the command buffer, then Reset+Begin
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
        //When an async Submit has not completed, first wait for the fence so the GPU no longer uses the command buffer, then Free
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
