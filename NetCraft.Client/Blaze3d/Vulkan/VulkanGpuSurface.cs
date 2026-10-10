using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Image = Silk.NET.Vulkan.Image;
using Semaphore = Silk.NET.Vulkan.Semaphore;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Buffers;

namespace NetCraft.Client.Blaze3d.Vulkan;

//VulkanGpuSurface Vulkan swapchain surface implementing GpuSurfaceBackend
//Owns the swapchain, its image views and the acquire/present synchronization, moved out of the app layer
public sealed unsafe class VulkanGpuSurface : GpuSurfaceBackend
{
    private readonly VulkanBackend _backend;
    private readonly VulkanDevice _device;
    private readonly Vk _vk;
    private readonly long _windowHandle;

    private SwapchainKHR _swapchain;
    private Image[] _images = Array.Empty<Image>();
    private ImageView[] _imageViews = Array.Empty<ImageView>();
    private ImageLayout[] _imageLayouts = Array.Empty<ImageLayout>();
    private Format _imageFormat;
    private Extent2D _extent;
    private uint _imageIndex;
    private bool _hasAcquired;
    private bool _suboptimal;
    private bool _configured;
    private bool _disposed;

    private Semaphore _imageAvailableSemaphore;
    private Fence _acquireFence;

    public VulkanGpuSurface(VulkanBackend backend, VulkanDevice device, long windowHandle)
    {
        _backend = backend;
        _device = device;
        _vk = backend.Api;
        _windowHandle = windowHandle;
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        if (_vk.CreateSemaphore(device.Device, &semaphoreInfo, null, out _imageAvailableSemaphore) != Result.Success)
            throw new InvalidOperationException("Surface semaphore creation failed");
        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        if (_vk.CreateFence(device.Device, &fenceInfo, null, out _acquireFence) != Result.Success)
            throw new InvalidOperationException("Surface fence creation failed");
    }

    public Format ImageFormat => _imageFormat;
    public Extent2D Extent => _extent;
    //CurrentImageView the image view of the acquired swapchain image, used by the app to render directly into it
    public ImageView CurrentImageView => _imageViews.Length > 0 ? _imageViews[_imageIndex] : default;
    public uint CurrentImageIndex => _imageIndex;
    public bool IsConfigured => _configured;

    public void Configure(GpuSurface.Configuration configuration)
    {
        if (_configured)
            CleanupSwapchain();
        CreateSwapchain(configuration.Width, configuration.Height, ToVkPresentMode(configuration.PresentMode));
        _configured = true;
    }

    public bool IsSuboptimal => _suboptimal;

    public IReadOnlyCollection<GpuSurface.PresentMode> SupportedPresentModes()
    {
        var modes = new List<GpuSurface.PresentMode>();
        uint count = 0;
        _backend.SurfaceExtension.GetPhysicalDeviceSurfacePresentModes(_backend.PhysicalDevice, _backend.Surface, &count, null);
        if (count == 0)
            return modes;
        var raw = new PresentModeKHR[count];
        fixed (PresentModeKHR* p = raw)
            _backend.SurfaceExtension.GetPhysicalDeviceSurfacePresentModes(_backend.PhysicalDevice, _backend.Surface, &count, p);
        foreach (var mode in raw)
        {
            switch (mode)
            {
                case PresentModeKHR.ImmediateKhr: modes.Add(GpuSurface.PresentMode.Immediate); break;
                case PresentModeKHR.MailboxKhr: modes.Add(GpuSurface.PresentMode.Mailbox); break;
                case PresentModeKHR.FifoKhr: modes.Add(GpuSurface.PresentMode.Fifo); break;
                case PresentModeKHR.FifoRelaxedKhr: modes.Add(GpuSurface.PresentMode.FifoRelaxed); break;
            }
        }
        return modes;
    }

