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
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Blaze3d.Platform;

//VulkanAppBase common base class for Vulkan apps
//Encapsulates the common swapchain/imageview/framebuffer/sync/DrawFrame/Cleanup logic
//Subclasses override OnCreatePipelineResources/OnRecordCommandBuffer/GetFramebufferRenderPass to implement custom rendering
//Watches window Resize to trigger swapchain recreation, handling SuboptimalKhr/ErrorOutOfDateKhr
public abstract unsafe class VulkanAppBase : IDisposable
{
    protected const int MaxFramesInFlight = 2;

    protected IWindow _window = null!;
    protected VulkanBackend _context = null!;
    protected VulkanDevice _device = null!;
    protected KhrSwapchain _swapchainExt = null!;
    protected SwapchainKHR _swapchain;
    protected Image[] _swapchainImages = Array.Empty<Image>();
    protected Format _swapchainImageFormat;
    protected Extent2D _swapchainExtent;
    protected ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    //4.3 rework to dynamic rendering no longer needs a framebuffer; the swapchain ImageView is passed directly to BeginRenderPass
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
    //The window-size-changed flag is set by the Resize event; DrawFrame detects it and triggers swapchain recreation
    protected bool _framebufferResized;
    //EnableVsync whether vsync is enabled, true by default; subclasses set it from GameConfig at construction
    //true selects FifoKhr vsync, false selects MailboxKhr without sync
    protected bool EnableVsync = true;

    public int SurfaceWidth { get; }
    public int SurfaceHeight { get; }
    public Vk Api => _device.Api;

    protected VulkanAppBase(int width, int height)
    {
        SurfaceWidth = width;
        SurfaceHeight = height;
    }

    //WindowTitle window title provided by subclasses
    protected abstract string WindowTitle { get; }

    //OnCreatePipelineResources subclasses create pipelines and dependent resources
    //_device/_swapchainImageFormat/_swapchainExtent are ready at this point
    protected abstract void OnCreatePipelineResources();

    //OnRecordCommandBuffer subclasses record the command buffer; colorImageView is the current frame's swapchain ImageView
    //4.3 rework to dynamic rendering passes an ImageView instead of a framebuffer; the subclass passes it to BeginRenderPass
    protected abstract void OnRecordCommandBuffer(VulkanCommandBuffer cmd, ImageView colorImageView);

    //OnSwapchainRecreated after swapchain recreation subclasses can rebuild extent-dependent resources like the pipeline viewport
    //Empty by default; subclasses without extent-dependent resources can ignore it
    protected virtual void OnSwapchainRecreated() { }

    //OnCleanupPipelineResources subclasses clean up their own pipeline resources while the device is not yet destroyed
    protected virtual void OnCleanupPipelineResources() { }

    //OnInitialized fired after the window and Vulkan resources are initialized; subclasses may override to subscribe to input events
    protected virtual void OnInitialized() { }

    //OnBeforeRun hook before the main loop's _window.Run; subclasses may start background tasks like the Tick thread
    //Stage 7 Tick/Render decoupling; the Tick thread starts here while Render stays serial inside _window.Run
    protected virtual void OnBeforeRun() { }

    //OnAfterRun hook after the main loop's _window.Run exits; subclasses may Join background threads and propagate exceptions
    //Called before _device.WaitIdle+Cleanup, when _window is still valid and Dispose resources are still usable
    protected virtual void OnAfterRun() { }

    //Run starts the main loop
    //Stage 7 removed the FrameUpdate event; Tick is now driven by an independent thread started by the subclass's OnBeforeRun
    //_window.Update no longer subscribes to the Update event; only Render drives DrawFrame
    //When OnAfterRun throws, Cleanup must still run; nested finally ensures resource release is not skipped by the exception
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

    //RequestClose requests the window to close so _window.Run exits the loop, called externally by MinecraftClient.Stop
    public void RequestClose()
    {
        if (_initialized) _window.Close();
    }

