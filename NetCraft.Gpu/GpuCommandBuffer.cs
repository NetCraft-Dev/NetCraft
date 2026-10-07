namespace NetCraft.Gpu;

//GpuIndexType index buffer element type
public enum GpuIndexType
{
    UInt16,
    UInt32
}

//GpuCommandBuffer GPU command buffer, corresponds to vanilla CommandBuffer
//Records render commands, then submits them to the GPU queue for execution
[Obsolete("Replaced by ICommandEncoder + IRenderPass, removed after the stage 5 migration")]
public abstract class GpuCommandBuffer : IDisposable
{
    //BeginRecording begins recording
    public abstract void BeginRecording();

    //BeginRenderPass begins a render pass and binds the pipeline
    //Obsolete transition-layer signature, does not expose an ImageView; the concrete ImageView comes from the VulkanCommandBuffer multi-arg overload
    public abstract void BeginRenderPass(CompiledRenderPipeline pipeline);

    //BindPipeline switches the graphics pipeline within the same RenderPass
    //Used when several pipelines share one RenderPass, switching draws without a new BeginRenderPass
    public abstract void BindPipeline(CompiledRenderPipeline pipeline);

    //BindVertexBuffer binds a vertex buffer to a binding slot
    public abstract void BindVertexBuffer(GpuBuffer buffer, int binding = 0, ulong offset = 0);

    //BindIndexBuffer binds an index buffer
    public abstract void BindIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0);

    //BindDescriptorSet binds a descriptor set to the setIndex slot of the pipeline layout
    public abstract void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0);

    //Draw issues a non-indexed draw
    public abstract void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);

    //DrawIndexed issues an indexed draw
    public abstract void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0);

    //SetScissor sets the dynamic scissor rectangle, pixel coordinates with the top-left origin and y downward
    //Must be called inside a pipeline with DynamicScissorEnabled
    public abstract void SetScissor(int x, int y, int width, int height);

    //EndRenderPass ends the render pass
    public abstract void EndRenderPass();

    //EndRecording ends recording
    public abstract void EndRecording();

    //Submit submits to the GPU and waits for completion
    public abstract void Submit();

    public virtual void Dispose() { }
}
