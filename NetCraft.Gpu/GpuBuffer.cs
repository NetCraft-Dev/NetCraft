namespace NetCraft.Gpu;

//GpuBufferUsage buffer usage
//VertexBuffer vertex buffer
//IndexBuffer index buffer
//UniformBuffer shader uniform data
//StagingBuffer staging area for uploading and downloading data
public enum GpuBufferUsage
{
    VertexBuffer,
    IndexBuffer,
    UniformBuffer,
    StagingBuffer
}

//GpuBuffer GPU memory buffer abstraction, corresponds to vanilla blaze3d VertexBuffer/IndexBuffer/UniformBuffer
//Subclasses provide the Upload/Download implementations
public abstract class GpuBuffer : IDisposable
{
    //Size byte size
    public int Size { get; }
    //Usage usage
    public GpuBufferUsage Usage { get; }

    protected GpuBuffer(int size, GpuBufferUsage usage)
    {
        Size = size;
        Usage = usage;
    }

    //Upload uploads a struct array to the GPU
    public abstract void Upload<T>(ReadOnlySpan<T> data) where T : struct;

    //Download downloads data into a span, only applicable to StagingBuffer
    public abstract void Download<T>(Span<T> data) where T : struct;

    public virtual void Dispose() { }
}
