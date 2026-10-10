using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;
using NetCraft.Client.Blaze3d;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;

using RenderPipeline = NetCraft.Client.Blaze3d.Pipeline.RenderPipeline;
using NetCraft.Client.Blaze3d.Pipeline;
namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanRenderPass Vulkan backend render pass implementing RenderPassBackend
//Uses VK_KHR_dynamic_rendering via CmdBeginRenderingKHR/CmdEndRenderingKHR instead of the traditional CmdBeginRenderPass
//Resources are bound by the names declared in the pipeline's BindGroupLayouts, resolved through VulkanRenderPipeline.TryGetBinding
public sealed unsafe class VulkanRenderPass : RenderPassBackend
{
    private readonly Vk _vk;
    private readonly KhrDynamicRendering _dynRenderingExt;
    private readonly CommandBuffer _cmd;
    private readonly VulkanDevice _device;
    private readonly RenderPassDescriptor _descriptor;
    private VulkanRenderPipeline? _pipeline;
    private GpuDescriptorSet[]? _descriptorSets;
    private bool _begun;
    private bool _ended;

    internal VulkanRenderPass(Vk vk, KhrDynamicRendering dynRenderingExt, CommandBuffer cmd, VulkanDevice device, RenderPassDescriptor descriptor)
    {
        _vk = vk;
        _dynRenderingExt = dynRenderingExt;
        _cmd = cmd;
        _device = device;
        _descriptor = descriptor;
    }

    public void SetPipeline(RenderPipeline pipeline)
    {
        if (_pipeline != null)
            throw new InvalidOperationException("SetPipeline may only be called once per render pass");
        if (_device.PrecompilePipeline(pipeline) is not VulkanRenderPipeline vkPipeline)
            throw new ArgumentException("pipeline must compile to a VulkanRenderPipeline", nameof(pipeline));
        SetCompiledPipeline(vkPipeline);
    }

    //SetCompiledPipeline binds an already compiled pipeline, a NetCraft entry for callers that hold a VulkanRenderPipeline directly
    internal void SetCompiledPipeline(VulkanRenderPipeline vkPipeline)
    {
        if (_pipeline != null)
            throw new InvalidOperationException("SetPipeline may only be called once per render pass");
        _pipeline = vkPipeline;
        BeginRendering();
        _vk.CmdBindPipeline(_cmd, PipelineBindPoint.Graphics, vkPipeline.Pipeline);
        AllocateDescriptorSets(vkPipeline);
    }

    public void BindTexture(string name, GpuTextureView? textureView, GpuSampler? sampler)
    {
        var (set, binding) = Resolve(name);
        if (textureView == null || sampler == null)
            throw new ArgumentException($"Binding {name} requires both a texture view and a sampler");
        _descriptorSets![set].WriteImage(binding, textureView.Texture, sampler);
        BindSet(set);
    }

    public void SetUniform(string name, GpuBuffer buffer) => SetUniform(name, buffer.Slice());

    public void SetUniform(string name, GpuBufferSlice slice)
    {
        var (set, binding) = Resolve(name);
        _descriptorSets![set].WriteBuffer(binding, slice.Buffer, (int)slice.Offset, (int)slice.Length);
        BindSet(set);
    }

    public void PushDebugGroup(Func<string> label) { }

    public void PopDebugGroup() { }

    public void EnableScissor(int x, int y, int width, int height)
    {
        //Clamped to non-negative width/height to avoid undefined driver behavior with a 0x0 extent
        var rect = new Rect2D
        {
            Offset = { X = Math.Max(0, x), Y = Math.Max(0, y) },
            Extent = { Width = (uint)Math.Max(0, width), Height = (uint)Math.Max(0, height) }
        };
        _vk.CmdSetScissor(_cmd, 0, 1, &rect);
    }

    public void DisableScissor()
    {
        var area = _descriptor.RenderArea!;
        var rect = new Rect2D
        {
            Offset = { X = area.X, Y = area.Y },
            Extent = { Width = (uint)area.Width, Height = (uint)area.Height }
        };
        _vk.CmdSetScissor(_cmd, 0, 1, &rect);
    }

