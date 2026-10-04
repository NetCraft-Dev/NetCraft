namespace NetCraft.Gpu.Pipeline;

//UniformDescription uniform 描述对标原版 UniformDescription record
//TEXEL_BUFFER 类型必须指定 GpuFormat 其他类型 GpuFormat 为 null
public readonly record struct UniformDescription(string Name, UniformType Type, GpuFormat? GpuFormat)
{
    public UniformDescription(string name, UniformType type) : this(name, type, null)
    {
        if (type == UniformType.TexelBuffer)
            throw new ArgumentException("Texel buffer needs a texture format");
    }

    public UniformDescription(string name, GpuFormat format) : this(name, UniformType.TexelBuffer, format) { }
}

//BindGroupLayout 绑定组布局对标原版 BindGroupLayout
//描述一组 sampler 和 uniform 绑定供 RenderPipeline 编译时分配 descriptor set layout
public sealed class BindGroupLayout
{
    public IReadOnlyList<string> Samplers { get; }
    public IReadOnlyList<UniformDescription> Uniforms { get; }

    private BindGroupLayout(IReadOnlyList<string> samplers, IReadOnlyList<UniformDescription> uniforms)
    {
        Samplers = samplers;
        Uniforms = uniforms;
    }

    public static Builder Create() => new();

    public sealed class Builder
    {
        private readonly List<string> _samplers = new();
        private readonly List<UniformDescription> _uniforms = new();

        public Builder AddSampler(string name)
        {
            _samplers.Add(name);
            return this;
        }

        public Builder AddUniform(string name, UniformType type)
        {
            _uniforms.Add(new UniformDescription(name, type));
            return this;
        }

        public Builder AddUniform(string name, UniformType type, GpuFormat format)
        {
            _uniforms.Add(new UniformDescription(name, type, format));
            return this;
        }

        public BindGroupLayout Build() => new(_samplers, _uniforms);
    }
}
