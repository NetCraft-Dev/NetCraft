using System.Numerics;
using Silk.NET.Vulkan;

namespace NetCraft.Gpu.Vulkan;

//VulkanItemAtlasApp 3D 物品图集渲染集成测试 app
//验证 ItemItemAtlas.DrawToSlot 在真 Vulkan 后端能跑通
//每帧轮换调 GetOrUpdate 触发 DrawToSlot 渲染物品到 AtlasTexture
//OnRecordCommandBuffer 只 clear swapchain 不显示 atlas 内容验证 GPU 命令录制链路稳定
public sealed unsafe class VulkanItemAtlasApp : VulkanAppBase
{
    private const int AtlasTextureSize = 512;
    private const int SlotTextureSize = 64;
    private const int ItemCount = 10;

    private VulkanRenderPipeline _clearPipeline = null!;
    private VulkanImage _depthImage = null!;
    private ItemItemAtlas _itemAtlas = null!;
    private readonly object[] _itemIds = new object[ItemCount];

    public VulkanItemAtlasApp() : base(800, 600) { }

    protected override string WindowTitle => "NetCraft.Gpu Vulkan ItemAtlas PoC";

    protected override void OnCreatePipelineResources()
    {
        CreateDepthImage();
        CreateClearPipeline();
        CreateItemAtlas();
    }

    protected override void OnSwapchainRecreated()
    {
        _depthImage.Dispose();
        _clearPipeline.Dispose();
        CreateDepthImage();
        CreateClearPipeline();
    }

    //CreateDepthImage 创建深度附件供 clear pipeline 用
    private void CreateDepthImage()
    {
        var desc = new GpuImageDescription
        {
            Width = (int)_swapchainExtent.Width,
            Height = (int)_swapchainExtent.Height,
            Format = GpuImageFormat.D32Sfloat,
            Usage = GpuImageUsage.DepthAttachment
        };
        _depthImage = (VulkanImage)_device.CreateImage(desc);
        _depthImage.Upload(ReadOnlySpan<byte>.Empty);
    }

    //CreateClearPipeline 创建只 clear swapchain 的 pipeline 用内置 SpirvShaders shader
    private void CreateClearPipeline()
    {
        var description = new RenderPipelineDescription
        {
            DepthTestEnabled = true
        };
        _clearPipeline = new VulkanRenderPipeline(_device.Api, _device.Device, _swapchainImageFormat, _swapchainExtent, description);
    }

    //CreateItemAtlas 创建 ItemItemAtlas 注册 10 个物品
    private void CreateItemAtlas()
    {
        _itemAtlas = new ItemItemAtlas(_device, AtlasTextureSize, SlotTextureSize);
        for (int i = 0; i < ItemCount; i++)
        {
            _itemIds[i] = $"item_{i}";
            _itemAtlas.RegisterItem(_itemIds[i], 1f);
        }
    }

    //OnRecordCommandBuffer 每帧轮换调 GetOrUpdate 触发 DrawToSlot 渲染物品到 AtlasTexture
    //ItemItemAtlas.GetOrUpdate 内部 CreateCommandEncoder+Submit 独立 command buffer 不影响主 cmd
    //主 cmd 只 clear swapchain 到深灰色验证渲染循环稳定
    protected override void OnRecordCommandBuffer(VulkanCommandBuffer cmd, ImageView colorImageView)
    {
        var frame = _framesRendered % ItemCount;
        _itemAtlas.GetOrUpdate(_itemIds[frame], isAnimated: false);

        cmd.BeginRecording();
        cmd.BeginRenderPass(_clearPipeline, colorImageView, _depthImage, 1.0f);
        cmd.EndRenderPass();
        cmd.EndRecording();
    }

    //RenderSlotAndReadback 渲染指定物品到槽位并 readback 槽位像素供集成测试验证
    //返回 SlotTextureSize x SlotTextureSize x 4(RGBA) 字节数组
    //slotX/slotY 由测试指定通常用 (0,0) 因为第一个物品分配到 (0,0) 槽位
    public byte[] RenderSlotAndReadback(int itemIndex, int slotX, int slotY)
    {
        _itemAtlas.GetOrUpdate(_itemIds[itemIndex], isAnimated: false);
        _device.WaitIdle();
        return _itemAtlas.ReadbackSlot(slotX, slotY);
    }

    //RenderAndReadbackFullAtlas 渲染物品并 readback 整张图集像素供诊断测试
    //返回 AtlasTextureSize x AtlasTextureSize x 4(RGBA) 字节数组
    public byte[] RenderAndReadbackFullAtlas(int itemIndex)
    {
        _itemAtlas.GetOrUpdate(_itemIds[itemIndex], isAnimated: false);
        _device.WaitIdle();
        return _itemAtlas.ReadbackFullAtlas();
    }

    //Slot0ReadbackPixels 第一个物品 (0,0) 槽位的 readback 像素供测试断言
    //在 OnBeforeRun 调 RenderSlotAndReadback(0,0,0) 填充测试在 RunFor 返回后读取
    public byte[]? Slot0ReadbackPixels { get; private set; }
    //FullAtlasReadbackPixels 整张图集 readback 像素供诊断渲染写入位置
    public byte[]? FullAtlasReadbackPixels { get; private set; }
    //LastVertexCount 最近一次 DrawToSlot 的顶点数供诊断 RenderToGpu 是否被调用
    public int LastVertexCount { get; private set; }
    //WriteTextureReadbackPixels WriteToTexture 测试的 readback 像素供测试断言
    //OnBeforeRun 用 CreateCommandEncoder+WriteToTexture 写入已知像素 Submit 后 readback 验证
    public byte[]? WriteTextureReadbackPixels { get; private set; }