    public void AcquireNextTexture()
    {
        if (!_configured)
            throw new InvalidOperationException("Surface is not configured");
        var fence = _acquireFence;
        var result = _backend.SurfaceExtension.AcquireNextImage(
            _device.Device, _swapchain, ulong.MaxValue, default, fence, out _imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            _suboptimal = true;
            throw new SurfaceException("Swapchain is out of date");
        }
        if (result != Result.Success && result != Result.SuboptimalKhr)
            throw new SurfaceException($"AcquireNextImage failed with {result}");
        _suboptimal = result == Result.SuboptimalKhr;
        //Waits on a fence instead of a semaphore so the acquired image is usable right away
        _vk.WaitForFences(_device.Device, 1, &fence, Vk.True, ulong.MaxValue);
        _vk.ResetFences(_device.Device, 1, &fence);
        _hasAcquired = true;
    }

    public void BlitFromTexture(CommandEncoderBackend commandEncoder, GpuTextureView textureView)
    {
        if (!_hasAcquired)
            throw new InvalidOperationException("No image has been acquired");
        if (commandEncoder is not VulkanCommandEncoder encoder)
            throw new ArgumentException("commandEncoder must be a VulkanCommandEncoder", nameof(commandEncoder));
        if (textureView.Texture is not VulkanImage source)
            throw new ArgumentException("textureView must wrap a VulkanImage", nameof(textureView));

        var cmd = encoder.Handle;
        var sourceLayout = ImageLayout.TransferSrcOptimal;
        var targetLayout = ImageLayout.TransferDstOptimal;
        var dstImage = _images[_imageIndex];
        TransitionImage(cmd, dstImage, _imageLayouts[_imageIndex], targetLayout,
            ImageLayout.TransferDstOptimal, ImageAspectFlags.ColorBit);
        _imageLayouts[_imageIndex] = targetLayout;
        source.TransitionLayout(cmd, sourceLayout);

        var blit = new ImageBlit
        {
            SrcSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            SrcOffsets = { [0] = new Offset3D { X = 0, Y = 0, Z = 0 }, [1] = new Offset3D { X = (int)_extent.Width, Y = (int)_extent.Height, Z = 1 } },
            DstSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            DstOffsets = { [0] = new Offset3D { X = 0, Y = 0, Z = 0 }, [1] = new Offset3D { X = (int)_extent.Width, Y = (int)_extent.Height, Z = 1 } }
        };
        _vk.CmdBlitImage(cmd, source.Handle, sourceLayout, dstImage, targetLayout, 1, &blit, Filter.Linear);

        TransitionImage(cmd, dstImage, _imageLayouts[_imageIndex], ImageLayout.PresentSrcKhr,
            ImageLayout.PresentSrcKhr, ImageAspectFlags.ColorBit);
        _imageLayouts[_imageIndex] = ImageLayout.PresentSrcKhr;
    }

