namespace NetCraft.Gpu;

//GpuBufferPool GPU buffer 池对标原版 GpuBufferPool
//跨帧复用 buffer 避免 GC 压力和 GPU buffer 创建开销
//GetBuffer 取或创建 ReturnBuffer 归还 EndFrame 批量回收
//当前单 frame-in-flight Submit 同步等待 buffer 立即可复用
//frame-in-flight 优化时扩展 fence 异步回收提交后 buffer 仍被 GPU 使用需 fence 完成才能复用
public sealed class GpuBufferPool : IDisposable
{
    private readonly Func<int, GpuBufferUsage, GpuBuffer> _factory;
    //available 按 usage 分桶每桶 List<(buffer, size)
    private readonly Dictionary<GpuBufferUsage, List<GpuBuffer>> _available = new();
    //inUse 当前帧借出的 buffer EndFrame 时批量归还
    private readonly List<GpuBuffer> _inUse = new();
    private bool _disposed;

    //GpuBufferPool(GpuDevice) 生产用 device.CreateBuffer 创建 HostVisible 优化的 buffer
    public GpuBufferPool(GpuDevice device)
        : this((size, usage) => device.CreateBuffer(size, usage))
    {
    }

    //GpuBufferPool(factory) 测试用注入 factory 不依赖真实 GpuDevice
    public GpuBufferPool(Func<int, GpuBufferUsage, GpuBuffer> factory)
    {
        _factory = factory;
    }

    //GetBuffer 取池中 size >= needed 的最小 buffer 没有则创建
    //借出的 buffer 加入 inUse EndFrame 时归还
    public GpuBuffer GetBuffer(int size, GpuBufferUsage usage)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GpuBufferPool));
        var bucket = GetOrCreateBucket(usage);
        //找 size >= needed 的最小 buffer 减少内存浪费
        var bestIndex = -1;
        var bestSize = int.MaxValue;
        for (int i = 0; i < bucket.Count; i++)
        {
            var b = bucket[i];
            if (b.Size >= size && b.Size < bestSize)
            {
                bestSize = b.Size;
                bestIndex = i;
            }
        }
        GpuBuffer buffer;
        if (bestIndex >= 0)
        {
            buffer = bucket[bestIndex];
            bucket.RemoveAt(bestIndex);
        }
        else
        {
            buffer = _factory(size, usage);
        }
        _inUse.Add(buffer);
        return buffer;
    }

    //ReturnBuffer 手动归还 buffer 到对应桶立即复用
    public void ReturnBuffer(GpuBuffer buffer)
    {
        if (buffer == null) return;
        _inUse.Remove(buffer);
        GetOrCreateBucket(buffer.Usage).Add(buffer);
    }

    //EndFrame 批量归还当前帧所有 inUse buffer 跨帧复用
    public void EndFrame()
    {
        foreach (var buffer in _inUse)
            GetOrCreateBucket(buffer.Usage).Add(buffer);
        _inUse.Clear();
    }

    //AvailableCount 指定 usage 的可用 buffer 数供单测验证池化
    public int AvailableCount(GpuBufferUsage usage)
        => _available.TryGetValue(usage, out var bucket) ? bucket.Count : 0;

    //InUseCount 当前帧借出的 buffer 数
    public int InUseCount => _inUse.Count;

    public void Dispose()
    {
        if (_disposed) return;
        foreach (var bucket in _available.Values)
            foreach (var b in bucket)
                b.Dispose();
        foreach (var b in _inUse)
            b.Dispose();
        _available.Clear();
        _inUse.Clear();
        _disposed = true;
    }

    private List<GpuBuffer> GetOrCreateBucket(GpuBufferUsage usage)
    {
        if (!_available.TryGetValue(usage, out var bucket))
        {
            bucket = new List<GpuBuffer>();
            _available[usage] = bucket;
        }
        return bucket;
    }
}
