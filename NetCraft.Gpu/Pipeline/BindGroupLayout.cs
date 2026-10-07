namespace NetCraft.Gpu.Pipeline;

//UniformDescription uniform description, maps to vanilla UniformDescription record
//TEXEL_BUFFER must specify a GpuFormat; for other types GpuFormat is null
public readonly record struct UniformDescription(string Name, UniformType Type, GpuFormat? GpuFormat)
{
    public UniformDescription(string name, UniformType type) : this(name, type, null)
    {
        if (type == UniformType.TexelBuffer)
            throw new ArgumentException("Texel buffer needs a texture format");
    }

    public UniformDescription(string name, GpuFormat format) : this(name, UniformType.TexelBuffer, format) { }
}

//BindGroupLayout bind group layout, maps to vanilla BindGroupLayout
//Describes a set of sampler and uniform bindings for RenderPipeline to allocate a descriptor set layout at compile time
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
