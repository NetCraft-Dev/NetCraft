using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//VulkanShader Vulkan 后端 SPIR-V shader module
//包装 VkShaderModule 由 VulkanGpuDevice.CreateShader 创建
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
                throw new InvalidOperationException("ShaderModule 创建失败");
        }
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyShaderModule(_device, _module, null);
        _disposed = true;
    }
}
