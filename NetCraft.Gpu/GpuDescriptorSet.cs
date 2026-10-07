namespace NetCraft.Gpu;

//GpuDescriptorType descriptor type
public enum GpuDescriptorType
{
    UniformBuffer,
    CombinedImageSampler
}

//GpuShaderStageFlags shader stage visibility bitmask
[Flags]
public enum GpuShaderStageFlags
{
    None = 0,
    Vertex = 1,
    Fragment = 2,
    Compute = 4,
    AllGraphics = Vertex | Fragment
}

//GpuDescriptorBinding description of a single descriptor binding
public sealed class GpuDescriptorBinding
{
    public int Binding { get; set; }
    public GpuDescriptorType DescriptorType { get; set; }
    public GpuShaderStageFlags StageFlags { get; set; } = GpuShaderStageFlags.AllGraphics;
    public int DescriptorCount { get; set; } = 1;
}

//GpuDescriptorLayoutDescription descriptor set layout description
public sealed class GpuDescriptorLayoutDescription
{
    public List<GpuDescriptorBinding> Bindings { get; set; } = new();
}

//GpuDescriptorLayout descriptor set layout abstraction
//Subclasses create the underlying layout object (Vulkan VkDescriptorSetLayout)
public abstract class GpuDescriptorLayout : IDisposable
{
    public GpuDescriptorLayoutDescription Description { get; }

    protected GpuDescriptorLayout(GpuDescriptorLayoutDescription description)
    {
        Description = description;
    }

    public virtual void Dispose() { }
}

//GpuDescriptorSet descriptor set instance
//WriteBuffer binds a uniform buffer to the given binding slot
//WriteImage binds a combined image sampler to the given binding slot
public abstract class GpuDescriptorSet : IDisposable
{
    public GpuDescriptorLayout Layout { get; }

    protected GpuDescriptorSet(GpuDescriptorLayout layout)
    {
        Layout = layout;
    }

    //WriteBuffer binds a uniform buffer; range=-1 means the whole buffer
    public abstract void WriteBuffer(int binding, GpuBuffer buffer, int offset = 0, int range = -1);

    //WriteImage binds a combined image sampler
    public abstract void WriteImage(int binding, GpuImage image, GpuSampler sampler);

    public virtual void Dispose() { }
}

//GpuSampler texture sampler abstraction
public abstract class GpuSampler : IDisposable
{
    public virtual void Dispose() { }
}

//GpuSamplerDescription sampler description
public sealed class GpuSamplerDescription
{
    public bool LinearFilter { get; set; } = true;
    public bool RepeatAddress { get; set; } = false;
}
