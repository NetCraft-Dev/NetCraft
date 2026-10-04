namespace NetCraft.Gpu;

//GuiItemAtlas 物品图集抽象基类对标原版 GuiItemAtlas
//管理 atlas color texture + depth texture + DynamicAtlasAllocator
//每帧 tryPrepareFor 检查空间不够 reclaimSpaceFor 腾位
//getOrUpdate 按 SlotState 决定 DrawToSlot（EMPTY 新画/STALE 清后画/READY 直接返回 UV）
//DrawToSlot 留 abstract 子类用 GpuDevice 录制 3D 物品渲染命令对标原版 item.submit
//当前无 3D 物品渲染管线子类留待后续世界渲染补 先定义数据层和模板流程
public abstract class GuiItemAtlas : IDisposable
{
    //MinimumTextureSize 最小图集尺寸对标原版 512
    private const int MinimumTextureSize = 512;

    protected GpuDevice Device { get; }
    //AtlasTexture 公开供外部注册到 GuiResourceManager 拿 textureId 供 DrawImage 采样
    public GpuImage AtlasTexture { get; }
    protected GpuImage AtlasDepth { get; }
    protected DynamicAtlasAllocator<object> Allocator { get; }
    public int TextureSize { get; }
    public int SlotTextureSize { get; }

    protected GuiItemAtlas(GpuDevice device, int textureSize, int slotTextureSize)
    {
        Device = device;
        TextureSize = textureSize;
        SlotTextureSize = slotTextureSize;
        var storageSize = textureSize / slotTextureSize;
        AtlasTexture = device.CreateImage(new GpuImageDescription
        {
            Width = textureSize,
            Height = textureSize,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.ColorAttachment | GpuImageUsage.SampledImage
        });
        AtlasDepth = device.CreateImage(new GpuImageDescription
        {
            Width = textureSize,
            Height = textureSize,
            Format = GpuImageFormat.D32Sfloat,
            Usage = GpuImageUsage.DepthAttachment
        });
        Allocator = new DynamicAtlasAllocator<object>(storageSize, storageSize);
    }

    //ComputeTextureSizeFor 按 slotTextureSize 和所需槽位数计算图集尺寸对标原版
    //preferredSlotCount = required + required/2 留 50% 余量
    //atlasSize = 最小正方形边长 preferredSlotCount 取平方根向上取整
    //maxTextureSize 从 device.Limits.MaxTextureSizeForFormat(R8G8B8A8Unorm) 查询替代硬编码 4096
    public static int ComputeTextureSizeFor(GpuDevice device, int slotTextureSize, int requiredSlotCount)
    {
        var maxTextureSize = device.Limits.MaxTextureSizeForFormat(GpuImageFormat.R8G8B8A8Unorm);
        return ComputeTextureSizeFor(slotTextureSize, requiredSlotCount, maxTextureSize);
    }

    //ComputeTextureSizeFor 显式 maxTextureSize 重载供测试和不依赖 device 的场景调用
    public static int ComputeTextureSizeFor(int slotTextureSize, int requiredSlotCount, int maxTextureSize)
    {
        var preferredSlotCount = requiredSlotCount + requiredSlotCount / 2;
        var atlasSize = SmallestSquareSide(preferredSlotCount);
        return Math.Clamp(SmallestEncompassingPowerOfTwo(atlasSize * slotTextureSize), MinimumTextureSize, maxTextureSize);
    }

    //SmallestSquareSide 最小正方形边长容纳 count 个槽位
    private static int SmallestSquareSide(int count)
    {
        if (count <= 0) return 1;
        var side = (int)Math.Ceiling(Math.Sqrt(count));
        return Math.Max(1, side);
    }

    //SmallestEncompassingPowerOfTwo 最小包围 2 的幂
    private static int SmallestEncompassingPowerOfTwo(int value)
    {
        if (value <= 1) return 1;
        var power = 1;
        while (power < value) power <<= 1;
        return power;
    }

    //EndFrame 帧末释放 discardAfterFrame 槽位对标原版
    public void EndFrame() => Allocator.EndFrame();

    //TryPrepareFor 检查图集能否容纳 items 不够则 reclaimSpaceFor 腾位
    public bool TryPrepareFor(IReadOnlySet<object> items)
    {
        return Allocator.HasSpaceForAll(items) || Allocator.ReclaimSpaceFor(items);
    }

    //GetOrUpdate 按 itemIdentity 查/分配槽位按 SlotState 决定 DrawToSlot
    //返回 SlotView 含 UV 供 BlitRenderState 用 null 表示图集满
    //isAnimated 动画物品 true 帧末释放
    public SlotView? GetOrUpdate(object itemIdentity, bool isAnimated)
    {
        var slot = Allocator.GetOrAllocate(itemIdentity, isAnimated);
        if (slot == null) return null;
        switch (slot.State)
        {
            case DynamicAtlasAllocator<object>.SlotState.Empty:
                DrawToSlot(slot.X, slot.Y, clear: false, itemIdentity);
                break;
            case DynamicAtlasAllocator<object>.SlotState.Stale:
                DrawToSlot(slot.X, slot.Y, clear: true, itemIdentity);
                break;
        }
        //Vulkan 纹理 V=0 对应图像顶部（与 OpenGL 相反）slot(x,y) 像素在 (y*slotSize) 顶部
        //V0=slotY*slotUvSize 顶部 UV V1=(slotY+1)*slotUvSize 底部 UV 配合 BlitRenderState V0 配 Y0 顶部
        var slotUvSize = (float)SlotTextureSize / TextureSize;
        var u0 = slot.X * slotUvSize;
        var v0 = slot.Y * slotUvSize;
        return new SlotView(AtlasTexture, u0, v0, u0 + slotUvSize, v0 + slotUvSize);
    }

    //DrawToSlot 渲染 3D 物品到指定槽位对标原版 drawToSlot
    //slotX/slotY 网格坐标 clear=true 表示 STALE 状态需先清槽位
    //itemIdentity 物品标识由子类映射到具体 ItemStackRenderState 调 3D 渲染
    //子类用 GpuDevice 录制命令设正交投影+scissor+清屏+item.submit
    protected abstract void DrawToSlot(int slotX, int slotY, bool clear, object itemIdentity);

    public void Dispose()
    {
        AtlasTexture.Dispose();
        AtlasDepth.Dispose();
        OnDispose();
        GC.SuppressFinalize(this);
    }

    //OnDispose 子类额外资源释放钩子
    protected virtual void OnDispose() { }
}

//SlotView 图集槽位 UV 视图对标原版 GuiItemAtlas.SlotView
//BlitRenderState 用此 UV 从 atlas texture 采样物品图标
public sealed record SlotView(
    GpuImage Texture,
    float U0,
    float V0,
    float U1,
    float V1);
