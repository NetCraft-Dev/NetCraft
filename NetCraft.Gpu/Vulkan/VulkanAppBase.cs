using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using SilkWindow = Silk.NET.Windowing.Window;
using Image = Silk.NET.Vulkan.Image;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace NetCraft.Gpu.Vulkan;

//VulkanAppBase Vulkan 应用公共基类
//封装 swapchain/imageview/framebuffer/sync/DrawFrame/Cleanup 通用逻辑
//子类重写 OnCreatePipelineResources/OnRecordCommandBuffer/GetFramebufferRenderPass 实现自定义渲染
//监听窗口 Resize 触发 swapchain 重建处理 SuboptimalKhr/ErrorOutOfDateKhr
public abstract unsafe class VulkanAppBase : IDisposable
{
    protected const int MaxFramesInFlight = 2;

    protected IWindow _window = null!;
    protected VulkanGpuContext _context = null!;
    protected VulkanGpuDevice _device = null!;
    protected KhrSwapchain _swapchainExt = null!;
    protected SwapchainKHR _swapchain;
    protected Image[] _swapchainImages = Array.Empty<Image>();
    protected Format _swapchainImageFormat;
    protected Extent2D _swapchainExtent;
    protected ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    //4.3 改造 dynamic rendering 不再需要 framebuffer swapchain ImageView 直接传给 BeginRenderPass
    protected VulkanCommandBuffer[] _commandBuffers = Array.Empty<VulkanCommandBuffer>();
    protected Semaphore[] _imageAvailableSemaphores = Array.Empty<Semaphore>();
    protected Semaphore[] _renderFinishedSemaphores = Array.Empty<Semaphore>();
    protected Fence[] _inFlightFences = Array.Empty<Fence>();
    protected Fence[] _imagesInFlight = Array.Empty<Fence>();
    protected uint _currentFrame;
    protected int _framesRendered;
    protected int _maxFrames = -1;
    protected bool _disposed;
    protected bool _initialized;
    //窗口大小变化标志由 Resize 事件设置 DrawFrame 检测后触发 swapchain 重建
    protected bool _framebufferResized;
    //EnableVsync 是否启用垂直同步默认 true 子类构造时按 GameConfig 设置
    //true 选 FifoKhr 垂直同步 false 选 MailboxKhr 无同步
    protected bool EnableVsync = true;

    public int SurfaceWidth { get; }
    public int SurfaceHeight { get; }
    public Vk Api => _device.Api;

    protected VulkanAppBase(int width, int height)
    {
        SurfaceWidth = width;
        SurfaceHeight = height;
    }

    //WindowTitle 窗口标题由子类提供
    protected abstract string WindowTitle { get; }

    //OnCreatePipelineResources 子类创建 pipeline 和依赖资源
    //_device/_swapchainImageFormat/_swapchainExtent 此时已就绪
    protected abstract void OnCreatePipelineResources();

    //OnRecordCommandBuffer 子类录制命令缓冲 colorImageView 是当前帧 swapchain ImageView
    //4.3 改造 dynamic rendering 传 ImageView 替代 framebuffer 子类 BeginRenderPass 时传入
    protected abstract void OnRecordCommandBuffer(VulkanCommandBuffer cmd, ImageView colorImageView);

    //OnSwapchainRecreated swapchain 重建后子类可重建依赖 extent 的资源如 pipeline viewport
    //默认空实现子类不需要 extent 依赖资源时可忽略
    protected virtual void OnSwapchainRecreated() { }

    //OnCleanupPipelineResources 子类清理自己的 pipeline 资源 device 还没销毁
    protected virtual void OnCleanupPipelineResources() { }

    //OnInitialized 窗口和 Vulkan 资源初始化完成后触发子类可重写订阅输入事件
    protected virtual void OnInitialized() { }

