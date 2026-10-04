namespace NetCraft.Gpu;

//GpuContext GPU 上下文对应渲染设备抽象
//提供创建 device/swapchain/pipeline 的工厂入口
//具体后端实现由子类提供（Vulkan/Software）
public abstract class GpuContext : IDisposable
{
    public GpuBackend Backend { get; }

    protected GpuContext(GpuBackend backend)
    {
        Backend = backend;
    }

    //CreateDevice 创建逻辑 GPU 设备
    public abstract GpuDevice CreateDevice(GpuDeviceOptions options);

    public virtual void Dispose() { }
}

//GpuDeviceOptions GPU 设备创建选项
public sealed class GpuDeviceOptions
{
    public bool EnableValidation { get; set; }
    public bool PreferDiscreteGpu { get; set; }
    public int FrameInFlightCount { get; set; } = 2;
}
