using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace NetCraft.Gpu.Vulkan;

//VulkanCommandBuffer Vulkan 后端命令缓冲
//包装 VkCommandBuffer 提供 Begin/End/BindVertex/BindIndex/BindDescriptorSet/Draw/DrawIndexed 录制入口
//Submit 内部用 fence 同步等待完成适合单线程串行提交资源初始化场景
//4.3 改造 BeginRenderPass/EndRenderPass 走 CmdBeginRendering/CmdEndRendering dynamic rendering
public sealed unsafe class VulkanCommandBuffer : GpuCommandBuffer
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly CommandPool _commandPool;
    private readonly CommandBuffer _handle;
    private readonly Queue _graphicsQueue;
    //DynRenderingExt KHR_dynamic_rendering 扩展实例调 CmdBeginRendering/CmdEndRendering
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
            throw new InvalidOperationException("Submit fence 创建失败");
    }

    //BeginRecording 开始命令缓冲录制
    public override void BeginRecording()
    {
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo
        };
        if (_vk.BeginCommandBuffer(_handle, &beginInfo) != Result.Success)
        {
            throw new InvalidOperationException("命令缓冲开始录制失败");
        }
        _currentPipeline = null;
    }

    //BeginRenderPass 单参数版本 GpuCommandBuffer 抽象层兼容无 ImageView 无法 dynamic rendering
    //Vulkan 后端走多参数重载传入 swapchain ImageView
    public override void BeginRenderPass(CompiledRenderPipeline pipeline)
        => throw new NotSupportedException("dynamic rendering 需要 ImageView 用 BeginRenderPass(pipeline, colorImageView) 重载");

    //BeginRenderPass 4.3 改造走 dynamic rendering 用 CmdBeginRendering 替代 CmdBeginRenderPass
    //colorImageView 由调用方传 swapchain image view depthImage 可选传深度附件
    public void BeginRenderPass(CompiledRenderPipeline pipeline, ImageView colorImageView, GpuImage? depthImage = null, float clearDepth = 0f)
    {
        if (pipeline is not VulkanRenderPipeline vkPipeline)
        {
            throw new ArgumentException("pipeline 必须是 VulkanRenderPipeline", nameof(pipeline));
        }
        _currentPipeline = vkPipeline;

        //颜色附件 LoadOp=Clear 用 pipeline.ClearColor StoreOp=Store Layout=ColorAttachmentOptimal
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
        _dynRenderingExt.CmdBeginRendering(_handle, &renderingInfo);
        _vk.CmdBindPipeline(_handle, PipelineBindPoint.Graphics, vkPipeline.Pipeline);
    }

    //BindPipeline 在同一 RenderPass 内切换 graphics pipeline
    //用于矩形管线和文本管线之间切换不重新 BeginRenderPass
    public override void BindPipeline(CompiledRenderPipeline pipeline)
    {
        if (pipeline is not VulkanRenderPipeline vkPipeline)
            throw new ArgumentException("pipeline 必须是 VulkanRenderPipeline", nameof(pipeline));
        _currentPipeline = vkPipeline;
        _vk.CmdBindPipeline(_handle, PipelineBindPoint.Graphics, vkPipeline.Pipeline);
    }

    //BindVertexBuffer 绑定顶点缓冲到指定 binding 槽
    public override void BindVertexBuffer(GpuBuffer buffer, int binding = 0, ulong offset = 0)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer 必须是 VulkanBuffer", nameof(buffer));
        var handles = stackalloc Buffer[1];
        handles[0] = vkBuffer.Handle;
        var offsets = stackalloc ulong[1];
        offsets[0] = offset;
        _vk.CmdBindVertexBuffers(_handle, (uint)binding, 1, handles, offsets);
    }

    //BindIndexBuffer 绑定索引缓冲
    public override void BindIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("buffer 必须是 VulkanBuffer", nameof(buffer));
        _vk.CmdBindIndexBuffer(_handle, vkBuffer.Handle, offset, ToVkIndexType(indexType));
    }

    //BindDescriptorSet 绑定描述符集到当前管线 PipelineLayout 的 setIndex 槽
    public override void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0)
    {
        if (_currentPipeline is null)
            throw new InvalidOperationException("BindDescriptorSet 必须在 BeginRenderPass 之后调用");
        if (set is not VulkanDescriptorSet vkSet)
            throw new ArgumentException("set 必须是 VulkanDescriptorSet", nameof(set));
        var handles = stackalloc DescriptorSet[1];
        handles[0] = vkSet.Handle;
        _vk.CmdBindDescriptorSets(_handle, PipelineBindPoint.Graphics, _currentPipeline.PipelineLayout, setIndex, 1, handles, 0, null);
    }

    //Draw 发起非索引绘制
    public override void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        _vk.CmdDraw(_handle, (uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }

    //DrawIndexed 发起索引绘制 vertexOffset 是基础顶点偏移
    public override void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0)
    {
        _vk.CmdDrawIndexed(_handle, (uint)indexCount, (uint)instanceCount, (uint)firstIndex, (int)vertexOffset, (uint)firstInstance);
    }

    //SetScissor 设置动态裁剪矩形像素坐标左上原点 y 向下
    //clamp 到非负宽高避免 0x0 extent 驱动未定义行为
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

    //EndRenderPass 4.3 改造走 CmdEndRendering 结束 dynamic rendering
    public override void EndRenderPass()
    {
        _dynRenderingExt.CmdEndRendering(_handle);
        _currentPipeline = null;
    }

    //EndRecording 结束命令缓冲录制
    public override void EndRecording()
    {
        if (_vk.EndCommandBuffer(_handle) != Result.Success)
        {
            throw new InvalidOperationException("命令缓冲结束录制失败");
        }
    }

    //Submit 提交到 graphics queue 并等待 fence 完成
    //单线程串行语义无信号量适合资源初始化和简单测试场景
    //渲染循环需要 GPU/CPU 并行时应由调用者直接 QueueSubmit
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
            throw new InvalidOperationException("QueueSubmit 失败");
        _vk.WaitForFences(_device, 1, &fence, Vk.True, ulong.MaxValue);
        _vk.ResetFences(_device, 1, &fence);
    }

    //Reset 重置命令缓冲以便重新录制
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