    public void SetVertexBuffer(int slot, GpuBufferSlice? vertexBuffer)
    {
        if (vertexBuffer == null)
        {
            var nullHandles = stackalloc Buffer[1];
            nullHandles[0] = default;
            var nullOffsets = stackalloc ulong[1];
            nullOffsets[0] = 0;
            _vk.CmdBindVertexBuffers(_cmd, (uint)slot, 1, nullHandles, nullOffsets);
            return;
        }
        if (vertexBuffer.Buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer must be a VulkanBuffer", nameof(vertexBuffer));
        var handles = stackalloc Buffer[1];
        handles[0] = vkBuffer.Handle;
        var offsets = stackalloc ulong[1];
        offsets[0] = (ulong)vertexBuffer.Offset;
        _vk.CmdBindVertexBuffers(_cmd, (uint)slot, 1, handles, offsets);
    }

    public void SetIndexBuffer(GpuBuffer buffer, IndexType indexType)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer must be a VulkanBuffer", nameof(buffer));
        _vk.CmdBindIndexBuffer(_cmd, vkBuffer.Handle, 0, ToVkIndexType(indexType));
    }

    public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
        => _vk.CmdDraw(_cmd, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);

    public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int vertexOffset, int firstInstance)
        => _vk.CmdDrawIndexed(_cmd, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, vertexOffset, (uint)firstInstance);

    public void DrawIndirect(GpuBufferSlice commands, int drawCount)
    {
        if (commands.Buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("commands must be a VulkanBuffer", nameof(commands));
        _vk.CmdDrawIndirect(_cmd, vkBuffer.Handle, (ulong)commands.Offset, (uint)drawCount, 16);
    }

    public void DrawIndexedIndirect(GpuBufferSlice commands, int drawCount)
    {
        if (commands.Buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("commands must be a VulkanBuffer", nameof(commands));
        _vk.CmdDrawIndexedIndirect(_cmd, vkBuffer.Handle, (ulong)commands.Offset, (uint)drawCount, 20);
    }

    public void MultiDrawIndexed(ReadOnlySpan<int> drawParameters, int instanceCount, int firstInstance, int drawCount)
        => throw new NotSupportedException("multiDrawIndexed requires VK_KHR_multi_draw_indirect");

    public void MultiDrawIndexed(ReadOnlySpan<int> firstIndexOffsets, ReadOnlySpan<int> indexCounts, ReadOnlySpan<int> vertexOffsets, int drawCount)
        => throw new NotSupportedException("multiDrawIndexed requires VK_KHR_multi_draw_indirect");

    public void DrawMultipleIndexed<T>(IReadOnlyCollection<NetCraft.Client.Blaze3d.Systems.RenderPass.DrawCommand<T>> draws, GpuBuffer? defaultIndexBuffer, IndexType? defaultIndexType, IReadOnlyCollection<string> dynamicUniforms, T uniformArgument)
        => throw new NotSupportedException("drawMultipleIndexed is not implemented in the Vulkan backend");

    public void MultiDraw(ReadOnlySpan<int> drawParameters, int instanceCount, int firstInstance, int drawCount)
        => throw new NotSupportedException("multiDraw requires VK_KHR_multi_draw_indirect");

    public void MultiDraw(ReadOnlySpan<int> firstVertices, ReadOnlySpan<int> vertexCounts, int drawCount)
        => throw new NotSupportedException("multiDraw requires VK_KHR_multi_draw_indirect");

    public void WriteTimestamp(GpuQueryPool pool, int index)
        => throw new NotSupportedException("Timestamp queries are not implemented yet");

    //End closes dynamic rendering; called by VulkanCommandEncoder.SubmitRenderPass
    internal void End()
    {
        if (_ended) return;
        if (_begun)
            _dynRenderingExt.CmdEndRendering(_cmd);
        _ended = true;
    }

    private void BeginRendering()
    {
        var attachments = _descriptor.ColorAttachments;
        var colorInfos = new RenderingAttachmentInfo[attachments.Count];
        for (int i = 0; i < attachments.Count; i++)
        {
            var attachment = attachments[i];
            if (attachment == null)
            {
                colorInfos[i] = default;
                continue;
            }
            if (attachment.TextureView.Texture is not VulkanImage colorImage)
                throw new ArgumentException("color attachment must be a VulkanImage");
            var info = new RenderingAttachmentInfo
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = colorImage.View,
                ImageLayout = ImageLayout.ColorAttachmentOptimal,
                StoreOp = AttachmentStoreOp.Store
            };
            if (attachment.ClearValue is { } clearColor)
            {
                info.LoadOp = AttachmentLoadOp.Clear;
                info.ClearValue = new ClearValue
                {
                    Color = new ClearColorValue
                    {
                        Float32_0 = clearColor.X,
                        Float32_1 = clearColor.Y,
                        Float32_2 = clearColor.Z,
                        Float32_3 = clearColor.W
                    }
                };
            }
            else
            {
                info.LoadOp = AttachmentLoadOp.Load;
            }
            colorInfos[i] = info;
        }

        var hasDepth = _descriptor.DepthAttachment != null;
        var depthInfo = default(RenderingAttachmentInfo);
        if (hasDepth)
        {
            var depthAttachment = _descriptor.DepthAttachment!;
            if (depthAttachment.TextureView.Texture is not VulkanImage depthImage)
                throw new ArgumentException("depth attachment must be a VulkanImage");
            depthInfo = new RenderingAttachmentInfo
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = depthImage.View,
                ImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                StoreOp = AttachmentStoreOp.Store
            };
            if (depthAttachment.ClearValue is { } clearDepth)
            {
                depthInfo.LoadOp = AttachmentLoadOp.Clear;
                depthInfo.ClearValue = new ClearValue
                {
                    DepthStencil = new ClearDepthStencilValue { Depth = (float)clearDepth, Stencil = 0 }
                };
            }
            else
            {
                depthInfo.LoadOp = AttachmentLoadOp.Load;
            }
        }

        var area = _descriptor.RenderArea!;
        var renderArea = new Rect2D
        {
            Offset = { X = area.X, Y = area.Y },
            Extent = { Width = (uint)area.Width, Height = (uint)area.Height }
        };
        fixed (RenderingAttachmentInfo* pColor = colorInfos)
        {
            var renderingInfo = new RenderingInfo
            {
                SType = StructureType.RenderingInfo,
                RenderArea = renderArea,
                LayerCount = 1,
                ColorAttachmentCount = (uint)colorInfos.Length,
                PColorAttachments = pColor,
                PDepthAttachment = hasDepth ? &depthInfo : null
            };
            _dynRenderingExt.CmdBeginRendering(_cmd, &renderingInfo);
        }
        _begun = true;
    }

    private void AllocateDescriptorSets(VulkanRenderPipeline pipeline)
    {
        var layouts = pipeline.Description.DescriptorLayouts;
        _descriptorSets = new GpuDescriptorSet[layouts.Count];
        for (int i = 0; i < layouts.Count; i++)
            _descriptorSets[i] = _device.AllocateDescriptorSet(layouts[i]);
    }

    private (int Set, int Binding) Resolve(string name)
    {
        if (_pipeline == null)
            throw new InvalidOperationException("SetPipeline must be called before binding resources");
        if (!_pipeline.TryGetBinding(name, out var set, out var binding))
            throw new ArgumentException($"Unknown binding name '{name}'", nameof(name));
        return ((int)set, (int)binding);
    }

    private void BindSet(int setIndex)
    {
        var handles = stackalloc DescriptorSet[1];
        handles[0] = ((VulkanDescriptorSet)_descriptorSets![setIndex]).Handle;
        _vk.CmdBindDescriptorSets(_cmd, PipelineBindPoint.Graphics, _pipeline!.PipelineLayout, (uint)setIndex, 1, handles, 0, null);
    }

    private static Silk.NET.Vulkan.IndexType ToVkIndexType(IndexType type) => type switch
    {
        IndexType.Short => Silk.NET.Vulkan.IndexType.Uint16,
        IndexType.Int => Silk.NET.Vulkan.IndexType.Uint32,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