    public void Present()
    {
        if (!_hasAcquired)
            throw new InvalidOperationException("No image has been acquired");
        var swapchain = _swapchain;
        var imageIndex = _imageIndex;
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex
        };
        var result = _backend.SurfaceExtension.QueuePresent(_device.PresentQueue, &presentInfo);
        _hasAcquired = false;
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            _suboptimal = true;
            return;
        }
        if (result != Result.Success)
            throw new SurfaceException($"QueuePresent failed with {result}");
    }

    private void CreateSwapchain(int width, int height, PresentModeKHR presentMode)
    {
        var capabilities = QueryCapabilities();
        var format = ChooseSurfaceFormat();
        var extent = ChooseExtent(capabilities, width, height);
        uint imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            imageCount = capabilities.MaxImageCount;

        var indices = _backend.FindQueueFamilies(_backend.PhysicalDevice);
        uint[] queueFamilyIndices = { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _backend.Surface,
            MinImageCount = imageCount,
            ImageFormat = format.Format,
            ImageColorSpace = format.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit
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
            createInfo.PreTransform = capabilities.CurrentTransform;
            createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = Vk.True;
            createInfo.OldSwapchain = default;
            var result = _device.SwapchainExtension.CreateSwapchain(_device.Device, &createInfo, null, out _swapchain);
            if (result != Result.Success)
                throw new SurfaceException($"Swapchain creation failed result={result} extent={extent.Width}x{extent.Height}");
        }

        _device.SwapchainExtension.GetSwapchainImages(_device.Device, _swapchain, &imageCount, null);
        _images = new Image[imageCount];
        fixed (Image* images = _images)
            _device.SwapchainExtension.GetSwapchainImages(_device.Device, _swapchain, &imageCount, images);
        _imageFormat = format.Format;
        _extent = extent;
        _imageLayouts = new ImageLayout[_images.Length];
        CreateImageViews();
    }

    private void CreateImageViews()
    {
        _imageViews = new ImageView[_images.Length];
        _imageLayouts = new ImageLayout[_images.Length];
        for (int i = 0; i < _images.Length; i++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _images[i],
                ViewType = ImageViewType.Type2D,
                Format = _imageFormat,
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
            if (_vk.CreateImageView(_device.Device, &createInfo, null, &view) != Result.Success)
                throw new InvalidOperationException($"ImageView {i} creation failed");
            _imageViews[i] = view;
            _imageLayouts[i] = ImageLayout.Undefined;
        }
    }

    private SurfaceCapabilitiesKHR QueryCapabilities()
    {
        _backend.SurfaceExtension.GetPhysicalDeviceSurfaceCapabilities(_backend.PhysicalDevice, _backend.Surface, out var capabilities);
        return capabilities;
    }

    private SurfaceFormatKHR ChooseSurfaceFormat()
    {
        uint count = 0;
        _backend.SurfaceExtension.GetPhysicalDeviceSurfaceFormats(_backend.PhysicalDevice, _backend.Surface, &count, null);
        if (count == 0)
            return new SurfaceFormatKHR { Format = Format.B8G8R8A8Unorm, ColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr };
        var formats = new SurfaceFormatKHR[count];
        fixed (SurfaceFormatKHR* p = formats)
            _backend.SurfaceExtension.GetPhysicalDeviceSurfaceFormats(_backend.PhysicalDevice, _backend.Surface, &count, p);
        foreach (var format in formats)
        {
            if (format.Format == Format.B8G8R8A8Unorm)
                return format;
        }
        return formats[0];
    }

    private Extent2D ChooseExtent(SurfaceCapabilitiesKHR capabilities, int width, int height)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;
        var extent = new Extent2D { Width = (uint)Math.Max(1, width), Height = (uint)Math.Max(1, height) };
        extent.Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, extent.Width));
        extent.Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, extent.Height));
        return extent;
    }

    private void TransitionImage(CommandBuffer cmd, Image image, ImageLayout oldLayout, ImageLayout newLayout, ImageLayout dstLayout, ImageAspectFlags aspect)
    {
        if (oldLayout == newLayout)
            return;
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange =
            {
                AspectMask = aspect,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };
        PipelineStageFlags srcStage;
        PipelineStageFlags dstStage;
        switch (oldLayout)
        {
            case ImageLayout.Undefined:
                srcStage = PipelineStageFlags.TopOfPipeBit;
                dstStage = PipelineStageFlags.TransferBit;
                break;
            case ImageLayout.PresentSrcKhr:
                srcStage = PipelineStageFlags.ColorAttachmentOutputBit;
                dstStage = PipelineStageFlags.TransferBit;
                break;
            default:
                srcStage = PipelineStageFlags.ColorAttachmentOutputBit;
                dstStage = PipelineStageFlags.TransferBit;
                break;
        }
        _vk.CmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
    }

    private static PresentModeKHR ToVkPresentMode(GpuSurface.PresentMode mode) => mode switch
    {
        GpuSurface.PresentMode.Immediate => PresentModeKHR.ImmediateKhr,
        GpuSurface.PresentMode.Mailbox => PresentModeKHR.MailboxKhr,
        GpuSurface.PresentMode.FifoRelaxed => PresentModeKHR.FifoRelaxedKhr,
        _ => PresentModeKHR.FifoKhr
    };

    private void CleanupSwapchain()
    {
        foreach (var view in _imageViews)
            _vk.DestroyImageView(_device.Device, view, null);
        _imageViews = Array.Empty<ImageView>();
        _imageLayouts = Array.Empty<ImageLayout>();
        _images = Array.Empty<Image>();
        if (_swapchain.Handle != 0)
        {
            _device.SwapchainExtension.DestroySwapchain(_device.Device, _swapchain, null);
            _swapchain = default;
        }
        _configured = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        CleanupSwapchain();
        _vk.DestroySemaphore(_device.Device, _imageAvailableSemaphore, null);
        _vk.DestroyFence(_device.Device, _acquireFence, null);
        _disposed = true;
    }
}
