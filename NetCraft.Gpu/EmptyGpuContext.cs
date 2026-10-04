namespace NetCraft.Gpu;

//EmptyGpuContext 空后端 GPU 上下文
//所有方法抛 NotSupportedException 用于无 GPU 环境保持编译通过
public sealed class EmptyGpuContext : GpuContext
{
    public EmptyGpuContext() : base(GpuBackend.Empty) { }

    public override GpuDevice CreateDevice(GpuDeviceOptions options)
        => new EmptyGpuDevice(this);
}

//EmptyGpuDevice 空后端 GPU 设备
//所有方法抛 NotSupportedException 占位骨架
internal sealed class EmptyGpuDevice : GpuDevice
{
    //EmptyLimits 空后端默认 4096 占位保证无 Vulkan 环境也能编译运行
    public override DeviceLimits Limits { get; } = new(4096);

    public EmptyGpuDevice(GpuContext context) : base(context) { }

    public override GpuCommandBuffer CreateCommandBuffer()
        => new EmptyGpuCommandBuffer();

    public override CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
        => new EmptyRenderPipeline(description);

    public override GpuBuffer CreateBuffer(int size, GpuBufferUsage usage)
        => throw new NotSupportedException("Empty 后端不支持创建 Buffer");

    public override GpuImage CreateImage(GpuImageDescription desc)
        => throw new NotSupportedException("Empty 后端不支持创建 Image");

    public override GpuShader CreateShader(GpuShaderStage stage, byte[] spirvCode, string entryPoint = "main")
        => throw new NotSupportedException("Empty 后端不支持创建 Shader");

    public override GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription description)
        => throw new NotSupportedException("Empty 后端不支持创建 DescriptorLayout");

    public override GpuDescriptorSet AllocateDescriptorSet(GpuDescriptorLayout layout)
        => throw new NotSupportedException("Empty 后端不支持分配 DescriptorSet");

    public override GpuSampler CreateSampler(GpuSamplerDescription description)
        => throw new NotSupportedException("Empty 后端不支持创建 Sampler");
}

//EmptyGpuCommandBuffer 空后端命令缓冲
internal sealed class EmptyGpuCommandBuffer : GpuCommandBuffer
{
    public override void BeginRecording() { }
    public override void BeginRenderPass(CompiledRenderPipeline pipeline) { }
    public override void BindPipeline(CompiledRenderPipeline pipeline) { }
    public override void BindVertexBuffer(GpuBuffer buffer, int binding = 0, ulong offset = 0) { }
    public override void BindIndexBuffer(GpuBuffer buffer, GpuIndexType indexType, ulong offset = 0) { }
    public override void BindDescriptorSet(GpuDescriptorSet set, uint setIndex = 0) { }
    public override void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0) { }
    public override void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int vertexOffset = 0, int firstInstance = 0) { }
    public override void SetScissor(int x, int y, int width, int height) { }
    public override void EndRenderPass() { }
    public override void EndRecording() { }
    public override void Submit() { }
}

//EmptyRenderPipeline 空后端渲染管线
internal sealed class EmptyRenderPipeline : CompiledRenderPipeline
{
    public EmptyRenderPipeline(RenderPipelineDescription description) : base(description) { }
}
