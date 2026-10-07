using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace NetCraft.Gpu.Vulkan;

//VulkanBuffer Vulkan backend GPU buffer
//UniformBuffer/StagingBuffer use host-visible+host-coherent memory mapped directly, suited to frequent updates
//VertexBuffer/IndexBuffer use device-local memory uploaded via a staging buffer for better performance
//Download is only supported for host-visible types; device-local types throw NotSupportedException
public sealed unsafe class VulkanBuffer : GpuBuffer
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanGpuDevice _gpuDevice;
    private readonly bool _hostVisible;
    private Buffer _buffer;
    private DeviceMemory _memory;
    private bool _disposed;

    public Buffer Handle => _buffer;
    public DeviceMemory Memory => _memory;

    internal VulkanBuffer(Vk vk, Device device, VulkanGpuDevice gpuDevice, int size, GpuBufferUsage usage)
        : this(vk, device, gpuDevice, size, usage,
               usage == GpuBufferUsage.UniformBuffer || usage == GpuBufferUsage.StagingBuffer)
    {
    }

    //hostVisible true forces host-visible memory, suited to per-frame vertex buffer updates without staging
    internal VulkanBuffer(Vk vk, Device device, VulkanGpuDevice gpuDevice, int size, GpuBufferUsage usage, bool hostVisible)
        : base(size, usage)
    {
        _vk = vk;
        _device = device;
        _gpuDevice = gpuDevice;
        _hostVisible = hostVisible;
        var properties = _hostVisible
            ? MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            : MemoryPropertyFlags.DeviceLocalBit;
        var (buf, mem) = gpuDevice.CreateBufferInternal((ulong)size, ToVkUsage(usage), properties);
        _buffer = buf;
        _memory = mem;
    }

    public override void Upload<T>(ReadOnlySpan<T> data)
    {
        var bytes = MemoryMarshal.AsBytes(data);
        if (bytes.Length > Size)
            throw new InvalidOperationException($"Upload data {bytes.Length} exceeds buffer size {Size}");
        if (_hostVisible)
        {
            //Host-visible uses map+memcpy directly, suited to frequent UniformBuffer updates
            void* mapped;
            if (_vk.MapMemory(_device, _memory, 0, (ulong)bytes.Length, 0, &mapped) != Result.Success)
                throw new InvalidOperationException("MapMemory failed");
            bytes.CopyTo(new Span<byte>(mapped, bytes.Length));
            _vk.UnmapMemory(_device, _memory);
        }
        else
        {
            //Device-local uploads via a staging buffer, suited to one-off VertexBuffer/IndexBuffer uploads
            var staging = (VulkanBuffer)_gpuDevice.CreateBuffer(bytes.Length, GpuBufferUsage.StagingBuffer);
            try
            {
                staging.Upload(bytes);
                int copySize = bytes.Length;
                _gpuDevice.RunOneTimeCommand(cmd =>
                {
                    var region = new BufferCopy
                    {
                        SrcOffset = 0,
                        DstOffset = 0,
                        Size = (ulong)copySize
                    };
                    _vk.CmdCopyBuffer(cmd, staging.Handle, _buffer, 1, &region);
                });
            }
            finally
            {
                staging.Dispose();
            }
        }
    }

    public override void Download<T>(Span<T> data)
    {
        if (!_hostVisible)
            throw new NotSupportedException("Device-local buffers do not support Download; use staging");
        var bytes = MemoryMarshal.AsBytes(data);
        if (bytes.Length > Size)
            throw new InvalidOperationException($"Download data {bytes.Length} exceeds buffer size {Size}");
        void* mapped;
        if (_vk.MapMemory(_device, _memory, 0, (ulong)bytes.Length, 0, &mapped) != Result.Success)
            throw new InvalidOperationException("MapMemory failed");
        new Span<byte>(mapped, bytes.Length).CopyTo(bytes);
        _vk.UnmapMemory(_device, _memory);
    }

    private static BufferUsageFlags ToVkUsage(GpuBufferUsage usage) => usage switch
    {
        GpuBufferUsage.VertexBuffer => BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
        GpuBufferUsage.IndexBuffer => BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
        GpuBufferUsage.UniformBuffer => BufferUsageFlags.UniformBufferBit,
        //StagingBuffer is bidirectional: TransferSrc during Upload (CPU→image) and TransferDst during Readback (image→CPU)
        GpuBufferUsage.StagingBuffer => BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit,
        _ => throw new ArgumentOutOfRangeException(nameof(usage))
    };

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyBuffer(_device, _buffer, null);
        _vk.FreeMemory(_device, _memory, null);
        _disposed = true;
    }
}