    //OnBeforeRun override 在窗口主循环前渲染所有注册物品到各自 slot 再 readback
    //每个物品 GetOrUpdate 时由 Allocator 分配独立 slot DrawToSlot 渲染到该 slot 区域
    //readback 整张图集 + slot(0,0) 供测试验证多 slot 渲染和单 slot 内容
    //额外测试 ICommandEncoder.WriteToTexture 命令录制+Submit 写入纹理像素
    protected override void OnBeforeRun()
    {
        base.OnBeforeRun();
        for (int i = 0; i < ItemCount; i++)
            _itemAtlas.GetOrUpdate(_itemIds[i], isAnimated: false);
        _device.WaitIdle();
        LastVertexCount = _itemAtlas.LastFrameVertices.VertexCount;
        FullAtlasReadbackPixels = _itemAtlas.ReadbackFullAtlas();
        Slot0ReadbackPixels = _itemAtlas.ReadbackSlot(0, 0);
        WriteTextureReadbackPixels = TestWriteToTexture();
    }

    //TestWriteToTexture 验证 ICommandEncoder.WriteToTexture 录制命令+Submit 后像素正确写入
    //创建 4x4 SampledImage 用 WriteToTexture 写入全红 RGBA(255,0,0,255) Submit 后 readback 验证
    //验证 staging buffer 生命周期管理 Submit 前不释放 GPU 能正确读到数据
    private byte[] TestWriteToTexture()
    {
        var desc = new GpuImageDescription
        {
            Width = 4,
            Height = 4,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.SampledImage
        };
        var img = (VulkanImage)_device.CreateImage(desc);
        try
        {
            //4x4 全红 RGBA(255,0,0,255)
            var pixels = new byte[4 * 4 * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 255;
            }
            using var encoder = _device.CreateCommandEncoder();
            encoder.WriteToTexture(img, pixels, 0, 0, 4, 4);
            encoder.Submit();
            return img.Readback();
        }
        finally
        {
            img.Dispose();
        }
    }

    //CountNonBlackPixels 统计槽位像素中 RGB 非零像素数量供测试验证实际渲染内容
    //clear 色是 (0,0,0,1) 黑色 渲染后 RGB 非零像素 > 0 说明 DrawToSlot 写入了物品像素
    public static int CountNonBlackPixels(byte[] slotPixels)
    {
        int count = 0;
        for (int i = 0; i < slotPixels.Length; i += 4)
        {
            if (slotPixels[i] > 0 || slotPixels[i + 1] > 0 || slotPixels[i + 2] > 0) count++;
        }
        return count;
    }

    //CountNonBlackPixelsRegion 统计指定矩形区域内 RGB 非零像素数量供诊断渲染写入位置
    //fullPixels 是整张图集像素 width 是图集尺寸 region 是要统计的子矩形
    public static int CountNonBlackPixelsRegion(byte[] fullPixels, int atlasWidth, int regionX, int regionY, int regionW, int regionH)
    {
        int count = 0;
        for (int y = regionY; y < regionY + regionH && y < atlasWidth; y++)
        {
            for (int x = regionX; x < regionX + regionW && x < atlasWidth; x++)
            {
                var idx = (y * atlasWidth + x) * 4;
                if (fullPixels[idx] > 0 || fullPixels[idx + 1] > 0 || fullPixels[idx + 2] > 0) count++;
            }
        }
        return count;
    }

    //CountAlphaPixels 统计指定 alpha 值的像素数 验证 clear 色写入正确
    //clear 色 (0,0,0,1) 渲染前 alpha 应为 255
    public static int CountAlphaPixels(byte[] pixels, byte alpha)
    {
        int count = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == alpha) count++;
        }
        return count;
    }

    //CountAlphaPixelsRegion 统计指定矩形区域内指定 alpha 值的像素数 验证 clear 色写入区域正确
    public static int CountAlphaPixelsRegion(byte[] fullPixels, int atlasWidth, int regionX, int regionY, int regionW, int regionH, byte alpha)
    {
        int count = 0;
        for (int y = regionY; y < regionY + regionH && y < atlasWidth; y++)
        {
            for (int x = regionX; x < regionX + regionW && x < atlasWidth; x++)
            {
                var idx = (y * atlasWidth + x) * 4;
                if (fullPixels[idx + 3] == alpha) count++;
            }
        }
        return count;
    }

    //FindFirstNonBlackPixel 找整张图集第一个 RGB 非零像素位置 诊断渲染写入位置
    //返回 (x, y) 没找到返回 (-1, -1)
    public static (int x, int y) FindFirstNonBlackPixel(byte[] fullPixels, int atlasWidth)
    {
        for (int i = 0; i < fullPixels.Length; i += 4)
        {
            if (fullPixels[i] > 0 || fullPixels[i + 1] > 0 || fullPixels[i + 2] > 0)
            {
                var pixelIdx = i / 4;
                return (pixelIdx % atlasWidth, pixelIdx / atlasWidth);
            }
        }
        return (-1, -1);
    }

    protected override void OnCleanupPipelineResources()
    {
        _itemAtlas.Dispose();
        _depthImage.Dispose();
        _clearPipeline.Dispose();
    }
}
