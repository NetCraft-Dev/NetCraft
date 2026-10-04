namespace NetCraft.Gpu;

//GpuIndexType 索引缓冲元素类型
public enum GpuIndexType
{
    UInt16,
    UInt32
}

//GpuCommandBuffer GPU 命令缓冲对应原版 CommandBuffer
//录制渲染命令后提交到 GPU 队列执行
[Obsolete("用 ICommandEncoder + IRenderPass 替代阶段 5 迁移移除")]
public abstract class GpuCommandBuffer : IDisposable
{
    //BeginRecording 开始录制
    public abstract void BeginRecording();

    //BeginRenderPass 开始渲染通道并绑定管线
    //Obsolete 过渡层签名不暴露 ImageView 具体 ImageView 由 VulkanCommandBuffer 多参数重载提供
    public abstract void BeginRenderPass(CompiledRenderPipeline pipeline);

    //BindPipeline 在同一 RenderPass 内切换 graphics pipeline
    //用于多个 pipeline 共享同一 RenderPass 时不重新 BeginRenderPass 切换绘制
    public abstract void BindPipeline(CompiledRenderPipeline pipeline);

    //BindVertexBuffer 绑定顶点缓冲到 binding 槽
    public abstract void BindVertexBuffer(GpuBuffer buffer, int binding = 0, ulong offset = 0);

    //BindIndexBuffer 绑定索引缓冲
    public abstract void BindIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0);

    //BindDescriptorSet 绑定描述符集到管线 layout 的 setIndex 槽
    public abstract void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0);

    //Draw 发起非索引绘制
    public abstract void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);

    //DrawIndexed 发起索引绘制
    public abstract void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0);

    //SetScissor 设置动态裁剪矩形像素坐标左上原点 y 向下
    //必须在启用了 DynamicScissorEnabled 的 pipeline 内调用
    public abstract void SetScissor(int x, int y, int width, int height);

    //EndRenderPass 结束渲染通道
    public abstract void EndRenderPass();

    //EndRecording 结束录制
    public abstract void EndRecording();

    //Submit 提交到 GPU 执行并等待完成
    public abstract void Submit();

    public virtual void Dispose() { }
}
