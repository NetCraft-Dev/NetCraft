using NetCraft.Client.Blaze3d.Buffers;

namespace NetCraft.Client.Blaze3d.Systems;

//TransientMemory transient GPU/CPU memory allocator, aligns with vanilla com.mojang.blaze3d.systems.TransientMemory
//Allocations live for the current frame and are reclaimed when the frame's command encoder submits
public interface TransientMemory
{
    //AllocateCpu allocates CPU-side memory for staging writes
    GpuBufferSlice.MappedView AllocateCpu(long size, long alignment, long minimumAllocation, long elementSize);

    //AllocateStaging allocates a mapped staging buffer
    GpuBufferSlice.MappedView AllocateStaging(long size, long alignment, int usage, long minimumAllocation, long elementSize);

    //AllocateGpu allocates a GPU buffer without mapping it
    GpuBufferSlice AllocateGpu(long size, long alignment, int usage, long minimumAllocation, long elementSize);

    //AllocateGpuMapped allocates a mapped GPU buffer
    GpuBufferSlice.MappedView AllocateGpuMapped(long size, long alignment, int usage, long minimumAllocation, long elementSize);

    //UploadStaging copies data into staging memory and returns the slice holding it
    GpuBufferSlice UploadStaging(ReadOnlySpan<byte> data, long alignment, int usage, long minimumAllocation, long elementSize);

    //UploadGpu copies data into GPU memory and returns the slice holding it
    GpuBufferSlice UploadGpu(ReadOnlySpan<byte> data, long alignment, int usage, long minimumAllocation, long elementSize);

    //MultiUploadStaging copies several data blocks into staging memory, returning one slice per block
    IReadOnlyList<GpuBufferSlice> MultiUploadStaging(IReadOnlyList<ReadOnlyMemory<byte>> data, long alignment, int usage);

    //MultiUploadGpu copies several data blocks into GPU memory, returning one slice per block
    IReadOnlyList<GpuBufferSlice> MultiUploadGpu(IReadOnlyList<ReadOnlyMemory<byte>> data, long alignment, int usage);
}
