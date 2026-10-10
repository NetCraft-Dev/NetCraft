using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;

using CompiledRenderPipeline = NetCraft.Client.Blaze3d.Pipeline.CompiledRenderPipeline;
using RenderPipelineDescription = NetCraft.Client.Blaze3d.Pipeline.RenderPipelineDescription;
using RenderPipeline = NetCraft.Client.Blaze3d.Pipeline.RenderPipeline;
using PipelineCache = NetCraft.Client.Blaze3d.Pipeline.PipelineCache;
using BindGroupLayout = NetCraft.Client.Blaze3d.Pipeline.BindGroupLayout;
using ColorTargetState = NetCraft.Client.Blaze3d.Pipeline.ColorTargetState;
using DepthStencilState = NetCraft.Client.Blaze3d.Pipeline.DepthStencilState;
using NetCraft.Client.Blaze3d.Pipeline;
using CompareOp = Silk.NET.Vulkan.CompareOp;
using BlendFactor = Silk.NET.Vulkan.BlendFactor;
using BlendOp = Silk.NET.Vulkan.BlendOp;
using PolygonMode = Silk.NET.Vulkan.PolygonMode;
using PrimitiveTopology = Silk.NET.Vulkan.PrimitiveTopology;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;
namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanBuffer Vulkan backend GPU buffer
//Host-visible buffers (usage carries UsageMapRead/UsageMapWrite) are mapped directly, suited to frequent updates
//Device-local buffers upload through a staging buffer; Map on them is rejected
public sealed unsafe class VulkanBuffer : GpuBuffer
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanDevice _gpuDevice;
    private readonly bool _hostVisible;
    private Buffer _buffer;
    private DeviceMemory _memory;
    private int _mapRefCount;
    private bool _disposed;

    public Buffer Handle => _buffer;
    public DeviceMemory Memory => _memory;

    internal VulkanBuffer(Vk vk, Device device, VulkanDevice gpuDevice, string? label, int usage, long size)
        : base(usage, size)
    {
        _vk = vk;
        _device = device;
        _gpuDevice = gpuDevice;
        //Vanilla derives host visibility from the usage map bits
        _hostVisible = (usage & (GpuBuffer.UsageMapRead | GpuBuffer.UsageMapWrite)) != 0;
        var properties = _hostVisible
            ? MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            : MemoryPropertyFlags.DeviceLocalBit;
        var (buf, mem) = gpuDevice.CreateBufferInternal((ulong)size, ToVkUsage(usage), properties);
        _buffer = buf;
        _memory = mem;
    }

    public override bool IsClosed => _disposed;

    //Map maps a byte range for CPU access, maps to vanilla VulkanGpuBuffer.Direct.map
    public override GpuBufferSlice.MappedView Map(long offset, long length, bool read, bool write)
    {
        if (_disposed)
            throw new InvalidOperationException("Buffer already closed");
        if (!read && !write)
            throw new ArgumentException("At least read or write must be true");
        if (read && (Usage & GpuBuffer.UsageMapRead) == 0)
            throw new InvalidOperationException("Buffer is not readable");
        if (write && (Usage & GpuBuffer.UsageMapWrite) == 0)
            throw new InvalidOperationException("Buffer is not writable");
        if (offset < 0 || length < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset or length must be positive integer values");
        if (offset + length > Size)
            throw new ArgumentOutOfRangeException(nameof(length), $"Cannot map more data than this buffer can hold (attempting to map {length} bytes at offset {offset} from {Size} size buffer)");
        if (length > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(length), "Mapping buffer slice larger than 2GB is not supported");
        if (!_hostVisible)
            throw new NotSupportedException("Device-local buffers cannot be mapped; use a host-visible buffer");

        void* mapped;
        if (_vk.MapMemory(_device, _memory, 0, (ulong)Size, 0, &mapped) != Result.Success)
            throw new InvalidOperationException("MapMemory failed");
        _mapRefCount++;
        var slice = Slice(offset, length);
        return new GpuBufferSlice.MappedView(slice, (nint)mapped + (nint)offset, (int)length, () =>
        {
            _vk.UnmapMemory(_device, _memory);
            _mapRefCount--;
        });
    }

    public override void Upload<T>(ReadOnlySpan<T> data)
    {
        var bytes = MemoryMarshal.AsBytes(data);
        if (bytes.Length > Size)
            throw new InvalidOperationException($"Upload data {bytes.Length} exceeds buffer size {Size}");
        if (_hostVisible)
        {
            //Host-visible uses map+memcpy directly, suited to frequent uniform buffer updates
            void* mapped;
            if (_vk.MapMemory(_device, _memory, 0, (ulong)bytes.Length, 0, &mapped) != Result.Success)
                throw new InvalidOperationException("MapMemory failed");
            bytes.CopyTo(new Span<byte>(mapped, bytes.Length));
            _vk.UnmapMemory(_device, _memory);
        }
        else
        {
            //Device-local uploads via a staging buffer, suited to one-off vertex/index buffer uploads
            var staging = (VulkanBuffer)_gpuDevice.CreateBuffer("staging", GpuBuffer.UsageCopySrc | GpuBuffer.UsageCopyDst | GpuBuffer.UsageMapWrite, bytes.Length);
            try
            {
                staging.Upload(bytes);
                int copySize = bytes.Length;
                var dst = _buffer;
                _gpuDevice.RunOneTimeCommand(cmd =>
                {
                    var region = new BufferCopy
                    {
                        SrcOffset = 0,
                        DstOffset = 0,
                        Size = (ulong)copySize
                    };
                    _vk.CmdCopyBuffer(cmd, staging.Handle, dst, 1, &region);
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

    private static BufferUsageFlags ToVkUsage(int usage)
    {
        var flags = BufferUsageFlags.None;
        if ((usage & GpuBuffer.UsageCopyDst) != 0) flags |= BufferUsageFlags.TransferDstBit;
        if ((usage & GpuBuffer.UsageCopySrc) != 0) flags |= BufferUsageFlags.TransferSrcBit;
        if ((usage & GpuBuffer.UsageVertex) != 0) flags |= BufferUsageFlags.VertexBufferBit;
        if ((usage & GpuBuffer.UsageIndex) != 0) flags |= BufferUsageFlags.IndexBufferBit;
        if ((usage & GpuBuffer.UsageUniform) != 0) flags |= BufferUsageFlags.UniformBufferBit;
        if ((usage & GpuBuffer.UsageUniformTexelBuffer) != 0) flags |= BufferUsageFlags.UniformTexelBufferBit;
        if ((usage & GpuBuffer.UsageIndirectParameters) != 0) flags |= BufferUsageFlags.IndirectBufferBit;
        if (flags == BufferUsageFlags.None)
            throw new ArgumentOutOfRangeException(nameof(usage), "Usage carries no Vulkan buffer usage bits");
        return flags;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        if (_mapRefCount != 0)
            throw new InvalidOperationException("Attempt to close a mapped buffer");
        _vk.DestroyBuffer(_device, _buffer, null);
        _vk.FreeMemory(_device, _memory, null);
        _disposed = true;
    }
}
