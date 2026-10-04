namespace NetCraft.Gpu;

//GpuShaderStage shader 阶段
public enum GpuShaderStage
{
    Vertex,
    Fragment,
    Compute
}

//GpuShader SPIR-V 字节码模块抽象对应原版 blaze3d Shader
//子类创建底层 shader module
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
