using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//GpuDevice GPU 逻辑设备对应原版 RenderSystem 抽象
//提供命令缓冲分配和资源创建入口
public abstract class GpuDevice : IDisposable
{
    public GpuContext Context { get; }
    //ShaderManager 声明式 pipeline shader 加载编译入口阶段 8 补完 FromDeclaration shader 加载
    public ShaderManager ShaderManager { get; }

    //Limits GPU 设备硬件限制对标原版 device.getDeviceInfo().limits()
    //子类查询后端真实值 Vulkan 走 VkPhysicalDeviceLimits.maxImageDimension2D
    //Empty/Mock 后端用默认 4096 占位保证无 Vulkan 环境也能编译运行
    public abstract DeviceLimits Limits { get; }

    //SupportsGpuRendering 是否支持录制 GPU 渲染命令 Vulkan 后端 true Empty/Mock 后端 false
    //ItemItemAtlas.DrawToSlot 用此判断走 GPU 渲染或仅 CPU 顶点生成
    public virtual bool SupportsGpuRendering => false;

    protected GpuDevice(GpuContext context)
    {
        Context = context;
        ShaderManager = new ShaderManager();
    }

    //CreateCommandBuffer 创建命令缓冲用于录制渲染命令
    public abstract GpuCommandBuffer CreateCommandBuffer();

    //CreateRenderPipeline 创建渲染管线
    public abstract CompiledRenderPipeline CreateRenderPipeline(RenderPipelineDescription description);

    //CreateBuffer 创建 GPU buffer
    public abstract GpuBuffer CreateBuffer(int size, GpuBufferUsage usage);

    //CreateHostVisibleBuffer 创建 HostVisible 内存 buffer 适合每帧更新的 vertex/index buffer
    //默认实现回退到 CreateBuffer 走 DeviceLocal+staging 子类可 override 提供 HostVisible 优化
    //HostVisible 走 map+memcpy 避免 staging 的 QueueSubmit+QueueWaitIdle 同步开销
    public virtual GpuBuffer CreateHostVisibleBuffer(int size, GpuBufferUsage usage)
        => CreateBuffer(size, usage);

    //CreateImage 创建 GPU 图像/纹理
    public abstract GpuImage CreateImage(GpuImageDescription desc);

    //CreateShader 创建 SPIR-V shader module
    public abstract GpuShader CreateShader(GpuShaderStage stage, byte[] spirvCode, string entryPoint = "main");

    //CreateDescriptorLayout 创建描述符集布局
    public abstract GpuDescriptorLayout CreateDescriptorLayout(GpuDescriptorLayoutDescription description);

    //AllocateDescriptorSet 从内部 pool 分配一个描述符集
    public abstract GpuDescriptorSet AllocateDescriptorSet(GpuDescriptorLayout layout);

    //CreateSampler 创建纹理采样器
    public abstract GpuSampler CreateSampler(GpuSamplerDescription description);

    //CreateCommandEncoder 创建命令编码器录制 copy/render pass 命令
    //旧后端不支持抛 NotSupportedException Vulkan 后端 override 实现
    public virtual ICommandEncoder CreateCommandEncoder() =>
        throw new NotSupportedException("当前后端不支持 ICommandEncoder");

    //PrecompilePipeline 编译声明式 RenderPipeline 为 CompiledRenderPipeline
    //默认实现把声明式转换为 RenderPipelineDescription 再编译 descriptor layout 后 CreateRenderPipeline
    //子类可 override 接入 PipelineCache 缓存编译产物
    public virtual CompiledRenderPipeline PrecompilePipeline(RenderPipeline declaration)
    {
        var description = RenderPipelineDescription.FromDeclaration(declaration, ShaderManager);
        foreach (var layoutDesc in description.DescriptorLayoutDescriptions)
            description.DescriptorLayouts.Add(CreateDescriptorLayout(layoutDesc));
        description.DescriptorLayoutDescriptions.Clear();
        return CreateRenderPipeline(description);
    }

    //PrecompilePipeline 兼容旧 RenderPipelineDescription 调用方不缓存
    public virtual CompiledRenderPipeline PrecompilePipeline(RenderPipelineDescription description) =>
        CreateRenderPipeline(description);

    public virtual void Dispose() { }
}
