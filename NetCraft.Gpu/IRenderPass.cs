namespace NetCraft.Gpu;

//IRenderPass render pass, maps to vanilla RenderPass
//After CreateRenderPass returns, render commands are recorded; Close ends the render pass
//Replaces the BeginRenderPass/EndRenderPass mix of the legacy GpuCommandBuffer
public interface IRenderPass : IDisposable
{
    //SetPipeline binds the graphics pipeline
    void SetPipeline(CompiledRenderPipeline pipeline);
    //SetVertexBuffer binds a vertex buffer to a binding slot
    void SetVertexBuffer(int slot, GpuBuffer buffer, ulong offset = 0);
    //SetIndexBuffer binds an index buffer
    void SetIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0);
    //BindDescriptorSet binds a descriptor set to the setIndex slot of the pipeline layout
    void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0);
    //EnableScissor enables the dynamic scissor rectangle, pixel coordinates with the top-left origin and y downward
    void EnableScissor(int x, int y, int width, int height);
    //DisableScissor disables clipping for full-screen rendering
    void DisableScissor();
    //Draw non-indexed draw
    void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);
    //DrawIndexed indexed draw; vertexOffset is the base vertex offset
    void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0);
    //Close ends the render pass; later commands are recorded into the owning CommandEncoder
    void Close();
}
