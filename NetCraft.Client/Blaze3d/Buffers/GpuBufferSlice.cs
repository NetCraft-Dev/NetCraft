namespace NetCraft.Client.Blaze3d.Buffers;

//GpuBufferSlice a byte range of a GpuBuffer, aligns with vanilla com.mojang.blaze3d.buffers.GpuBufferSlice
public sealed class GpuBufferSlice
{
    public GpuBuffer Buffer { get; }
    public long Offset { get; }
    public long Length { get; }

    public GpuBufferSlice(GpuBuffer buffer, long offset, long length)
    {
        Buffer = buffer;
        Offset = offset;
        Length = length;
    }

    //Slice returns a sub-slice relative to this slice's offset
    public GpuBufferSlice Slice(long offset, long length)
    {
        if (offset < 0 || length < 0 || offset + length > Length)
            throw new ArgumentOutOfRangeException(nameof(offset), $"Offset of {offset} and length {length} would put new slice outside existing slice's range (of {Offset},{Length})");
        return new GpuBufferSlice(Buffer, Offset + offset, length);
    }

    //Map maps this slice for CPU access, maps to vanilla map(read, write)
    public MappedView Map(bool read, bool write) => Buffer.Map(Offset, Length, read, write);

    //MappedView a mapped CPU view of a buffer slice; disposing it unmaps, aligns with vanilla MappedView
    public sealed class MappedView : IDisposable
    {
        private readonly System.Action _onClose;
        private readonly nint _pointer;
        private readonly int _length;
        private bool _closed;

        public GpuBufferSlice Slice { get; }

        public MappedView(GpuBufferSlice slice, nint pointer, int length, System.Action onClose)
        {
            Slice = slice;
            _pointer = pointer;
            _length = length;
            _onClose = onClose;
        }

        //Data the mapped bytes; writes go straight to the mapped GPU memory
        public unsafe Span<byte> Data => new Span<byte>((void*)_pointer, _length);

        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            _onClose();
        }
    }
}
