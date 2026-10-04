using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace NetCraft.Gpu.Vulkan;

//VulkanBuffer Vulkan 后端 GPU buffer
//UniformBuffer/StagingBuffer 用 HostVisible+HostCoherent 内存直接 map 适合频繁更新
//VertexBuffer/IndexBuffer 用 DeviceLocal 内存通过 staging buffer 中转上传性能更优
//Download 仅 HostVisible 类型支持 DeviceLocal 类型抛 NotSupportedException
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

    //hostVisible true 强制 HostVisible 内存适合每帧更新的 vertex buffer 避免 staging 中转
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
            throw new InvalidOperationException($"Upload 数据 {bytes.Length} 超过 buffer 大小 {Size}");
        if (_hostVisible)
        {
            //HostVisible 直接 map+memcpy 适合 UniformBuffer 频繁更新
            void* mapped;
            if (_vk.MapMemory(_device, _memory, 0, (ulong)bytes.Length, 0, &mapped) != Result.Success)
                throw new InvalidOperationException("MapMemory 失败");
            bytes.CopyTo(new Span<byte>(mapped, bytes.Length));
            _vk.UnmapMemory(_device, _memory);
        }
        else
        {
            //DeviceLocal 通过 staging buffer 中转上传适合 VertexBuffer/IndexBuffer 一次性上传
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
            throw new NotSupportedException("DeviceLocal buffer 不支持 Download 需用 staging 中转");
        var bytes = MemoryMarshal.AsBytes(data);
        if (bytes.Length > Size)
            throw new InvalidOperationException($"Download 数据 {bytes.Length} 超过 buffer 大小 {Size}");
        void* mapped;
        if (_vk.MapMemory(_device, _memory, 0, (ulong)bytes.Length, 0, &mapped) != Result.Success)
            throw new InvalidOperationException("MapMemory 失败");
        new Span<byte>(mapped, bytes.Length).CopyTo(bytes);
        _vk.UnmapMemory(_device, _memory);
    }

    private static BufferUsageFlags ToVkUsage(GpuBufferUsage usage) => usage switch
    {
        GpuBufferUsage.VertexBuffer => BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
        GpuBufferUsage.IndexBuffer => BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
        GpuBufferUsage.UniformBuffer => BufferUsageFlags.UniformBufferBit,
        //StagingBuffer 双向中转 Upload 时 TransferSrc（CPU→image）Readback 时 TransferDst（image→CPU）
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
