namespace NetCraft.Client.Blaze3d.Shaders;

//GpuShaderStage shader stage
public enum GpuShaderStage
{
    Vertex,
    Fragment,
    Compute
}

//GpuShader SPIR-V bytecode module abstraction, corresponds to vanilla blaze3d Shader
//Subclasses create the underlying shader module
public abstract class GpuShader : IDisposable
{
    public GpuShaderStage Stage { get; }
    public byte[] SpirvCode { get; }
    public string EntryPoint { get; }

    protected GpuShader(GpuShaderStage stage, byte[] spirvCode, string entryPoint = "main")
    {
        Stage = stage;
        SpirvCode = spirvCode;
        EntryPoint = entryPoint;
    }

    public virtual void Dispose() { }
}