    //OnBeforeRun 主循环 _window.Run 前钩子子类可启动 Tick 线程等后台任务
    //阶段7 Tick/Render 解耦 Tick 线程在此启动 Render 仍在 _window.Run 内串行
    protected virtual void OnBeforeRun() { }

    //OnAfterRun 主循环 _window.Run 退出后钩子子类可 Join 后台线程并传播异常
    //_device.WaitIdle+Cleanup 之前调用此时 _window 仍有效 Dispose 资源仍可用
    protected virtual void OnAfterRun() { }

    //Run 启动主循环
    //阶段7 移除 FrameUpdate 事件 Tick 改由子类 OnBeforeRun 启动独立线程驱动
    //_window.Update 不再订阅 Update 事件只 Render 驱动 DrawFrame
    //OnAfterRun 抛异常时 Cleanup 仍需执行用嵌套 finally 保证资源释放不被异常跳过
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
                _device.WaitIdle();
                Cleanup();
            }
        }
    }

    //RequestClose 请求窗口关闭让 _window.Run 退出循环供外部 MinecraftClient.Stop 调用
    public void RequestClose()
    {
        if (_initialized) _window.Close();
    }

    //RunFor 启动主循环跑指定帧数后自动退出
    //maxFrames<=0 表示不限制走 Run 逻辑
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
            throw new NotSupportedException("平台不支持 Vulkan");
        }
    }

    private void InitVulkan()
    {
        _context = new VulkanGpuContext(_window);
        CreateSurface();
        _context.PickPhysicalDevice();
        _device = (VulkanGpuDevice)_context.CreateDevice(new GpuDeviceOptions());
        _swapchainExt = _device.SwapchainExtension;
        CreateSwapChain();
        CreateImageViews();
        OnCreatePipelineResources();
        //4.3 改造 dynamic rendering 不再创建 framebuffer
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    //CreateSurface 创建平台 surface 注入到 context
    protected virtual void CreateSurface()
    {
        var surfaceHandle = _window.VkSurface!.Create<AllocationCallbacks>(_context.Instance.ToHandle(), null);
        _context.Surface = surfaceHandle.ToSurface();
    }

    //OnWindowResize 窗口大小变化时设置标志 DrawFrame 末尾检测并重建 swapchain
    private void OnWindowResize(Vector2D<int> obj)
    {
        _framebufferResized = true;
    }

    private void CreateSwapChain()
    {
        var support = QuerySwapChainSupport(_context.PhysicalDevice);
        var surfaceFormat = ChooseSwapSurfaceFormat(support.Formats);
        var presentMode = ChooseSwapPresentMode(support.PresentModes);
        var extent = ChooseSwapExtent(support.Capabilities);
        uint imageCount = support.Capabilities.MinImageCount + 1;
        if (support.Capabilities.MaxImageCount > 0 && imageCount > support.Capabilities.MaxImageCount)
        {
            imageCount = support.Capabilities.MaxImageCount;
        }
        var indices = _context.FindQueueFamilies(_context.PhysicalDevice);
        uint[] queueFamilyIndices = { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _context.Surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit
        };
        fixed (uint* qfiPtr = queueFamilyIndices)
        {
            if (indices.GraphicsFamily != indices.PresentFamily)
            {
                createInfo.ImageSharingMode = SharingMode.Concurrent;
                createInfo.QueueFamilyIndexCount = 2;
                createInfo.PQueueFamilyIndices = qfiPtr;
            }
            else
            {
                createInfo.ImageSharingMode = SharingMode.Exclusive;
            }
            createInfo.PreTransform = support.Capabilities.CurrentTransform;
            createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = Vk.True;
            createInfo.OldSwapchain = default;
            var createResult = _swapchainExt.CreateSwapchain(_device.Device, &createInfo, null, out _swapchain);
            if (createResult != Result.Success)
            {
                throw new InvalidOperationException($"Swapchain 创建失败 result={createResult} extent={extent.Width}x{extent.Height}");
            }
        }
        _swapchainExt.GetSwapchainImages(_device.Device, _swapchain, &imageCount, null);
        _swapchainImages = new Image[imageCount];
        fixed (Image* images = _swapchainImages)
        {
            _swapchainExt.GetSwapchainImages(_device.Device, _swapchain, &imageCount, images);
        }
        _swapchainImageFormat = surfaceFormat.Format;
        _swapchainExtent = extent;
    }

    private SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
    {
        var details = new SwapChainSupportDetails();
        _context.SurfaceExtension.GetPhysicalDeviceSurfaceCapabilities(device, _context.Surface, out details.Capabilities);
        uint formatCount = 0;
        _context.SurfaceExtension.GetPhysicalDeviceSurfaceFormats(device, _context.Surface, &formatCount, null);
        if (formatCount != 0)
        {
            details.Formats = new SurfaceFormatKHR[formatCount];
            using var mem = GlobalMemory.Allocate((int)formatCount * sizeof(SurfaceFormatKHR));
            var formats = (SurfaceFormatKHR*)Unsafe.AsPointer(ref mem.GetPinnableReference());
            _context.SurfaceExtension.GetPhysicalDeviceSurfaceFormats(device, _context.Surface, &formatCount, formats);
            for (int i = 0; i < formatCount; i++)
            {
                details.Formats[i] = formats[i];
            }
        }
        else
        {
            details.Formats = Array.Empty<SurfaceFormatKHR>();
        }
        uint presentModeCount = 0;
        _context.SurfaceExtension.GetPhysicalDeviceSurfacePresentModes(device, _context.Surface, &presentModeCount, null);
        if (presentModeCount != 0)
        {
            details.PresentModes = new PresentModeKHR[presentModeCount];
            using var mem = GlobalMemory.Allocate((int)presentModeCount * sizeof(PresentModeKHR));
            var modes = (PresentModeKHR*)Unsafe.AsPointer(ref mem.GetPinnableReference());
            _context.SurfaceExtension.GetPhysicalDeviceSurfacePresentModes(device, _context.Surface, &presentModeCount, modes);
            for (int i = 0; i < presentModeCount; i++)
            {
                details.PresentModes[i] = modes[i];
            }
        }
        else
        {
            details.PresentModes = Array.Empty<PresentModeKHR>();
        }
        return details;
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] formats)
    {
        foreach (var format in formats)
        {
            if (format.Format == Format.B8G8R8A8Unorm)
            {
                return format;
            }
        }
        return formats.Length > 0 ? formats[0] : new SurfaceFormatKHR { Format = Format.B8G8R8A8Unorm };
    }

    private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] presentModes)
    {
        //EnableVsync=true 用 FifoKhr 垂直同步对齐显示器刷新率
        if (EnableVsync) return PresentModeKHR.FifoKhr;
        //EnableVsync=false 优先 MailboxKhr 无同步回退 FifoKhr
        foreach (var mode in presentModes)
        {
            if (mode == PresentModeKHR.MailboxKhr) return mode;
        }
        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }
        //Math.Max(1, ...) 防止窗口最小化时 FramebufferSize 为 0 导致 0x0 extent
        var actualExtent = new Extent2D
        {
            Width = (uint)Math.Max(1, _window.FramebufferSize.X),
            Height = (uint)Math.Max(1, _window.FramebufferSize.Y)
        };
        actualExtent.Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, actualExtent.Width));
        actualExtent.Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, actualExtent.Height));
        return actualExtent;
    }

    private void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];
        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainImageFormat,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };
            ImageView view;
            if (_device.Api.CreateImageView(_device.Device, &createInfo, null, &view) != Result.Success)
            {
                throw new InvalidOperationException($"ImageView {i} 创建失败");
            }
            _swapchainImageViews[i] = view;
        }
    }

    //CreateCommandBuffers 按 swapchain image 数量分配命令缓冲
    //4.3 改造 dynamic rendering 不再依赖 framebuffer 数量改用 _swapchainImageViews 数量
    private void CreateCommandBuffers()
    {
        _commandBuffers = new VulkanCommandBuffer[_swapchainImageViews.Length];
        for (int i = 0; i < _commandBuffers.Length; i++)
        {
            _commandBuffers[i] = (VulkanCommandBuffer)_device.CreateCommandBuffer();
        }
        _imagesInFlight = new Fence[_swapchainImageViews.Length];
    }

    private void CreateSyncObjects()
    {
        _imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        _renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
        _inFlightFences = new Fence[MaxFramesInFlight];
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            if (_device.Api.CreateSemaphore(_device.Device, &semaphoreInfo, null, out _imageAvailableSemaphores[i]) != Result.Success ||
                _device.Api.CreateSemaphore(_device.Device, &semaphoreInfo, null, out _renderFinishedSemaphores[i]) != Result.Success ||
                _device.Api.CreateFence(_device.Device, &fenceInfo, null, out _inFlightFences[i]) != Result.Success)
            {
                throw new InvalidOperationException($"同步对象 {i} 创建失败");
            }
        }
    }

    //RecreateSwapchain 窗口大小变化或 swapchain 失效时重建 swapchain 和依赖资源
    //先等设备空闲清理旧 swapchain 重新创建后通知子类重建 extent 依赖资源
    //窗口最小化时 FramebufferSize 为 0 跳过重建保留 _framebufferResized 等下次 Resize 重试
    protected void RecreateSwapchain()
    {
        _device.WaitIdle();
        var fb = _window.FramebufferSize;
        if (fb.X <= 0 || fb.Y <= 0)
        {
            //尺寸 0 保留标志避免 CleanupSwapchain 后无法重建陷入死循环
            _framebufferResized = true;
            return;
        }
        CleanupSwapchain();
        CreateSwapChain();
        CreateImageViews();
        OnSwapchainRecreated();
        //4.3 改造 dynamic rendering 不再重建 framebuffer
        //命令缓冲数量可能变化重新分配
        if (_commandBuffers.Length != _swapchainImageViews.Length)
        {
            foreach (var cmd in _commandBuffers)
            {
                cmd.Dispose();
            }
            _commandBuffers = new VulkanCommandBuffer[_swapchainImageViews.Length];
            for (int i = 0; i < _commandBuffers.Length; i++)
            {
                _commandBuffers[i] = (VulkanCommandBuffer)_device.CreateCommandBuffer();
            }
            _imagesInFlight = new Fence[_swapchainImageViews.Length];
        }
    }

    private void DrawFrame(double obj)
    {
        var vk = _device.Api;
        var fence = _inFlightFences[_currentFrame];
        vk.WaitForFences(_device.Device, 1, in fence, Vk.True, ulong.MaxValue);
        uint imageIndex;
        var result = _swapchainExt.AcquireNextImage
            (_device.Device, _swapchain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, &imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new InvalidOperationException("AcquireNextImage 失败");
        }
        if (_imagesInFlight[imageIndex].Handle != 0)
        {
            vk.WaitForFences(_device.Device, 1, in _imagesInFlight[imageIndex], Vk.True, ulong.MaxValue);
        }
        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];
        RecordCommandBuffer(imageIndex);
        var submitInfo = new SubmitInfo { SType = StructureType.SubmitInfo };
        Semaphore[] waitSemaphores = { _imageAvailableSemaphores[_currentFrame] };
        PipelineStageFlags[] waitStages = { PipelineStageFlags.ColorAttachmentOutputBit };
        submitInfo.WaitSemaphoreCount = 1;
        var signalSemaphore = _renderFinishedSemaphores[_currentFrame];
        fixed (Semaphore* waitSemaphoresPtr = waitSemaphores)
        fixed (PipelineStageFlags* waitStagesPtr = waitStages)
        {
            submitInfo.PWaitSemaphores = waitSemaphoresPtr;
            submitInfo.PWaitDstStageMask = waitStagesPtr;
            submitInfo.CommandBufferCount = 1;
            var bufHandle = _commandBuffers[imageIndex].Handle;
            submitInfo.PCommandBuffers = &bufHandle;
            submitInfo.SignalSemaphoreCount = 1;
            submitInfo.PSignalSemaphores = &signalSemaphore;
            vk.ResetFences(_device.Device, 1, &fence);
            if (vk.QueueSubmit(_device.GraphicsQueue, 1, &submitInfo, _inFlightFences[_currentFrame]) != Result.Success)
            {
                throw new InvalidOperationException("QueueSubmit 失败");
            }
        }
        fixed (SwapchainKHR* swapchain = &_swapchain)
        {
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &signalSemaphore,
                SwapchainCount = 1,
                PSwapchains = swapchain,
                PImageIndices = &imageIndex
            };
            result = _swapchainExt.QueuePresent(_device.PresentQueue, &presentInfo);
        }
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || _framebufferResized)
        {
            _framebufferResized = false;
            RecreateSwapchain();
        }
        else if (result != Result.Success)
        {
            throw new InvalidOperationException("QueuePresent 失败");
        }
        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
        _framesRendered++;
        if (_maxFrames > 0 && _framesRendered >= _maxFrames)
        {
            _window.Close();
        }
    }

    private void RecordCommandBuffer(uint imageIndex)
    {
        var cmd = _commandBuffers[imageIndex];
        cmd.Reset();
        //4.3 改造传 ImageView 替代 framebuffer dynamic rendering 模式下 BeginRenderPass 直接用
        OnRecordCommandBuffer(cmd, _swapchainImageViews[imageIndex]);
    }

    private void Cleanup()
    {
        if (!_initialized) return;
        _initialized = false;
        var vk = _device.Api;
        vk.DeviceWaitIdle(_device.Device);
        CleanupSwapchain();
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            vk.DestroySemaphore(_device.Device, _renderFinishedSemaphores[i], null);
            vk.DestroySemaphore(_device.Device, _imageAvailableSemaphores[i], null);
            vk.DestroyFence(_device.Device, _inFlightFences[i], null);
        }
        foreach (var cmd in _commandBuffers)
        {
            cmd.Dispose();
        }
        OnCleanupPipelineResources();
        _device.Dispose();
        _context.Dispose();
        //清除事件订阅避免 Reset 时回调访问已释放资源
        _window.Render -= DrawFrame;
        _window.Resize -= OnWindowResize;
        //_window.Reset 移到 Dispose 避免在 Run 渲染循环调用栈内调 Reset
        //Silk.NET 不允许在渲染循环内调 Reset 抛 You cannot call Reset inside of the render loop
    }

    protected void CleanupSwapchain()
    {
        var vk = _device.Api;
        //4.3 改造 dynamic rendering 不再销毁 framebuffer 只销毁 ImageView
        foreach (var view in _swapchainImageViews)
        {
            vk.DestroyImageView(_device.Device, view, null);
        }
        if (_swapchain.Handle != 0)
        {
            _swapchainExt.DestroySwapchain(_device.Device, _swapchain, null);
        }
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        Cleanup();
        //Silk.NET 的 Dispose 内部调 Reset 长时间运行后 Run 返回仍可能报告在渲染循环内抛 InvalidOperationException
        //Vulkan 资源和事件订阅已在 Cleanup 释放 窗口 GLFW 句柄由进程退出回收 忽略此第三方库错误
        try { _window.Dispose(); }
        catch (InvalidOperationException) { }
        _disposed = true;
    }

    private struct SwapChainSupportDetails
    {
        public SurfaceCapabilitiesKHR Capabilities;
        public SurfaceFormatKHR[] Formats;
        public PresentModeKHR[] PresentModes;
    }
}
