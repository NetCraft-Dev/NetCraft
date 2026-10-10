namespace NetCraft.Client.Blaze3d.Buffers;

//GpuBuffer GPU memory buffer, aligns with vanilla com.mojang.blaze3d.buffers.GpuBuffer
//Usage is a bitmask of the Usage* constants; the backend maps it to native buffer/memory flags
public abstract class GpuBuffer : IDisposable
{
    public const int UsageMapRead = 1;
    public const int UsageMapWrite = 2;
    public const int UsageHintClientStorage = 4;
    public const int UsageCopyDst = 8;
    public const int UsageCopySrc = 16;
    public const int UsageVertex = 32;
    public const int UsageIndex = 64;
    public const int UsageUniform = 128;
    public const int UsageUniformTexelBuffer = 256;
    public const int UsageIndirectParameters = 512;

    private readonly GpuBufferSlice _defaultSlice;

    //Size byte size
    public long Size { get; }
    //Usage usage bitmask
    public int Usage { get; }

    protected GpuBuffer(int usage, long size)
    {
        Size = size;
        Usage = usage;
        _defaultSlice = new GpuBufferSlice(this, 0, size);
    }

    //IsClosed whether the buffer has been disposed, maps to vanilla isClosed
    public abstract bool IsClosed { get; }

    public abstract void Dispose();

    //Slice returns the slice for a byte range, maps to vanilla slice(offset, length)
    public GpuBufferSlice Slice(long offset, long length)
    {
        if (offset < 0 || length < 0 || offset + length > Size)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Offset of {offset} and length {length} would put new slice outside buffer's range (of 0,{Size})");
        return new GpuBufferSlice(this, offset, length);
    }

    //Slice returns the whole-buffer default slice, maps to vanilla slice()
    public GpuBufferSlice Slice() => _defaultSlice;

    //Map maps the whole buffer for CPU access, maps to vanilla map(read, write)
    public GpuBufferSlice.MappedView Map(bool read, bool write) => Map(0, Size, read, write);

    //Map maps a byte range for CPU access, maps to vanilla map(offset, length, read, write)
    public abstract GpuBufferSlice.MappedView Map(long offset, long length, bool read, bool write);

    //Upload uploads a struct array to the GPU, a NetCraft convenience on top of the backend mapping
    public abstract void Upload<T>(ReadOnlySpan<T> data) where T : struct;

    //Download downloads data into a span, a NetCraft convenience on top of the backend mapping
    public abstract void Download<T>(Span<T> data) where T : struct;
}
