namespace NetCraft.Gpu;

//GpuDescriptorType 描述符类型
public enum GpuDescriptorType
{
    UniformBuffer,
    CombinedImageSampler
}

//GpuShaderStageFlags shader 阶段可见位掩码
[Flags]
public enum GpuShaderStageFlags
{
    None = 0,
    Vertex = 1,
    Fragment = 2,
    Compute = 4,
    AllGraphics = Vertex | Fragment
}

//GpuDescriptorBinding 单个描述符绑定描述
public sealed class GpuDescriptorBinding
{
    public int Binding { get; set; }
    public GpuDescriptorType DescriptorType { get; set; }
    public GpuShaderStageFlags StageFlags { get; set; } = GpuShaderStageFlags.AllGraphics;
    public int DescriptorCount { get; set; } = 1;
}

//GpuDescriptorLayoutDescription 描述符集布局描述
public sealed class GpuDescriptorLayoutDescription
{
    public List<GpuDescriptorBinding> Bindings { get; set; } = new();
}

//GpuDescriptorLayout 描述符集布局抽象
//子类创建底层 layout 对象（Vulkan VkDescriptorSetLayout）
public abstract class GpuDescriptorLayout : IDisposable
{
    public GpuDescriptorLayoutDescription Description { get; }

    protected GpuDescriptorLayout(GpuDescriptorLayoutDescription description)
    {
        Description = description;
    }

    public virtual void Dispose() { }
}

//GpuDescriptorSet 描述符集实例
//WriteBuffer 绑定 uniform buffer 到指定 binding 槽
//WriteImage 绑定 combined image sampler 到指定 binding 槽
public abstract class GpuDescriptorSet : IDisposable
{
    public GpuDescriptorLayout Layout { get; }

    protected GpuDescriptorSet(GpuDescriptorLayout layout)
    {
        Layout = layout;
    }

    //WriteBuffer 绑定 uniform buffer range=-1 表示整个 buffer
    public abstract void WriteBuffer(int binding, GpuBuffer buffer, int offset = 0, int range = -1);

    //WriteImage 绑定 combined image sampler
    public abstract void WriteImage(int binding, GpuImage image, GpuSampler sampler);

    public virtual void Dispose() { }
}

//GpuSampler 纹理采样器抽象
public abstract class GpuSampler : IDisposable
{
    public virtual void Dispose() { }
}

//GpuSamplerDescription 采样器描述
public sealed class GpuSamplerDescription
{
    public bool LinearFilter { get; set; } = true;
    public bool RepeatAddress { get; set; } = false;
}
