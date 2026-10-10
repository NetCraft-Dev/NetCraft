using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using SilkWindow = Silk.NET.Windowing.Window;
using Image = Silk.NET.Vulkan.Image;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Render;

namespace NetCraft.Client.Blaze3d.Platform;

//VulkanAppBase common base class for the Vulkan sample apps
//Owns the window, the backend, the logical device and the surface; subclasses record their rendering into an offscreen target
//The frame loop acquires a swapchain image, records into the scene texture, blits it to the swapchain and presents
public abstract unsafe class VulkanAppBase : IDisposable
{
    protected IWindow _window = null!;
    protected VulkanBackend _backend = null!;
    protected VulkanDevice _vkDevice = null!;
    protected GpuDevice _device = null!;
    protected VulkanGpuSurface _vkSurface = null!;
    protected GpuSurface _surface = null!;
    protected CommandEncoder _encoder = null!;
    protected GpuTexture _sceneColor = null!;
    protected GpuTextureView _sceneView = null!;
    protected Format _swapchainImageFormat;
    protected Extent2D _swapchainExtent;
    protected uint _framesRendered;
    private int _maxFrames = -1;
    protected bool _disposed;
    protected bool _initialized;
    //The window-size-changed flag is set by the Resize event; DrawFrame detects it and triggers swapchain recreation
    protected bool _framebufferResized;
    //EnableVsync whether vsync is enabled, true by default; subclasses set it from GameConfig at construction
    protected bool EnableVsync = true;

    private readonly ShaderManager _shaderManager = new();
    private GpuSurface.PresentMode _presentMode;

    public int SurfaceWidth { get; }
    public int SurfaceHeight { get; }
    public Vk Api => _vkDevice.Api;

    protected VulkanAppBase(int width, int height)
    {
        SurfaceWidth = width;
        SurfaceHeight = height;
    }

    //WindowTitle window title provided by subclasses
    protected abstract string WindowTitle { get; }

    //OnCreatePipelineResources subclasses create pipelines and dependent resources
    //_vkDevice/_swapchainImageFormat/_swapchainExtent are ready at this point
    protected abstract void OnCreatePipelineResources();

    //OnRecordCommandBuffer subclasses record their rendering into the scene texture
    protected abstract void OnRecordCommandBuffer(CommandEncoder encoder, GpuTextureView sceneView);

    //OnSwapchainRecreated after swapchain recreation subclasses can rebuild extent-dependent resources like the pipeline viewport
    protected virtual void OnSwapchainRecreated() { }

    //OnCleanupPipelineResources subclasses clean up their own pipeline resources while the device is not yet destroyed
    protected virtual void OnCleanupPipelineResources() { }

    //OnInitialized fired after the window and Vulkan resources are initialized; subclasses may override to subscribe to input events
    protected virtual void OnInitialized() { }

    //OnBeforeRun hook before the main loop's _window.Run; subclasses may start background tasks like the Tick thread
    protected virtual void OnBeforeRun() { }

    //OnAfterRun hook after the main loop's _window.Run exits; subclasses may join background threads and propagate exceptions
    protected virtual void OnAfterRun() { }

    public void Run()
    {
        Init();
        OnInitialized();
        _window.Render += DrawFrame;
        _window.Resize += OnWindowResize;
        OnBeforeRun();
        try
        {
            _window.Run();
        }
        finally
        {
            try
            {
                OnAfterRun();
            }
            finally
            {
                _vkDevice.WaitIdle();
                Cleanup();
            }
        }
    }

    //RequestClose requests the window to close so _window.Run exits the loop, called externally by MinecraftClient.Stop
    public void RequestClose()
    {
        if (_initialized) _window.Close();
    }

    //RunFor starts the main loop and exits automatically after the given number of frames
    public void RunFor(int maxFrames)
    {
        _maxFrames = maxFrames;
        Run();
    }

    private void Init()
    {
        InitWindow();
        InitVulkan();
        _initialized = true;
    }

    private void InitWindow()
    {
        var opts = WindowOptions.DefaultVulkan;
        opts.Size = new Vector2D<int>(SurfaceWidth, SurfaceHeight);
        opts.Title = WindowTitle;
        _window = SilkWindow.Create(opts);
        _window.Initialize();
        if (_window.VkSurface is null)
        {
            throw new NotSupportedException("The platform does not support Vulkan");
        }
    }

