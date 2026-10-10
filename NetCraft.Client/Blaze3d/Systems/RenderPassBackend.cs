using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//RenderPassBackend raw render pass operations, aligns with vanilla RenderPassBackend
//Binding goes through names the pipeline declares in its BindGroupLayouts, not through explicit descriptor sets
public interface RenderPassBackend
{
    void PushDebugGroup(Func<string> label);

    void PopDebugGroup();

    void SetPipeline(RenderPipeline pipeline);

    void BindTexture(string name, GpuTextureView? textureView, GpuSampler? sampler);

    void SetUniform(string name, GpuBuffer buffer);

    void SetUniform(string name, GpuBufferSlice slice);

    void EnableScissor(int x, int y, int width, int height);

    void DisableScissor();

    void SetVertexBuffer(int slot, GpuBufferSlice? vertexBuffer);

    void SetIndexBuffer(GpuBuffer buffer, IndexType indexType);

    void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int vertexOffset, int firstInstance);

    void MultiDrawIndexed(ReadOnlySpan<int> drawParameters, int instanceCount, int firstInstance, int drawCount);

    void MultiDrawIndexed(ReadOnlySpan<int> firstIndexOffsets, ReadOnlySpan<int> indexCounts, ReadOnlySpan<int> vertexOffsets, int drawCount);

    void DrawIndexedIndirect(GpuBufferSlice commands, int drawCount);

    void DrawMultipleIndexed<T>(IReadOnlyCollection<RenderPass.DrawCommand<T>> draws, GpuBuffer? defaultIndexBuffer, IndexType? defaultIndexType, IReadOnlyCollection<string> dynamicUniforms, T uniformArgument);

    void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance);

    void MultiDraw(ReadOnlySpan<int> drawParameters, int instanceCount, int firstInstance, int drawCount);

    void MultiDraw(ReadOnlySpan<int> firstVertices, ReadOnlySpan<int> vertexCounts, int drawCount);

    void DrawIndirect(GpuBufferSlice commands, int drawCount);

    void WriteTimestamp(GpuQueryPool pool, int index);
}
