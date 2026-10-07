namespace NetCraft.Gpu;

//GpuBufferPool GPU buffer pool, maps to vanilla GpuBufferPool
//Reuses buffers across frames to avoid GC pressure and GPU buffer creation cost
//GetBuffer gets or creates ReturnBuffer returns EndFrame batch-reclaims
//Currently single frame-in-flight; Submit waits synchronously so buffers are immediately reusable
//A frame-in-flight optimization would add fences for async reclaim; after submit the buffer is still in GPU use and cannot be reused until the fence completes
public sealed class GpuBufferPool : IDisposable
{
    private readonly Func<int, GpuBufferUsage, GpuBuffer> _factory;
    //available bucketed by usage, each bucket a List<(buffer, size)
    private readonly Dictionary<GpuBufferUsage, List<GpuBuffer>> _available = new();
    //inUse buffers lent out this frame, batch-returned at EndFrame
    private readonly List<GpuBuffer> _inUse = new();
    private bool _disposed;

    //GpuBufferPool(GpuDevice) production uses device.CreateBuffer to create host-visible optimized buffers
    public GpuBufferPool(GpuDevice device)
        : this((size, usage) => device.CreateBuffer(size, usage))
    {
    }

    //GpuBufferPool(factory) tests inject a factory without depending on a real GpuDevice
    public GpuBufferPool(Func<int, GpuBufferUsage, GpuBuffer> factory)
    {
        _factory = factory;
    }

    //GetBuffer takes the smallest pooled buffer with size >= needed, creating one if none
    //The lent buffer is added to inUse and returned at EndFrame
    public GpuBuffer GetBuffer(int size, GpuBufferUsage usage)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(GpuBufferPool));
        var bucket = GetOrCreateBucket(usage);
        //Finds the smallest buffer with size >= needed to reduce memory waste
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

    //ReturnBuffer manually returns a buffer to its bucket for immediate reuse
    public void ReturnBuffer(GpuBuffer buffer)
    {
        if (buffer == null) return;
        _inUse.Remove(buffer);
        GetOrCreateBucket(buffer.Usage).Add(buffer);
    }

    //EndFrame batch-returns all inUse buffers of the frame for cross-frame reuse
    public void EndFrame()
    {
        foreach (var buffer in _inUse)
            GetOrCreateBucket(buffer.Usage).Add(buffer);
        _inUse.Clear();
    }

    //AvailableCount available buffer count for a usage, for unit tests to verify pooling
    public int AvailableCount(GpuBufferUsage usage)
        => _available.TryGetValue(usage, out var bucket) ? bucket.Count : 0;

    //InUseCount buffers lent out this frame
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