    private void InitVulkan()
    {
        _backend = new VulkanBackend(_window);
        CreateSurface();
        _backend.PickPhysicalDevice();
        _vkDevice = new VulkanDevice(_backend, _shaderManager, GpuDebugOptions.None);
        _device = new GpuDevice(_vkDevice, _shaderManager, () => { });
        _vkSurface = (VulkanGpuSurface)_vkDevice.CreateSurface(0);
        _surface = new GpuSurface(_vkSurface);
        _presentMode = GpuSurface.GetSupportedVsyncMode(_vkSurface.SupportedPresentModes(), EnableVsync);
        _vkSurface.Configure(new GpuSurface.Configuration(SurfaceWidth, SurfaceHeight, _presentMode));
        _encoder = _device.CreateCommandEncoder();
        CreateSceneResources();
        OnCreatePipelineResources();
    }

    //CreateSurface creates the platform surface and injects it into the backend
    protected virtual void CreateSurface()
    {
        var surfaceHandle = _window.VkSurface!.Create<AllocationCallbacks>(_backend.Instance.ToHandle(), null);
        _backend.Surface = surfaceHandle.ToSurface();
    }

    //CreateSceneResources builds the offscreen target every app renders into before blitting to the swapchain
    private void CreateSceneResources()
    {
        _swapchainImageFormat = _vkSurface.ImageFormat;
        _swapchainExtent = _vkSurface.Extent;
        _sceneColor = _device.CreateTexture(
            "scene",
            GpuTexture.UsageRenderAttachment | GpuTexture.UsageCopySrc | GpuTexture.UsageCopyDst,
            GpuFormat.Bgra8Unorm,
            (int)_swapchainExtent.Width,
            (int)_swapchainExtent.Height,
            1,
            1);
        _sceneView = _device.CreateTextureView(_sceneColor);
    }

    //OnWindowResize sets the flag when the window size changes; DrawFrame detects it at the end and recreates the swapchain
    private void OnWindowResize(Vector2D<int> obj)
    {
        _framebufferResized = true;
    }

    //RecreateSwapchain rebuilds the swapchain and dependent resources on a window size change or swapchain invalidation
    protected void RecreateSwapchain()
    {
        _vkDevice.WaitIdle();
        var fb = _window.FramebufferSize;
        if (fb.X <= 0 || fb.Y <= 0)
        {
            //Size 0 keeps the flag to avoid being unable to rebuild after the cleanup and looping forever
            _framebufferResized = true;
            return;
        }
        _sceneView.Dispose();
        _sceneColor.Dispose();
        _vkSurface.Configure(new GpuSurface.Configuration(fb.X, fb.Y, _presentMode));
        CreateSceneResources();
        OnSwapchainRecreated();
    }

    private void DrawFrame(double obj)
    {
        try
        {
            _surface.AcquireNextTexture();
        }
        catch (SurfaceException)
        {
            RecreateSwapchain();
            return;
        }
        if (_vkSurface.IsSuboptimal)
        {
            RecreateSwapchain();
            return;
        }

        var vkEncoder = (VulkanCommandEncoder)_encoder.Backend;
        vkEncoder.BeginRecording();
        OnRecordCommandBuffer(_encoder, _sceneView);
        _surface.BlitFromTexture(_encoder, _sceneView);
        vkEncoder.Submit();
        _surface.Present();

        if (_vkSurface.IsSuboptimal || _framebufferResized)
        {
            _framebufferResized = false;
            RecreateSwapchain();
        }

        _framesRendered++;
        if (_maxFrames > 0 && _framesRendered >= _maxFrames)
        {
            _window.Close();
        }
    }

    private void Cleanup()
    {
        if (!_initialized) return;
        _initialized = false;
        _vkDevice.WaitIdle();
        OnCleanupPipelineResources();
        _sceneView?.Dispose();
        _sceneColor?.Dispose();
        _encoder?.Dispose();
        _surface?.Dispose();
        _device?.Dispose();
        _backend?.Dispose();
        //Clears event subscriptions so callbacks during Reset do not access released resources
        _window.Render -= DrawFrame;
        _window.Resize -= OnWindowResize;
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        Cleanup();
        //Silk.NET's Dispose internally calls Reset; after a long run it may still report being inside the render loop and throw
        //Vulkan resources and event subscriptions were already released in Cleanup; this third-party error is ignored
        try { _window.Dispose(); }
        catch (InvalidOperationException) { }
        _disposed = true;
    }
}
