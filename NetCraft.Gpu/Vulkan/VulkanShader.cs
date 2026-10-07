using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//VulkanShader Vulkan backend SPIR-V shader module
//Wraps VkShaderModule created by VulkanGpuDevice.CreateShader
public sealed unsafe class VulkanShader : GpuShader
{
    private readonly Vk _vk;
    private readonly Device _device;
    private ShaderModule _module;
    private bool _disposed;

    public ShaderModule Handle => _module;

    internal VulkanShader(Vk vk, Device device, GpuShaderStage stage, byte[] spirvCode, string entryPoint)
        : base(stage, spirvCode, entryPoint)
    {
        _vk = vk;
        _device = device;
        var createInfo = new ShaderModuleCreateInfo
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)spirvCode.Length
        };
        fixed (byte* codePtr = spirvCode)
        {
            createInfo.PCode = (uint*)codePtr;
            if (_vk.CreateShaderModule(_device, &createInfo, null, out _module) != Result.Success)
                throw new InvalidOperationException("ShaderModule creation failed");
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyShaderModule(_device, _module, null);
        _disposed = true;
    }
}