    //RunFor starts the main loop and exits automatically after the given number of frames
    //maxFrames<=0 means unlimited, using the Run logic
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
        _context = new VulkanBackend(_window);
        CreateSurface();
        _context.PickPhysicalDevice();
        _device = (VulkanDevice)_context.CreateDevice(new GpuDeviceOptions());
        _swapchainExt = _device.SwapchainExtension;
        CreateSwapChain();
        CreateImageViews();
        OnCreatePipelineResources();
        //4.3 rework to dynamic rendering no longer creates a framebuffer
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    //CreateSurface creates the platform surface and injects it into the context
    protected virtual void CreateSurface()
    {
        var surfaceHandle = _window.VkSurface!.Create<AllocationCallbacks>(_context.Instance.ToHandle(), null);
        _context.Surface = surfaceHandle.ToSurface();
    }

    //OnWindowResize sets the flag when the window size changes; DrawFrame detects it at the end and recreates the swapchain
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
                throw new InvalidOperationException($"Swapchain creation failed result={createResult} extent={extent.Width}x{extent.Height}");
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
        //EnableVsync=true uses FifoKhr vsync to align with the monitor refresh rate
        if (EnableVsync) return PresentModeKHR.FifoKhr;
        //EnableVsync=false prefers MailboxKhr without sync, falling back to FifoKhr
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
        //Math.Max(1, ...) prevents a 0x0 extent when FramebufferSize is 0 while minimized
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
                throw new InvalidOperationException($"ImageView {i} creation failed");
            }
            _swapchainImageViews[i] = view;
        }
    }

    //CreateCommandBuffers allocates command buffers by swapchain image count
    //4.3 rework to dynamic rendering no longer depends on the framebuffer count and uses the _swapchainImageViews count
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
                throw new InvalidOperationException($"Sync object {i} creation failed");
            }
        }
    }

    //RecreateSwapchain rebuilds the swapchain and dependent resources on a window size change or swapchain invalidation
    //First waits for the device to idle and cleans up the old swapchain; after recreation it notifies subclasses to rebuild extent-dependent resources
    //Skips recreation when FramebufferSize is 0 while minimized, keeping _framebufferResized for the next Resize retry
    protected void RecreateSwapchain()
    {
        _device.WaitIdle();
        var fb = _window.FramebufferSize;
        if (fb.X <= 0 || fb.Y <= 0)
        {
            //Size 0 keeps the flag to avoid being unable to rebuild after CleanupSwapchain and looping forever
            _framebufferResized = true;
            return;
        }
        CleanupSwapchain();
        CreateSwapChain();
        CreateImageViews();
        OnSwapchainRecreated();
        //4.3 rework to dynamic rendering no longer rebuilds the framebuffer
        //The command buffer count may change and is reallocated
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
            throw new InvalidOperationException("AcquireNextImage failed");
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
                throw new InvalidOperationException("QueueSubmit failed");
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
            throw new InvalidOperationException("QueuePresent failed");
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
        //4.3 rework passes an ImageView instead of a framebuffer; in dynamic rendering mode BeginRenderPass uses it directly
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
        //Clears event subscriptions so callbacks during Reset do not access released resources
        _window.Render -= DrawFrame;
        _window.Resize -= OnWindowResize;
        //_window.Reset moved to Dispose to avoid calling Reset inside the Run render loop call stack
        //Silk.NET does not allow calling Reset inside the render loop and throws You cannot call Reset inside of the render loop
    }

    protected void CleanupSwapchain()
    {
        var vk = _device.Api;
        //4.3 rework to dynamic rendering no longer destroys the framebuffer, only the ImageView
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
        //Silk.NET's Dispose internally calls Reset; after a long run that returns from Run it may still report being inside the render loop and throw InvalidOperationException
        //Vulkan resources and event subscriptions were already released in Cleanup and the window GLFW handle is reclaimed at process exit; this third-party error is ignored
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
