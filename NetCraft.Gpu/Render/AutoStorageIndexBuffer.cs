using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//AutoStorageIndexBuffer 自动索引缓冲对标原版 AutoStorageIndexBuffer
//QUADS topology 自动生成 6 索引(0,1,2,2,3,0)避免每 Draw 单独建索引缓冲
//其他 topology 不生成索引直接非索引绘制
//CPU 暂存 List<uint> Upload 阶段才创建 GpuBuffer 上传单测不依赖 GpuDevice
public sealed class AutoStorageIndexBuffer : IDisposable
{
    //UInt32 索引足够 GUI 顶点规模单帧顶点数不会超 2^32
    private readonly List<uint> _indices = new();
    private GpuBuffer? _indexBuffer;
    private bool _disposed;

    //Count 索引总数用于上传前确定 buffer 大小
    public int Count => _indices.Count;

    //IndexBuffer 上传后的 GPU 索引缓冲 Upload 前为 null
    public GpuBuffer? IndexBuffer => _indexBuffer;

    //Append 对指定 topology 的 Draw 生成索引返回 firstIndex 和 indexCount
    //QUADS 每 4 顶点生成 6 索引(0,1,2,2,3,0)其他 topology 不生成返回 0
    //baseVertex 是该 Draw 在 vertex buffer 中的起始顶点偏移
    public (int firstIndex, int indexCount) Append(int baseVertex, int vertexCount, PrimitiveTopology topology)
    {
        if (topology != PrimitiveTopology.Quads)
            return (0, 0);
        var firstIndex = _indices.Count;
        var quads = vertexCount / 4;
        for (int i = 0; i < quads; i++)
        {
            var v = baseVertex + i * 4;
            //两个三角形 v0-v1-v2 和 v2-v3-v0 覆盖一个四边形
            _indices.Add((uint)(v + 0));
            _indices.Add((uint)(v + 1));
            _indices.Add((uint)(v + 2));
            _indices.Add((uint)(v + 2));
            _indices.Add((uint)(v + 3));
            _indices.Add((uint)(v + 0));
        }
        return (firstIndex, quads * 6);
    }

    //GetIndices 返回索引快照供单测验证顺序
    public ReadOnlySpan<uint> GetIndices() => _indices.ToArray();

    //Upload 创建 UInt32 index buffer 上传索引数据
    //buffer 跨帧复用 size 不够才重建 HostVisible 每帧 map+memcpy 重写内容
    //修复旧实现 _indexBuffer!=null 直接 return 导致第二帧索引数据没更新的 bug
    public void Upload(GpuDevice device)
    {
        if (_indices.Count == 0) return;
        var size = _indices.Count * sizeof(uint);
        if (_indexBuffer == null || _indexBuffer.Size < size)
        {
            _indexBuffer?.Dispose();
            _indexBuffer = device.CreateHostVisibleBuffer(size, GpuBufferUsage.IndexBuffer);
        }
        _indexBuffer.Upload<uint>(_indices.ToArray());
    }

    //EndFrame 重置索引列表保留 GPU buffer 跨帧复用
    //调用后 Upload 可再次为新帧上传
    public void EndFrame()
    {
        _indices.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _indexBuffer?.Dispose();
        _indexBuffer = null;
        _disposed = true;
    }
}
