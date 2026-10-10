using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;
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

//VulkanCommandBuffer Vulkan backend command buffer
//Wraps VkCommandBuffer providing Begin/End/BindVertex/BindIndex/BindDescriptorSet/Draw/DrawIndexed recording entry points
//Submit internally uses a fence to wait synchronously, suited to single-threaded serial submission during resource initialization
//4.3 rework makes BeginRenderPass/EndRenderPass use CmdBeginRendering/CmdEndRendering dynamic rendering
public sealed unsafe class VulkanCommandBuffer : GpuCommandBuffer
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly CommandPool _commandPool;
    private readonly CommandBuffer _handle;
    private readonly Queue _graphicsQueue;
    //DynRenderingExt the KHR_dynamic_rendering extension instance calling CmdBeginRendering/CmdEndRendering
    private readonly KhrDynamicRendering _dynRenderingExt;
    private readonly Fence _submitFence;
    private VulkanRenderPipeline? _currentPipeline;
    private bool _disposed;

    public CommandBuffer Handle => _handle;

    internal VulkanCommandBuffer(Vk vk, Device device, CommandPool commandPool, CommandBuffer handle, Queue graphicsQueue, KhrDynamicRendering dynRenderingExt)
    {
        _vk = vk;
        _device = device;
        _commandPool = commandPool;
        _handle = handle;
        _graphicsQueue = graphicsQueue;
        _dynRenderingExt = dynRenderingExt;
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        if (_vk.CreateFence(_device, &fenceInfo, null, out _submitFence) != Result.Success)
            throw new InvalidOperationException("Submit fence creation failed");
    }

    //BeginRecording starts command buffer recording
    public override void BeginRecording()
    {
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo
        };
        if (_vk.BeginCommandBuffer(_handle, &beginInfo) != Result.Success)
        {
            throw new InvalidOperationException("Failed to begin command buffer recording");
        }
        _currentPipeline = null;
    }

    //BeginRenderPass single-arg version for GpuCommandBuffer abstraction compatibility; without an ImageView dynamic rendering is impossible
    //The Vulkan backend uses the multi-arg overload passing the swapchain ImageView
    public override void BeginRenderPass(CompiledRenderPipeline pipeline)
        => throw new NotSupportedException("dynamic rendering needs an ImageView; use the BeginRenderPass(pipeline, colorImageView) overload");

    //BeginRenderPass 4.3 rework to dynamic rendering using CmdBeginRendering instead of CmdBeginRenderPass
    //colorImageView is the swapchain image view passed by the caller; depthImage optionally passes a depth attachment
    public void BeginRenderPass(CompiledRenderPipeline pipeline, ImageView colorImageView, GpuImage? depthImage = null, float clearDepth = 0f)
    {
        if (pipeline is not VulkanRenderPipeline vkPipeline)
        {
            throw new ArgumentException("pipeline must be a VulkanRenderPipeline", nameof(pipeline));
        }
        _currentPipeline = vkPipeline;

        //Color attachment LoadOp=Clear using pipeline.ClearColor StoreOp=Store Layout=ColorAttachmentOptimal
        var colorClear = new ClearValue
        {
            Color = new ClearColorValue
            {
                Float32_0 = vkPipeline.ClearColor.R,
                Float32_1 = vkPipeline.ClearColor.G,
                Float32_2 = vkPipeline.ClearColor.B,
                Float32_3 = vkPipeline.ClearColor.A
            }
        };
        var colorAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = colorImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = colorClear
        };

        //Depth attachment attached with Layout=DepthStencilAttachmentOptimal when depthImage is non-null
        var hasDepth = depthImage is VulkanImage;
        var depthAttachment = hasDepth
            ? new RenderingAttachmentInfo
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = ((VulkanImage)depthImage!).View,
                ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                ClearValue = new ClearValue
                {
                    DepthStencil = new ClearDepthStencilValue { Depth = clearDepth > 0 ? clearDepth : 1.0f, Stencil = 0 }
                }
            }
            : default;

        //RenderingInfo dynamic rendering main struct RenderArea=extent ColorAttachmentCount=1
        var renderArea = new Rect2D { Offset = new Offset2D { X = 0, Y = 0 }, Extent = vkPipeline.Extent };
        RenderingAttachmentInfo* pColor = &colorAttachment;
        RenderingAttachmentInfo* pDepth = hasDepth ? &depthAttachment : null;
        var renderingInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = renderArea,
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = pColor,
            PDepthAttachment = pDepth
        };
        _dynRenderingExt.CmdBeginRendering(_handle, &renderingInfo);
        _vk.CmdBindPipeline(_handle, PipelineBindPoint.Graphics, vkPipeline.Pipeline);
    }

    //BindPipeline switches the graphics pipeline within the same RenderPass
    //Used to switch between the rectangle and text pipelines without a new BeginRenderPass
    public override void BindPipeline(CompiledRenderPipeline pipeline)
    {
        if (pipeline is not VulkanRenderPipeline vkPipeline)
            throw new ArgumentException("pipeline must be a VulkanRenderPipeline", nameof(pipeline));
        _currentPipeline = vkPipeline;
        _vk.CmdBindPipeline(_handle, PipelineBindPoint.Graphics, vkPipeline.Pipeline);
    }

    //BindVertexBuffer binds a vertex buffer to the given binding slot
    public override void BindVertexBuffer(GpuBuffer buffer, int binding = 0, ulong offset = 0)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer must be a VulkanBuffer", nameof(buffer));
        var handles = stackalloc Buffer[1];
        handles[0] = vkBuffer.Handle;
        var offsets = stackalloc ulong[1];
        offsets[0] = offset;
        _vk.CmdBindVertexBuffers(_handle, (uint)binding, 1, handles, offsets);
    }

    //BindIndexBuffer binds an index buffer
    public override void BindIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer must be a VulkanBuffer", nameof(buffer));
        _vk.CmdBindIndexBuffer(_handle, vkBuffer.Handle, offset, ToVkIndexType(indexType));
    }

    //BindDescriptorSet binds a descriptor set to the setIndex slot of the current pipeline's PipelineLayout
    public override void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0)
    {
        if (_currentPipeline is null)
            throw new InvalidOperationException("BindDescriptorSet must be called after BeginRenderPass");
        if (set is not VulkanDescriptorSet vkSet)
            throw new ArgumentException("set must be a VulkanDescriptorSet", nameof(set));
        var handles = stackalloc DescriptorSet[1];
        handles[0] = vkSet.Handle;
        _vk.CmdBindDescriptorSets(_handle, PipelineBindPoint.Graphics, _currentPipeline.PipelineLayout, setIndex, 1, handles, 0, null);
    }

    //Draw issues a non-indexed draw
    public override void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        _vk.CmdDraw(_handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }

    //DrawIndexed issues an indexed draw; vertexOffset is the base vertex offset
    public override void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0)
    {
        _vk.CmdDrawIndexed(_handle, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, (int)vertexOffset, (uint)firstInstance);
    }

    //SetScissor sets the dynamic scissor rectangle, pixel coordinates with the top-left origin and y downward
    //Clamped to non-negative width/height to avoid undefined driver behavior with a 0x0 extent
    public override void SetScissor(int x, int y, int width, int height)
    {
        var rx = Math.Max(0, x);
        var ry = Math.Max(0, y);
        var rw = Math.Max(0, width);
        var rh = Math.Max(0, height);
        var rect = new Rect2D
        {
            Offset = { X = rx, Y = ry },
            Extent = { Width = (uint)rw, Height = (uint)rh }
        };
        _vk.CmdSetScissor(_handle, 0, 1, &rect);
    }

    //EndRenderPass 4.3 rework uses CmdEndRendering to end dynamic rendering
    public override void EndRenderPass()
    {
        _dynRenderingExt.CmdEndRendering(_handle);
        _currentPipeline = null;
    }

    //EndRecording ends command buffer recording
    public override void EndRecording()
    {
        if (_vk.EndCommandBuffer(_handle) != Result.Success)
        {
            throw new InvalidOperationException("Failed to end command buffer recording");
        }
    }

    //Submit submits to the graphics queue and waits for the fence
    //Single-threaded serial semantics without semaphores, suited to resource initialization and simple test scenarios
    //When the render loop needs GPU/CPU parallelism the caller should QueueSubmit directly
    public override void Submit()
    {
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
    }

    //Reset resets the command buffer for re-recording
    public void Reset()
    {
        _vk.ResetCommandBuffer(_handle, CommandBufferResetFlags.ReleaseResourcesBit);
    }

    private static IndexType ToVkIndexType(GpuIndexType type) => type switch
    {
        GpuIndexType.UInt16 => IndexType.Uint16,
        GpuIndexType.UInt32 => IndexType.Uint32,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public override void Dispose()
    {
        if (_disposed) return;
        var cmd = _handle;
        _vk.FreeCommandBuffers(_device, _commandPool, 1, &cmd);
        var fence = _submitFence;
        _vk.DestroyFence(_device, fence, null);
        _disposed = true;
    }
}
