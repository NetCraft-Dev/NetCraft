namespace NetCraft.Gpu;

//IRenderPass 渲染通道对标原版 RenderPass
//CreateRenderPass 返回后录制渲染命令 Close 结束 render pass
//替代旧 GpuCommandBuffer 的 BeginRenderPass/EndRenderPass 混合
public interface IRenderPass : IDisposable
{
    //SetPipeline 绑定 graphics pipeline
    void SetPipeline(CompiledRenderPipeline pipeline);
    //SetVertexBuffer 绑定顶点缓冲到 binding 槽
    void SetVertexBuffer(int slot, GpuBuffer buffer, ulong offset = 0);
    //SetIndexBuffer 绑定索引缓冲
    void SetIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0);
    //BindDescriptorSet 绑定描述符集到管线 layout 的 setIndex 槽
    void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0);
    //EnableScissor 启用动态裁剪矩形像素坐标左上原点 y 向下
    void EnableScissor(int x, int y, int width, int height);
    //DisableScissor 禁用裁剪全屏渲染
    void DisableScissor();
    //Draw 非索引绘制
    void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);
    //DrawIndexed 索引绘制 vertexOffset 是基础顶点偏移
    void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0);
    //Close 结束 render pass 后续命令录制到所属 CommandEncoder
    void Close();
}
