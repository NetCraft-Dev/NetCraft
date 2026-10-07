namespace NetCraft.Gpu;

//GpuContext GPU context, the render device abstraction
//Provides factory entry points for creating device/swapchain/pipeline
//Concrete backends are provided by subclasses (Vulkan/Software)
public abstract class GpuContext : IDisposable
{
    public GpuBackend Backend { get; }

    protected GpuContext(GpuBackend backend)
    {
        Backend = backend;
    }

    //CreateDevice creates the logical GPU device
    public abstract GpuDevice CreateDevice(GpuDeviceOptions options);

    public virtual void Dispose() { }
}

//GpuDeviceOptions GPU device creation options
public sealed class GpuDeviceOptions
{
    public bool EnableValidation { get; set; }
    public bool PreferDiscreteGpu { get; set; }
    public int FrameInFlightCount { get; set; } = 2;
}
