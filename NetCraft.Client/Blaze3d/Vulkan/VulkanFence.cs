using Silk.NET.Vulkan;
using NetCraft.Client.Blaze3d.Buffers;

namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanFence VkFence wrapper implementing GpuFence
internal sealed unsafe class VulkanFence : GpuFence
{
    private readonly Vk _vk;
    private readonly Device _device;
    private Fence _fence;
    private bool _disposed;

    public Fence Handle => _fence;

    public VulkanFence(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
        var info = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        if (_vk.CreateFence(_device, &info, null, &_fence) != Result.Success)
            throw new InvalidOperationException("Fence creation failed");
    }

    public bool AwaitCompletion(long timeoutNS)
    {
        var fence = _fence;
        var result = _vk.WaitForFences(_device, 1, &fence, Vk.True, timeoutNS < 0 ? ulong.MaxValue : (ulong)timeoutNS);
        return result == Result.Success;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyFence(_device, _fence, null);
        _disposed = true;
    }
}
