using System.Numerics;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//RenderPass records draw commands into an open render pass, aligns with vanilla com.mojang.blaze3d.systems.RenderPass
//Dispose finishes the pass; resources are bound by name as declared in the pipeline's BindGroupLayouts
public sealed class RenderPass : IDisposable
{
    public const int MaxVertexBuffers = 16;

    //VkDrawIndirectCommand / VkDrawIndexedIndirectCommand sizes in bytes
    private const int DrawIndirectCommandSize = 16;
    private const int DrawIndexedIndirectCommandSize = 20;

    private readonly RenderPassBackend _backend;
    private readonly GpuDeviceBackend _device;
    private readonly DeviceFeatures _deviceFeatures;
    private readonly DeviceLimits _deviceLimits;
    private readonly System.Action _onFinish;
    private readonly RenderArea? _renderArea;
    private readonly List<RenderPassDescriptor.Attachment<Vector4?>?> _colorAttachments;
    private bool _isClosed;
    private int _pushedDebugGroups;

    public RenderPass(RenderPassBackend backend, GpuDeviceBackend device, List<RenderPassDescriptor.Attachment<Vector4?>?> colorAttachments, System.Action onFinish, RenderArea? renderArea)
    {
        _backend = backend;
        _device = device;
        _deviceFeatures = device.GetDeviceInfo().Features;
        _deviceLimits = device.GetDeviceInfo().Limits;
        _colorAttachments = colorAttachments;
        _onFinish = onFinish;
        _renderArea = renderArea;
    }

    public void PushDebugGroup(Func<string> label)
    {
        EnsureOpen();
        _pushedDebugGroups++;
        _backend.PushDebugGroup(label);
    }

    public void PopDebugGroup()
    {
        EnsureOpen();
        if (_pushedDebugGroups == 0)
            throw new InvalidOperationException("Can't pop more debug groups than was pushed!");
        _pushedDebugGroups--;
        _backend.PopDebugGroup();
    }

    public void WriteTimestamp(GpuQueryPool pool, int index)
    {
        if (index < 0 || index > pool.Size)
            throw new InvalidOperationException($"Index {index} is out of range for query pool of size {pool.Size}");
        _backend.WriteTimestamp(pool, index);
    }

    public void SetPipeline(RenderPipeline pipeline)
    {
        var colorTargetStates = pipeline.ColorTargetStates;
        if (colorTargetStates.Count != _colorAttachments.Count)
            throw new InvalidOperationException("Render pass color attachment count must match pipeline color target state count.");
        for (int i = 0; i < _colorAttachments.Count; i++)
        {
            var attachment = _colorAttachments[i];
            if (attachment == null) continue;
            var colorTargetState = colorTargetStates[i];
            if (colorTargetState == null || colorTargetState.Format == attachment.TextureView.Texture.Format) continue;
            throw new InvalidOperationException($"Render pass color attachment {i} format doesn't match pipeline format.");
        }
        _backend.SetPipeline(pipeline);
    }

    public void BindTexture(string name, GpuTextureView? textureView, GpuSampler? sampler)
        => _backend.BindTexture(name, textureView, sampler);

    public void SetUniform(string name, GpuBuffer value) => _backend.SetUniform(name, value);

    public void SetUniform(string name, GpuBufferSlice value)
    {
        int alignment = _device.GetDeviceInfo().Limits.MinUniformOffsetAlignment;
        if (value.Offset % alignment > 0)
            throw new ArgumentException($"Uniform buffer offset must be aligned to {alignment}");
        _backend.SetUniform(name, value);
    }

    public void EnableScissor(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException($"Scissor size must be >0, was {width}x{height}");
        var area = _renderArea!;
        if (x < area.X || y < area.Y || x + width > area.X + area.Width || y + height > area.Y + area.Height)
            throw new ArgumentException($"Scissor at {x}, {y} with size {width}x{height} is out of bounds for render area {area}");
        _backend.EnableScissor(x, y, width, height);
    }

    public void DisableScissor() => _backend.DisableScissor();

