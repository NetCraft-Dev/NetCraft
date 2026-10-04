namespace NetCraft.Gpu;

//GpuBufferUsage buffer 用途
//VertexBuffer 顶点缓冲
//IndexBuffer 索引缓冲
//UniformBuffer shader uniform 数据
//StagingBuffer 暂存中转用于上传下载数据
public enum GpuBufferUsage
{
    VertexBuffer,
    IndexBuffer,
    UniformBuffer,
    StagingBuffer
}

//GpuBuffer GPU 显存缓冲抽象对应原版 blaze3d VertexBuffer/IndexBuffer/UniformBuffer
//子类提供 Upload/Download 实现
public abstract class GpuBuffer : IDisposable
{
    //Size 字节数
    public int Size { get; }
    //Usage 用途
    public GpuBufferUsage Usage { get; }

    protected GpuBuffer(int size, GpuBufferUsage usage)
    {
        Size = size;
        Usage = usage;
    }

    //Upload 上传结构体数组到 GPU
    public abstract void Upload<T>(ReadOnlySpan<T> data) where T : struct;

    //Download 下载数据到 span 仅 StagingBuffer 适用
    public abstract void Download<T>(Span<T> data) where T : struct;

    public virtual void Dispose() { }
}
