using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//VulkanDescriptorLayout Vulkan descriptor set layout
//Wraps VkDescriptorSetLayout for PipelineLayout creation and DescriptorSet allocation
public sealed unsafe class VulkanDescriptorLayout : GpuDescriptorLayout
{
    private readonly Vk _vk;
    private readonly Device _device;
    private DescriptorSetLayout _handle;
    private bool _disposed;

    public DescriptorSetLayout Handle => _handle;

    internal VulkanDescriptorLayout(Vk vk, Device device, GpuDescriptorLayoutDescription description) : base(description)
    {
        _vk = vk;
        _device = device;
        var bindings = new DescriptorSetLayoutBinding[description.Bindings.Count];
        for (int i = 0; i < description.Bindings.Count; i++)
        {
            var b = description.Bindings[i];
            bindings[i] = new DescriptorSetLayoutBinding
            {
                Binding = (uint)b.Binding,
                DescriptorType = ToVkDescriptorType(b.DescriptorType),
                DescriptorCount = (uint)b.DescriptorCount,
                StageFlags = ToVkStageFlags(b.StageFlags)
            };
        }
        fixed (DescriptorSetLayoutBinding* bPtr = bindings)
        {
            var layoutInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)bindings.Length,
                PBindings = bPtr
            };
            if (_vk.CreateDescriptorSetLayout(_device, &layoutInfo, null, out _handle) != Result.Success)
                throw new InvalidOperationException("DescriptorSetLayout creation failed");
        }
    }

    private static DescriptorType ToVkDescriptorType(GpuDescriptorType type) => type switch
    {
        GpuDescriptorType.UniformBuffer => DescriptorType.UniformBuffer,
        GpuDescriptorType.CombinedImageSampler => DescriptorType.CombinedImageSampler,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static ShaderStageFlags ToVkStageFlags(GpuShaderStageFlags flags)
    {
        ShaderStageFlags result = 0;
        if ((flags & GpuShaderStageFlags.Vertex) != 0) result |= ShaderStageFlags.VertexBit;
        if ((flags & GpuShaderStageFlags.Fragment) != 0) result |= ShaderStageFlags.FragmentBit;
        if ((flags & GpuShaderStageFlags.Compute) != 0) result |= ShaderStageFlags.ComputeBit;
        return result;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyDescriptorSetLayout(_device, _handle, null);
        _disposed = true;
    }
}

//VulkanDescriptorSet Vulkan descriptor set instance
//WriteBuffer binds a uniform buffer WriteImage binds a combined image sampler
public sealed unsafe class VulkanDescriptorSet : GpuDescriptorSet
{
    private readonly Vk _vk;
    private readonly Device _device;
    private DescriptorSet _handle;

    public DescriptorSet Handle => _handle;

    internal VulkanDescriptorSet(Vk vk, Device device, VulkanDescriptorLayout layout, DescriptorSet handle) : base(layout)
    {
        _vk = vk;
        _device = device;
        _handle = handle;
    }

    //WriteBuffer binds a uniform buffer; range=-1 uses the whole buffer size
    public override void WriteBuffer(int binding, GpuBuffer buffer, int offset = 0, int range = -1)
    {
        var vkBuffer = (VulkanBuffer)buffer;
        var desc = new DescriptorBufferInfo
        {
            Buffer = vkBuffer.Handle,
            Offset = (ulong)offset,
            Range = range < 0 ? (ulong)vkBuffer.Size : (ulong)range
        };
        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _handle,
            DstBinding = (uint)binding,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            PBufferInfo = &desc
        };
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }

    //WriteImage binds a combined image sampler; the image must be in ShaderReadOnlyOptimal layout
    public override void WriteImage(int binding, GpuImage image, GpuSampler sampler)
    {
        var vkImage = (VulkanImage)image;
        var vkSampler = (VulkanSampler)sampler;
        var desc = new DescriptorImageInfo
        {
            Sampler = vkSampler.Handle,
            ImageView = vkImage.View,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };
        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _handle,
            DstBinding = (uint)binding,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &desc
        };
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }
}

//VulkanSampler Vulkan texture sampler
public sealed unsafe class VulkanSampler : GpuSampler
{
    private readonly Vk _vk;
    private readonly Device _device;
    private Sampler _handle;
    private bool _disposed;

    public Sampler Handle => _handle;

    internal VulkanSampler(Vk vk, Device device, GpuSamplerDescription desc)
    {
        _vk = vk;
        _device = device;
        var info = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = desc.LinearFilter ? Filter.Linear : Filter.Nearest,
            MinFilter = desc.LinearFilter ? Filter.Linear : Filter.Nearest,
            AddressModeU = desc.RepeatAddress ? SamplerAddressMode.Repeat : SamplerAddressMode.ClampToEdge,
            AddressModeV = desc.RepeatAddress ? SamplerAddressMode.Repeat : SamplerAddressMode.ClampToEdge,
            AddressModeW = desc.RepeatAddress ? SamplerAddressMode.Repeat : SamplerAddressMode.ClampToEdge,
            AnisotropyEnable = Vk.False,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = Vk.False,
            CompareEnable = Vk.False,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear
        };
        if (_vk.CreateSampler(_device, &info, null, out _handle) != Result.Success)
            throw new InvalidOperationException("Sampler creation failed");
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroySampler(_device, _handle, null);
        _disposed = true;
    }
}