    public void SetVertexBuffer(int slot, GpuBufferSlice? vertexBuffer)
    {
        if (slot < 0 || slot >= MaxVertexBuffers)
            throw new ArgumentException($"Vertex buffer slot is out of range: {slot}");
        if (vertexBuffer != null && vertexBuffer.Buffer.IsClosed)
            throw new InvalidOperationException($"Vertex buffer at slot {slot} has been closed!");
        if (vertexBuffer != null && (vertexBuffer.Buffer.Usage & GpuBuffer.UsageVertex) == 0)
            throw new InvalidOperationException($"Vertex buffer at slot {slot} doesn't have GpuBuffer.USAGE_VERTEX flag!");
        _backend.SetVertexBuffer(slot, vertexBuffer);
    }

    public void SetIndexBuffer(GpuBuffer indexBuffer, IndexType indexType) => _backend.SetIndexBuffer(indexBuffer, indexType);

    public void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int vertexOffset, int firstInstance)
    {
        EnsureOpen();
        if (firstInstance != 0 && !_deviceFeatures.NonZeroFirstInstance)
            throw new NotSupportedException("firstInstance must be zero when device does not support nonZeroFirstInstance");
        _backend.DrawIndexed(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void MultiDrawIndexed(ReadOnlySpan<int> drawParameters, int instanceCount, int firstInstance, int drawCount)
    {
        EnsureOpen();
        if (!_deviceFeatures.MultiDrawDirectInterleaved)
            throw new NotSupportedException("device does not support multiDrawDirectInterleaved");
        if (firstInstance != 0 && !_deviceFeatures.NonZeroFirstInstance)
            throw new NotSupportedException("firstInstance must be zero when device does not support nonZeroFirstInstance");
        if (drawCount > _deviceLimits.MaxMultiDrawDirectInterleavedDrawCount)
            throw new ArgumentException("May not exceed maxMultiDrawDirectInterleavedDrawCount draws in a single multiDrawDirectInterleaved call");
        if (drawParameters.Length < drawCount * 3)
            throw new ArgumentException("Not enough elements in drawParameters for drawCount draws");
        _backend.MultiDrawIndexed(drawParameters, instanceCount, firstInstance, drawCount);
    }

    public void MultiDrawIndexed(ReadOnlySpan<int> firstIndexOffsets, ReadOnlySpan<int> indexCounts, ReadOnlySpan<int> vertexOffsets, int drawCount)
    {
        EnsureOpen();
        if (!_deviceFeatures.MultiDrawDirectSeparate)
            throw new NotSupportedException("device does not support multiDrawDirectSeparate");
        if (firstIndexOffsets.Length < drawCount)
            throw new ArgumentException("firstIndexOffsets does not contain enough elements for drawCount draws");
        if (indexCounts.Length < drawCount)
            throw new ArgumentException("indexCounts does not contain enough elements for drawCount draws");
        if (vertexOffsets.Length < drawCount)
            throw new ArgumentException("vertexOffsets does not contain enough elements for drawCount draws");
        _backend.MultiDrawIndexed(firstIndexOffsets, indexCounts, vertexOffsets, drawCount);
    }

    public void DrawIndexedIndirect(GpuBufferSlice commands, int drawCount)
    {
        EnsureOpen();
        if (!_deviceFeatures.DrawIndirect)
            throw new NotSupportedException("device does not support drawIndirect");
        if (drawCount > 1 && !_deviceFeatures.MultiDrawIndirect)
            throw new NotSupportedException("drawCount must be one when device does not support multiDrawIndirect");
        if ((commands.Buffer.Usage & GpuBuffer.UsageIndirectParameters) == 0)
            throw new ArgumentException("Indirect commands buffer must have GpuBuffer.USAGE_INDIRECT_PARAMETERS flag");
        if (commands.Length < (long)drawCount * DrawIndexedIndirectCommandSize)
            throw new ArgumentException("Commands buffer is not large enough to hold requested draw count at the given offset");
        if (commands.Offset % 4 != 0)
            throw new ArgumentException("Commands offset must be multiple of 4");
        _backend.DrawIndexedIndirect(commands, drawCount);
    }

    public void DrawMultipleIndexed<T>(IReadOnlyCollection<DrawCommand<T>> draws, GpuBuffer? defaultIndexBuffer, IndexType? defaultIndexType, IReadOnlyCollection<string> dynamicUniforms, T uniformArgument)
    {
        EnsureOpen();
        _backend.DrawMultipleIndexed(draws, defaultIndexBuffer, defaultIndexType, dynamicUniforms, uniformArgument);
    }

    public void Draw(int vertexCount, int instanceCount, int firstVertex, int firstInstance)
    {
        EnsureOpen();
        if (firstInstance != 0 && !_deviceFeatures.NonZeroFirstInstance)
            throw new NotSupportedException("firstInstance must be zero when device does not support nonZeroFirstInstance");
        _backend.Draw(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void MultiDraw(ReadOnlySpan<int> drawParameters, int instanceCount, int firstInstance, int drawCount)
    {
        EnsureOpen();
        if (!_deviceFeatures.MultiDrawDirectInterleaved)
            throw new NotSupportedException("device does not support multiDrawDirectInterleaved");
        if (firstInstance != 0 && !_deviceFeatures.NonZeroFirstInstance)
            throw new NotSupportedException("firstInstance must be zero when device does not support nonZeroFirstInstance");
        if (drawCount > _deviceLimits.MaxMultiDrawDirectInterleavedDrawCount)
            throw new ArgumentException("May not exceed maxMultiDrawDirectInterleavedDrawCount draws in a single multiDrawDirectInterleaved call");
        if (drawParameters.Length < drawCount * 2)
            throw new ArgumentException("Not enough elements in drawParameters for drawCount draws");
        _backend.MultiDraw(drawParameters, instanceCount, firstInstance, drawCount);
    }

    public void MultiDraw(ReadOnlySpan<int> firstVertices, ReadOnlySpan<int> vertexCounts, int drawCount)
    {
        EnsureOpen();
        if (!_deviceFeatures.MultiDrawDirectSeparate)
            throw new NotSupportedException("device does not support multiDrawDirectSeparate");
        if (firstVertices.Length < drawCount)
            throw new ArgumentException("firstVertices does not contain enough elements for drawCount draws");
        if (vertexCounts.Length < drawCount)
            throw new ArgumentException("vertexCounts does not contain enough elements for drawCount draws");
        _backend.MultiDraw(firstVertices, vertexCounts, drawCount);
    }

    public void DrawIndirect(GpuBufferSlice commands, int drawCount)
    {
        EnsureOpen();
        if (!_deviceFeatures.DrawIndirect)
            throw new NotSupportedException("device does not support drawIndirect");
        if (drawCount > 1 && !_deviceFeatures.MultiDrawIndirect)
            throw new NotSupportedException("drawCount must be one when device does not support multiDrawIndirect");
        if ((commands.Buffer.Usage & GpuBuffer.UsageIndirectParameters) == 0)
            throw new ArgumentException("Indirect commands buffer must have GpuBuffer.USAGE_INDIRECT_PARAMETERS flag");
        if (commands.Length < (long)drawCount * DrawIndirectCommandSize)
            throw new ArgumentException("Commands buffer is not large enough to hold requested draw count at the given offset");
        if (commands.Offset % 4 != 0)
            throw new ArgumentException("Commands offset must be multiple of 4");
        _backend.DrawIndirect(commands, drawCount);
    }

    public void Dispose()
    {
        if (_isClosed) return;
        _isClosed = true;
        if (_pushedDebugGroups > 0)
            throw new InvalidOperationException("Render pass had debug groups left open!");
        _onFinish();
    }

    private void EnsureOpen()
    {
        if (_isClosed)
            throw new InvalidOperationException("Can't use a closed render pass");
    }

    //RenderArea the pixel region a render pass covers, maps to vanilla RenderPass.RenderArea
    public sealed record RenderArea(int X, int Y, int Width, int Height)
    {
        public bool FillsTexture(GpuTextureView texture)
            => X == 0 && Y == 0 && Width == texture.GetWidth(0) && Height == texture.GetHeight(0);
    }

    //DrawCommand one indexed draw in a batched multi-draw, maps to vanilla RenderPass.Draw
    //C# forbids a nested type and a method sharing a name, so the record carries the Command suffix
    public sealed record DrawCommand<T>(
        int Slot,
        GpuBuffer VertexBuffer,
        GpuBuffer? IndexBuffer,
        IndexType? IndexType,
        int FirstIndex,
        int IndexCount,
        int BaseVertex,
        Action<T, UniformUploader>? UniformUploaderConsumer = null);

    //UniformUploader uploads a uniform slice for one draw, maps to vanilla RenderPass.UniformUploader
    public interface UniformUploader
    {
        void Upload(string name, GpuBufferSlice slice);
    }
}
