using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace NetCraft.Gpu.Vulkan;

//VulkanRenderPass Vulkan 后端渲染通道实现 IRenderPass
//4.3 改造走 VK_KHR_dynamic_rendering 用 CmdBeginRenderingKHR/CmdEndRenderingKHR 替代传统 CmdBeginRenderPass
//附件 ImageView 由 VulkanCommandEncoder.CreateRenderPass 传入不再依赖 framebuffer
public sealed unsafe class VulkanRenderPass : IRenderPass
{
    private readonly Vk _vk;
    //DynRenderingExt KHR_dynamic_rendering 扩展实例调 CmdBeginRendering/CmdEndRendering
    private readonly KhrDynamicRendering _dynRenderingExt;
    private readonly CommandBuffer _cmd;
    //_pipeline 随 SetPipeline 切换更新 BindDescriptorSet 用当前 pipeline 的 PipelineLayout
    private VulkanRenderPipeline _pipeline;
    private bool _closed;
    private bool _disposed;

    internal VulkanRenderPass(Vk vk, KhrDynamicRendering dynRenderingExt, CommandBuffer cmd,
        CompiledRenderPipeline pipeline, ImageView colorImageView, Vector4 clearColor,
        GpuImage? depthImage, float clearDepth)
        : this(vk, dynRenderingExt, cmd, pipeline, colorImageView, clearColor, AttachmentLoadOp.Clear, depthImage, clearDepth)
    {
    }

    //colorLoadOp 控制 color 附件加载策略 BeforeBlur/blur 用 Clear AfterBlur 用 Load 保留模糊背景
    internal VulkanRenderPass(Vk vk, KhrDynamicRendering dynRenderingExt, CommandBuffer cmd,
        CompiledRenderPipeline pipeline, ImageView colorImageView, Vector4 clearColor,
        AttachmentLoadOp colorLoadOp, GpuImage? depthImage, float clearDepth)
    {
        _vk = vk;
        _dynRenderingExt = dynRenderingExt;
        _cmd = cmd;
        if (pipeline is not VulkanRenderPipeline vkPipeline)
            throw new ArgumentException("pipeline 必须是 VulkanRenderPipeline", nameof(pipeline));
        _pipeline = vkPipeline;

        //颜色附件 LoadOp 由调用方指定 StoreOp=Store 写回 Layout=ColorAttachmentOptimal
        var colorClear = new ClearValue
        {
            Color = new ClearColorValue
            {
                Float32_0 = clearColor.X,
                Float32_1 = clearColor.Y,
                Float32_2 = clearColor.Z,
                Float32_3 = clearColor.W
            }
        };
        var colorAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = colorImageView,
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            LoadOp = colorLoadOp,
            StoreOp = AttachmentStoreOp.Store,
            ClearValue = colorClear
        };

        //深度附件 depthImage 非 null 时附加 Layout=DepthStencilAttachmentOptimal
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

        //RenderingInfo dynamic rendering 主结构 RenderArea=extent ColorAttachmentCount=1
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
        _dynRenderingExt.CmdBeginRendering(_cmd, &renderingInfo);
        _vk.CmdBindPipeline(_cmd, PipelineBindPoint.Graphics, vkPipeline.Pipeline);
    }

    public void SetPipeline(CompiledRenderPipeline pipeline)
    {
        if (pipeline is not VulkanRenderPipeline vkPipeline)
            throw new ArgumentException("pipeline 必须是 VulkanRenderPipeline", nameof(pipeline));
        _pipeline = vkPipeline;
        _vk.CmdBindPipeline(_cmd, PipelineBindPoint.Graphics, vkPipeline.Pipeline);
    }

    public void SetVertexBuffer(int slot, GpuBuffer buffer, ulong offset = 0)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer 必须是 VulkanBuffer", nameof(buffer));
        var handles = stackalloc Buffer[1];
        handles[0] = vkBuffer.Handle;
        var offsets = stackalloc ulong[1];
        offsets[0] = offset;
        _vk.CmdBindVertexBuffers(_cmd, (uint)slot, 1, handles, offsets);
    }

    public void SetIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer 必须是 VulkanBuffer", nameof(buffer));
        _vk.CmdBindIndexBuffer(_cmd, vkBuffer.Handle, offset, ToVkIndexType(indexType));
    }

    public void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0)
    {
        if (set is not VulkanDescriptorSet vkSet)
            throw new ArgumentException("set 必须是 VulkanDescriptorSet", nameof(set));
        var handles = stackalloc DescriptorSet[1];
        handles[0] = vkSet.Handle;
        _vk.CmdBindDescriptorSets(_cmd, PipelineBindPoint.Graphics, _pipeline.PipelineLayout, setIndex, 1, handles, 0, null);
    }

    public void EnableScissor(int x, int y, int width, int height)
    {
        //clamp 到非负宽高避免 0x0 extent 驱动未定义行为
        var rx = Math.Max(0, x);
        var ry = Math.Max(0, y);
        var rw = Math.Max(0, width);
        var rh = Math.Max(0, height);
        var rect = new Rect2D
        {
            Offset = { X = rx, Y = ry },
            Extent = { Width = (uint)rw, Height = (uint)rh }
        };
        _vk.CmdSetScissor(_cmd, 0, 1, &rect);
    }

    public void DisableScissor()
    {
        //用 pipeline extent 作为全屏 scissor
        var rect = new Rect2D { Offset = { X = 0, Y = 0 }, Extent = _pipeline.Extent };
        _vk.CmdSetScissor(_cmd, 0, 1, &rect);
    }

    public void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
        => _vk.CmdDraw(_cmd, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);

    public void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0)
        => _vk.CmdDrawIndexed(_cmd, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, (int)vertexOffset, (uint)firstInstance);

    public void Close()
    {
        if (_closed) return;
        _dynRenderingExt.CmdEndRendering(_cmd);
        _closed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!_closed) Close();
        _disposed = true;
    }

    private static IndexType ToVkIndexType(GpuIndexType type) => type switch
    {
        GpuIndexType.UInt16 => IndexType.Uint16,
        GpuIndexType.UInt32 => IndexType.Uint32,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
