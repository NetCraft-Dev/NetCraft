using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkFormat = Silk.NET.Vulkan.Format;

namespace NetCraft.Gpu.Vulkan;

//VulkanImage Vulkan 后端 GPU 图像/纹理
//包装 VkImage + VkDeviceMemory + VkImageView
//Upload 通过 staging buffer + CmdCopyBufferToImage + 布局转换到 ShaderReadOnlyOptimal
public sealed unsafe class VulkanImage : GpuImage
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanGpuDevice _gpuDevice;
    private Image _image;
    private DeviceMemory _memory;
    private ImageView _view;
    private bool _disposed;
    //_currentLayout 追踪当前 image layout 用于 UploadRegion 多次按区域写入
    //Upload 首次 Undefined→TransferDst→ShaderReadOnly 后续 UploadRegion ShaderReadOnly→TransferDst→ShaderReadOnly
    private ImageLayout _currentLayout = ImageLayout.Undefined;

    public Image Handle => _image;
    public ImageView View => _view;
    //CurrentLayout 当前 image layout 供 blur 流程 barrier 决策
    public ImageLayout CurrentLayout => _currentLayout;

    internal VulkanImage(Vk vk, Device device, VulkanGpuDevice gpuDevice, GpuImageDescription desc) : base(desc)
    {
        _vk = vk;
        _device = device;
        _gpuDevice = gpuDevice;
        var fmt = ToVkFormat(desc.Format);
        var usage = ToVkUsage(desc.Usage);
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = fmt,
            Extent = new Extent3D { Width = (uint)Width, Height = (uint)Height, Depth = 1 },
            MipLevels = (uint)MipLevels,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };
        if (_vk.CreateImage(_device, &imageInfo, null, out _image) != Result.Success)
            throw new InvalidOperationException("Image 创建失败");
        _vk.GetImageMemoryRequirements(_device, _image, out var memReqs);
        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = gpuDevice.FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };
        if (_vk.AllocateMemory(_device, &allocInfo, null, out _memory) != Result.Success)
            throw new InvalidOperationException("Image memory 分配失败");
        _vk.BindImageMemory(_device, _image, _memory, 0);
        CreateView();
        //ColorAttachment 初始 layout 转换 Undefined->ColorAttachmentOptimal
        //dynamic rendering 期望 ColorAttachmentOptimal 不转换会导致写入数据丢失或 validation warning
        //SampledImage 不含 ColorAttachment 走 Upload 路径转换 DepthAttachment 走 Upload(Empty) 转换
        //ColorAttachment|SampledImage（如 AtlasTexture）首次渲染前需 ColorAttachmentOptimal
        if (desc.Usage.HasFlag(GpuImageUsage.ColorAttachment))
            TransitionInitialColorAttachment();
    }

    //TransitionInitialColorAttachment 把 ColorAttachment 图像从 Undefined 转到 ColorAttachmentOptimal
    //供 AtlasTexture 这类不上传像素的 color attachment 用 dynamic rendering 前必须就位
    private void TransitionInitialColorAttachment()
    {
        _gpuDevice.RunOneTimeCommand(cmd =>
        {
            TransitionLayout(cmd, ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal,
                AccessFlags.None, AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit,
                PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.ColorAttachmentOutputBit,
                ImageAspectFlags.ColorBit);
        });
        _currentLayout = ImageLayout.ColorAttachmentOptimal;
    }

    private void CreateView()
    {
        var fmt = ToVkFormat(Format);
        //DepthAttachment 用 DepthBit 其他用 ColorBit aspect
        var aspectMask = Usage == GpuImageUsage.DepthAttachment
            ? ImageAspectFlags.DepthBit
            : ImageAspectFlags.ColorBit;
        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _image,
            ViewType = ImageViewType.Type2D,
            Format = fmt,
            Components = default,
            SubresourceRange =
            {
                AspectMask = aspectMask,
                BaseMipLevel = 0,
                LevelCount = (uint)MipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };
        if (_vk.CreateImageView(_device, &viewInfo, null, out _view) != Result.Success)
            throw new InvalidOperationException("ImageView 创建失败");
    }

    //Upload 通过 staging buffer 拷贝到 device local image 然后布局转换到 ShaderReadOnlyOptimal
    //DepthAttachment 不上传像素只做 Undefined->DepthStencilAttachmentOptimal 布局转换
    public override void Upload(ReadOnlySpan<byte> pixels)
    {
        if (Usage == GpuImageUsage.DepthAttachment)
        {
            _gpuDevice.RunOneTimeCommand(cmd =>
            {
                TransitionLayout(cmd, ImageLayout.Undefined, ImageLayout.DepthStencilAttachmentOptimal,
                    AccessFlags.None, AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit,
                    PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.EarlyFragmentTestsBit,
                    ImageAspectFlags.DepthBit);
            });
            _currentLayout = ImageLayout.DepthStencilAttachmentOptimal;
            return;
        }
        var staging = (VulkanBuffer)_gpuDevice.CreateBuffer(pixels.Length, GpuBufferUsage.StagingBuffer);
        try
        {
            staging.Upload(pixels.ToArray());

            _gpuDevice.RunOneTimeCommand(cmd =>
            {
                TransitionLayout(cmd, _currentLayout, ImageLayout.TransferDstOptimal,
                    AccessFlags.None, AccessFlags.TransferWriteBit,
                    PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit);

                var region = new BufferImageCopy
                {
                    BufferOffset = 0,
                    BufferRowLength = 0,
                    BufferImageHeight = 0,
                    ImageSubresource =
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    },
                    ImageOffset = default,
                    ImageExtent = new Extent3D { Width = (uint)Width, Height = (uint)Height, Depth = 1 }
                };
                fixed (Image* img = &_image)
                {
                    _vk.CmdCopyBufferToImage(cmd, staging.Handle, _image, ImageLayout.TransferDstOptimal, 1, &region);
                }

                TransitionLayout(cmd, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
                    AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit,
                    PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit);
            });
            _currentLayout = ImageLayout.ShaderReadOnlyOptimal;
        }
        finally
        {
            staging.Dispose();
        }
    }

    //UploadRegion 按区域上传像素到图集子区域对应原版 GlyphBitmap.upload(x,y,texture)
    //用于动态字形烘焙 FontTexture 256×256 图集按需写入新字形像素
    //ShaderReadOnly→TransferDst→写入区域→ShaderReadOnly 完整布局转换循环
    public override void UploadRegion(int x, int y, int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (Usage == GpuImageUsage.DepthAttachment)
            throw new InvalidOperationException("DepthAttachment 不支持 UploadRegion");
        var staging = (VulkanBuffer)_gpuDevice.CreateBuffer(pixels.Length, GpuBufferUsage.StagingBuffer);
        try
        {
            staging.Upload(pixels.ToArray());

            _gpuDevice.RunOneTimeCommand(cmd =>
            {
                TransitionLayout(cmd, _currentLayout, ImageLayout.TransferDstOptimal,
                    AccessFlags.ShaderReadBit, AccessFlags.TransferWriteBit,
                    PipelineStageFlags.FragmentShaderBit, PipelineStageFlags.TransferBit);

                var region = new BufferImageCopy
                {
                    BufferOffset = 0,
                    BufferRowLength = (uint)width,
                    BufferImageHeight = (uint)height,
                    ImageSubresource =
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    },
                    ImageOffset = new Offset3D { X = x, Y = y, Z = 0 },
                    ImageExtent = new Extent3D { Width = (uint)width, Height = (uint)height, Depth = 1 }
                };
                fixed (Image* img = &_image)
                {
                    _vk.CmdCopyBufferToImage(cmd, staging.Handle, _image, ImageLayout.TransferDstOptimal, 1, &region);
                }

                TransitionLayout(cmd, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
                    AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit,
                    PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit);
            });
            _currentLayout = ImageLayout.ShaderReadOnlyOptimal;
        }
        finally
        {
            staging.Dispose();
        }
    }

    //TransitionLayout 布局转换 helper aspect 默认 ColorBit 深度图传 DepthBit
    private void TransitionLayout(CommandBuffer cmd, ImageLayout oldLayout, ImageLayout newLayout,
        AccessFlags srcAccess, AccessFlags dstAccess, PipelineStageFlags srcStage, PipelineStageFlags dstStage,
        ImageAspectFlags aspect = ImageAspectFlags.ColorBit)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _image,
            SubresourceRange =
            {
                AspectMask = aspect,
                BaseMipLevel = 0,
                LevelCount = (uint)MipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            SrcAccessMask = srcAccess,
            DstAccessMask = dstAccess
        };
        _vk.CmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
    }

    //TransitionLayout 把 image 从 _currentLayout 转到 newLayout 录到外部 cmd buffer
    //blur offscreen 链路 Undefined→ColorAttachmentOptimal→ShaderReadOnlyOptimal→ColorAttachmentOptimal
    //access mask 和 stage 由 layout 自动推断调用方不必手填
    public void TransitionLayout(CommandBuffer cmd, ImageLayout newLayout)
    {
        if (_currentLayout == newLayout) return;
        var aspect = Usage.HasFlag(GpuImageUsage.DepthAttachment) ? ImageAspectFlags.DepthBit : ImageAspectFlags.ColorBit;
        var (srcAccess, srcStage) = GetBarrierParams(_currentLayout);
        var (dstAccess, dstStage) = GetBarrierParams(newLayout);
        TransitionLayout(cmd, _currentLayout, newLayout, srcAccess, dstAccess, srcStage, dstStage, aspect);
        _currentLayout = newLayout;
    }

    //GetBarrierParams 按 layout 推断 access mask 和 pipeline stage
    private static (AccessFlags, PipelineStageFlags) GetBarrierParams(ImageLayout layout) => layout switch
    {
        ImageLayout.Undefined => (AccessFlags.None, PipelineStageFlags.TopOfPipeBit),
        ImageLayout.ColorAttachmentOptimal => (AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit, PipelineStageFlags.ColorAttachmentOutputBit),
        ImageLayout.ShaderReadOnlyOptimal => (AccessFlags.ShaderReadBit, PipelineStageFlags.FragmentShaderBit),
        ImageLayout.TransferDstOptimal => (AccessFlags.TransferWriteBit, PipelineStageFlags.TransferBit),
        ImageLayout.TransferSrcOptimal => (AccessFlags.TransferReadBit, PipelineStageFlags.TransferBit),
        ImageLayout.General => (AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit, PipelineStageFlags.AllCommandsBit),
        _ => (AccessFlags.None, PipelineStageFlags.BottomOfPipeBit)
    };

    //Readback 把 GPU 图像像素读回 CPU 供集成测试验证渲染结果
    //流程 当前layout→TransferSrcOptimal→CmdCopyImageToBuffer→map staging buffer→读 CPU→恢复原 layout
    //AtlasTexture 渲染后是 ColorAttachmentOptimal 读取后恢复回 ColorAttachmentOptimal
    //DepthAttachment 不支持 readback PoC 不验证深度图
    public override byte[] Readback()
    {
        if (Usage == GpuImageUsage.DepthAttachment)
            throw new NotSupportedException("DepthAttachment 不支持 Readback");
        var pixelSize = Width * Height * 4;
        var staging = (VulkanBuffer)_gpuDevice.CreateBuffer(pixelSize, GpuBufferUsage.StagingBuffer);
        try
        {
            var originalLayout = _currentLayout;
            _gpuDevice.RunOneTimeCommand(cmd =>
            {
                //当前 layout→TransferSrcOptimal
                TransitionLayout(cmd, _currentLayout, ImageLayout.TransferSrcOptimal,
                    GetBarrierParams(_currentLayout).Item1, AccessFlags.TransferReadBit,
                    GetBarrierParams(_currentLayout).Item2, PipelineStageFlags.TransferBit,
                    ImageAspectFlags.ColorBit);

                //CmdCopyImageToBuffer image→staging buffer
                var region = new BufferImageCopy
                {
                    BufferOffset = 0,
                    BufferRowLength = 0,
                    BufferImageHeight = 0,
                    ImageSubresource =
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    },
                    ImageOffset = default,
                    ImageExtent = new Extent3D { Width = (uint)Width, Height = (uint)Height, Depth = 1 }
                };
                fixed (Image* img = &_image)
                {
                    _vk.CmdCopyImageToBuffer(cmd, _image, ImageLayout.TransferSrcOptimal, staging.Handle, 1, &region);
                }

                //TransferSrcOptimal→原 layout 恢复供后续渲染继续用
                TransitionLayout(cmd, ImageLayout.TransferSrcOptimal, originalLayout,
                    AccessFlags.TransferReadBit, GetBarrierParams(originalLayout).Item1,
                    PipelineStageFlags.TransferBit, GetBarrierParams(originalLayout).Item2,
                    ImageAspectFlags.ColorBit);
            });
            _currentLayout = originalLayout;

            //map staging buffer 读 CPU
            var pixels = new byte[pixelSize];
            staging.Download<byte>(pixels);
            return pixels;
        }
        finally
        {
            staging.Dispose();
        }
    }

    private static VkFormat ToVkFormat(GpuImageFormat fmt) => fmt switch
    {
        GpuImageFormat.R8G8B8A8Unorm => VkFormat.R8G8B8A8Unorm,
        GpuImageFormat.B8G8R8A8Unorm => VkFormat.B8G8R8A8Unorm,
        GpuImageFormat.R8G8B8Unorm => VkFormat.R8G8B8Unorm,
        GpuImageFormat.R8Unorm => VkFormat.R8Unorm,
        GpuImageFormat.D32Sfloat => VkFormat.D32Sfloat,
        _ => throw new ArgumentOutOfRangeException(nameof(fmt))
    };

    private static ImageUsageFlags ToVkUsage(GpuImageUsage usage)
    {
        var flags = ImageUsageFlags.None;
        if (usage.HasFlag(GpuImageUsage.SampledImage))
            flags |= ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit;
        if (usage.HasFlag(GpuImageUsage.ColorAttachment))
            flags |= ImageUsageFlags.ColorAttachmentBit;
        if (usage.HasFlag(GpuImageUsage.DepthAttachment))
            flags |= ImageUsageFlags.DepthStencilAttachmentBit;
        //所有 color image 都允许 TransferSrc 供 Readback 用 CmdCopyImageToBuffer
        if (!usage.HasFlag(GpuImageUsage.DepthAttachment))
            flags |= ImageUsageFlags.TransferSrcBit;
        return flags;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _vk.DestroyImageView(_device, _view, null);
        _vk.DestroyImage(_device, _image, null);
        _vk.FreeMemory(_device, _memory, null);
        _disposed = true;
    }
}
