using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using NetCraft.Gpu.Font;
using NetCraft.Gpu.Pipeline;
using NetCraft.Gpu.Sprite;
using Silk.NET.Input;
using Silk.NET.Vulkan;
using VkFormat = Silk.NET.Vulkan.Format;

namespace NetCraft.Gpu.Vulkan;

//VulkanGuiApp GUI 渲染主程序
//继承 VulkanAppBase 阶段5 切换到 GuiRenderContext+GuiRenderer+GuiResourceManager 新路径
//submission 阶段 Window.Render(GuiRenderContext) 提交 RenderState
//render 阶段 GuiRenderer.Draw(IRenderPass, pipelineResolver, descriptorResolver)
//阶段 11.49 订阅 Silk Input 事件缓存到队列 PollInput 派发到 GuiWindow
public sealed unsafe class VulkanGuiApp : VulkanAppBase
{
    //5a 新路径字段替代旧 VulkanGuiRenderer
    private GuiRenderState _renderState = null!;
    private GuiRenderer _guiRenderer = null!;
    private GuiRenderContext _renderContext = null!;
    private GuiResourceManager _resourceManager = null!;
    //_guiPipeline 预编译 RenderPipelines.GUI 用于创建 render pass 取 Extent 做 renderArea
    private CompiledRenderPipeline _guiPipeline = null!;
    private FontAtlas? _fontAtlas;
    //F7 GlyphFont 动态烘焙路径 GlyphStitcher+GlyphFont 替代 FontAtlas
    private GlyphStitcher? _glyphStitcher;
    private GlyphFont? _font;
    //P0 GuiSpriteManager 按 identifier 缓存 GuiSprite 懒加载 PNG+.mcmeta
    //swapchain 重建时 ClearCache 旧 TextureSetup 失效
    private GuiSpriteManager? _spriteManager;
    //日志抽象解耦 NetCraft.Logging 传给 GuiResourceManager
    private readonly IGpuLogger _logger;
    //输入事件队列 Silk 回调线程入队 MinecraftClient.Tick 调 PollInput 出队派发
    private readonly ConcurrentQueue<InputEvent> _inputQueue = new();
    //IInputContext CreateInput 返回的输入上下文 Dispose 时释放
    private IInputContext? _inputContext;
    //_latestSnapshot Tick 写 Interlocked.Exchange 发布 Render 线程 volatile 读
    //阶段7 Tick/Render 解耦 Render 只读快照不访问 _renderState
    private volatile GuiRenderState? _latestSnapshot;
    //_resourceLock 保护 _resourceManager/_renderContext 替换和 submission 期间引用稳定
    private readonly object _resourceLock = new();

    //blur 后处理资源 BeforeBlur 段渲染到 offscreen 水平 blur 到 temp 垂直 blur 到 swapchain
    //AfterBlur 段 LoadOp=Load 保留模糊背景叠加 GUI 控件
    private GpuImage? _blurOffscreen;
    private GpuImage? _blurTemp;
    private GpuSampler? _blurSampler;
    private GpuBuffer? _blurUniformBuffer;
    private GpuDescriptorSet? _blurUniformDescriptorSet;
    private GpuDescriptorSet? _blurOffscreenDescriptorSet;
    private GpuDescriptorSet? _blurTempDescriptorSet;
    private GpuDescriptorLayout? _blurSamplerLayout;
    private GpuDescriptorLayout? _blurUniformLayout;
    private CompiledRenderPipeline? _blurPipeline;
    //BlurRadius 采样步长半径控制模糊强度越大越模糊
    private const float BlurRadius = 2.0f;

    //P15 ItemItemAtlas 3D 物品图集渲染到 AtlasTexture 供 Hotbar 物品图标采样
    //不依赖 swapchain extent 跨 resize 复用 textureId 随 _resourceManager 重建重新注册
    private ItemItemAtlas? _itemAtlas;
    //ItemAtlasTextureId AtlasTexture 注册到 GuiResourceManager 拿到的 textureId 供 GameScreen DrawImage
    private int _itemAtlasTextureId;
    //P15 ItemPipRenderer 超大物品 PIP 离屏渲染器注册到 GuiRenderer 按 ItemPipState 分派
    private ItemPipRenderer? _itemPipRenderer;

    //W7 世界渲染资源 depth image + ViewProj UBO + atlas/lightmap sampler descriptor set
    //IWorldRenderer 由 Game 层注入 VulkanGuiApp 负责 Prepare/Upload/Draw 调度和 GPU 资源管理
    //用接口避免 NetCraft.Gpu 反向引用 NetCraft.Game 循环依赖
    private IWorldRenderer? _levelRenderer;
    private GpuImage? _depthImage;
    private GpuBuffer? _worldViewProjBuffer;
    private GpuDescriptorSet? _worldViewProjSet;
    private GpuDescriptorLayout? _worldViewProjLayout;
    private GpuDescriptorLayout? _worldSamplerLayout;
    private GpuDescriptorSet? _worldAtlasSet;
    private GpuImage? _blockAtlasImage;
    private GpuImage? _lightmapImage;
    private GpuSampler? _worldSampler;
    //_injectedBlockAtlas/_injectedLightmap Game 层注入的真实纹理 null 时 CreateWorldResources 用占位
    //注入资源由本类管理生命周期 OnCleanupPipelineResources 释放 DisposeWorldResources 不释放
    private BlockTextureAtlas? _injectedBlockAtlas;
    private LightTexture? _injectedLightmap;
    //世界渲染是否启用 OnRecordCommandBuffer 检查此标志决定是否调世界 RenderPass
    public bool WorldRenderEnabled => _levelRenderer is not null;

    //Device 暴露 GpuDevice 供 Game 层在 SwapchainRecreated 时创建 GpuBufferPool 等 GPU 资源
    //SwapchainRecreated 触发时 _device 已就绪 构造前访问返回 null-forgiving 实例
    public GpuDevice Device => _device;

    //GuiWindow 外部创建后传入允许测试代码预先布置控件
    public GuiWindow Window { get; }

    //RawKeyDown 原始键码事件由 Game 层订阅识别业务键不依赖 Silk.Input
    public event Action<int>? RawKeyDown;

    //RawMouseDown/RawMouseUp 原始鼠标按键事件带窗口坐标 由 Game 层订阅做游戏交互
    //GuiWindow 只在命中控件时派发 世界交互(挖方块一类)拿不到 必须另开原始通道
    public event Action<GuiMouseButton, int, int>? RawMouseDown;
    public event Action<GuiMouseButton, int, int>? RawMouseUp;

    //RawMouseMove 原始鼠标移动事件带窗口坐标 供屏幕记录悬停位置
    public event Action<int, int>? RawMouseMove;

    //SwapchainRecreated swapchain 重建后触发 ScreenManager.Resized 重布局当前 Screen
    //Game 层订阅触发 Screen.Init 重排控件适应新窗口尺寸
    public event Action? SwapchainRecreated;

    //FrameTick 业务更新事件由 Tick 线程 20tps 触发替代旧 FrameUpdate
    //MinecraftClient 订阅做 PollInput/Window.Update/Screens.Tick/Connection.Tick
    //事件返回后本类立即调 SubmitFrame 发布快照供 Render 线程读
    public event Action<double>? FrameTick;

    //TickThread 20tps 业务更新线程阶段7 Tick/Render 解耦的核心
    //Run 为 IsBackground 不阻塞进程退出 OnAfterRun Join 同步
    //_tickThreadRunning false 让循环退出 RequestClose 触发 _window.Close 让 _window.Run 退出
    private Thread? _tickThread;
    private volatile bool _tickThreadRunning;
    //_tickException Tick 线程未捕获异常 OnAfterRun Join 后由主线程重新抛
    private Exception? _tickException;
    //TickTargetInterval 20tps 对应 50ms 与原版 Minecraft 一致
    private const double TickTargetInterval = 1.0 / 20.0;
    //TickJoinTimeoutMs OnAfterRun 等 Tick 线程退出超时抛 TimeoutException 防卡死
    private const int TickJoinTimeoutMs = 2000;
    //性能验收指标供 GameScreen F3 显示
    //SubmissionCpuMs 最近一帧 submission（Window.Render 构造 RenderState）CPU 耗时
    //RenderCpuMs 最近一帧 render（Prepare+Upload+Draw）CPU 耗时
    //TickRate 最近一秒实际 tps 目标 20
    //DrawCallCount/MeshCount/VertexCount 透传 GuiRenderer 同名字段
    //PipelineHits/PipelineMisses 透传 PipelineCache 同名字段
    public double SubmissionCpuMs { get; private set; }
    public double RenderCpuMs { get; private set; }
    public int TickRate { get; private set; }
    public int DrawCallCount => _guiRenderer?.DrawCallCount ?? 0;
    public int MeshCount => _guiRenderer?.MeshCount ?? 0;
    public int VertexCount => _guiRenderer?.VertexCount ?? 0;
    public int PipelineHits => _device?.PipelineCache?.HitCount ?? 0;
    public int PipelineMisses => _device?.PipelineCache?.MissCount ?? 0;

    //ItemAtlas 3D 物品图集供 GameScreen 调 GetOrUpdate 拿 UV + 触发 DrawToSlot
    //ItemAtlasTextureId AtlasTexture 的 textureId 供 GameScreen 调 DrawImage 采样图集
    //null/0 表示未创建（Headless 模式或 OnCreatePipelineResources 前）
    public ItemItemAtlas? ItemAtlas => _itemAtlas;
    public int ItemAtlasTextureId => _itemAtlasTextureId;
    //ItemPipRenderer 超大物品 PIP 渲染器供 GameLayer/测试 RegisterItem 注入物品模型
    //null 表示未创建（OnCreatePipelineResources 前）
    public ItemPipRenderer? ItemPipRenderer => _itemPipRenderer;

    //SetLevelRenderer 注入世界渲染器 Game 层创建 LevelRenderer 后调此方法注入
    //null 时禁用世界渲染 OnRecordCommandBuffer 跳过世界 RenderPass 只渲染 GUI
    public void SetLevelRenderer(IWorldRenderer? renderer) => _levelRenderer = renderer;

    //SetBlockAtlas 注入真实方块纹理图集 Game 层用 BlockTextureCollector+BlockTextureAtlas.Build 构建后注入
    //null 恢复占位纹理 CreateWorldResources 检查此字段决定用真实还是占位
    //注入后所有权移交本类 OnCleanupPipelineResources 释放 swapchain 重建时不释放
    public void SetBlockAtlas(BlockTextureAtlas? atlas) => _injectedBlockAtlas = atlas;

    //SetLightmap 注入真实光照贴图 Game 层创建 LightTexture 后注入
    //null 恢复占位纹理注入后所有权移交本类 OnCleanupPipelineResources 释放
    public void SetLightmap(LightTexture? lightmap) => _injectedLightmap = lightmap;

    //LevelRenderer 性能指标供 GameScreen F3 显示世界渲染统计
    public int WorldSectionCount => _levelRenderer?.SectionCount ?? 0;
    public int WorldVisibleSectionCount => _levelRenderer?.VisibleSectionCount ?? 0;
    public int WorldVertexCount => _levelRenderer?.TotalVertexCount ?? 0;
    public int WorldDrawCallCount => _levelRenderer?.DrawCallCount ?? 0;

    //RegisterTexture 按 path 加载 PNG 注册为纹理返回 textureId 供 GuiImage 引用
    //委托给当前 _resourceManager swapchain 重建后 _resourceManager 已换新 textureId 可能变化
    //Screen.Init 在 Resized 重布局时重新调此方法取新 id 赋给 GuiImage
    //_resourceManager 在 OnCreatePipelineResources 之前为 null 此时返回 0 走占位
    //OnCreatePipelineResources 末尾触发 SwapchainRecreated 让 Screen 重 Init 取到真实 textureId
    //阶段7 持 _resourceLock 保护 _resourceManager 字段和内部字典与 OnSwapchainRecreated 重建跨线程竞争
    public int RegisterTexture(string path)
    {
        lock (_resourceLock)
        {
            return _resourceManager?.RegisterTexture(path) ?? 0;
        }
    }

    public VulkanGuiApp() : this(true, 800, 600)
    {
    }

    public VulkanGuiApp(int width, int height) : this(true, width, height)
    {
    }

    //enableVsync 由调用方从 GameConfig.EnableVsync 传入避免 Gpu 层依赖 GameConfig 类型
    public VulkanGuiApp(bool enableVsync, int width, int height, IGpuLogger? logger = null) : base(width, height)
    {
        EnableVsync = enableVsync;
        _logger = logger ?? new ConsoleGpuLogger();
        Window = new GuiWindow(width, height);
    }

    protected override string WindowTitle => "NetCraft.Gpu VulkanGuiApp";

    //OnCreatePipelineResources 创建新路径资源
    //_swapchainExtent/_swapchainImageFormat 此时已就绪
    //先 UpdateSurfaceSize 重算 guiScale 再创建 GuiResourceManager/GuiRenderState/GuiRenderer/GuiRenderContext
    //预编译 GUI 系列 pipeline 避免运行时编译卡顿
    //末尾触发 SwapchainRecreated 通知 Screen 重 Init 此时 _resourceManager 已就绪可加载纹理
    //F7 初始化 Font 动态烘焙路径 GlyphStitcher+FontSet+Font 替代 FontAtlas
    protected override void OnCreatePipelineResources()
    {
        var w = (int)_swapchainExtent.Width;
        var h = (int)_swapchainExtent.Height;
        Window.UpdateSurfaceSize(w, h);
        _fontAtlas = FontAtlas.FromSystemFont();
        _resourceManager = new GuiResourceManager(_device, w, h, _fontAtlas, _logger);
        _renderState = new GuiRenderState();
        _guiRenderer = new GuiRenderer();
        //F7 创建 Font 动态烘焙路径 GlyphStitcher 接入 GuiResourceManager 注册图集
        _glyphStitcher = new GlyphStitcher(_device, _resourceManager);
        _font = CreateFontFromAssets();
        //P0 创建 GuiSpriteManager 注入 GuiRenderContext 让 DrawSprite 按 .mcmeta 分派
        _spriteManager = new GuiSpriteManager(Path.Combine(AppContext.BaseDirectory, "assets"), _resourceManager);
        _renderContext = new GuiRenderContext(_renderState, w, h, Window.GuiScale,
            _font, _resourceManager.FontAtlas, _resourceManager.FontTexture,
            id => _resourceManager.ResolveTexture(id),
            _spriteManager);
        PrecompileGuiPipelines();
        CreateBlurResources(w, h);
        CreateItemAtlasResources();
        //SwapchainRecreated 先触发让 Game 层注入真实纹理+LevelRenderer 再创建世界资源
        //首次世界资源创建即用注入的真实图集/lightmap 而非占位
        SwapchainRecreated?.Invoke();
        CreateWorldResources(w, h);
    }

    //CreateItemAtlasResources 创建 ItemItemAtlas+ItemPipRenderer 注册到 GuiRenderer+ResourceManager
    //_itemAtlas/_itemPipRenderer 不依赖 swapchain extent 跨 resize 复用
    //_itemAtlasTextureId 依赖 _resourceManager 随重建重新 RegisterImage
    //首次创建时 _itemAtlas 为 null 才 new ItemItemAtlas 后续 resize 跳过创建只重新注册 textureId
    private void CreateItemAtlasResources()
    {
        if (_itemAtlas is null)
        {
            //512x512 图集 64x64 槽位 8x8=64 槽位对标 VulkanItemAtlasApp 测试配置
            _itemAtlas = new ItemItemAtlas(_device, 512, 64);
            _itemPipRenderer = new ItemPipRenderer(_device);
            _guiRenderer.RegisterPipRenderer(_itemPipRenderer);
        }
        _itemAtlasTextureId = _resourceManager.RegisterImage(_itemAtlas.AtlasTexture);
    }

    //CreateWorldResources 创建世界渲染 GPU 资源 depth image + ViewProj UBO + atlas/lightmap sampler
    //blockAtlas 首版用 1x1 白色占位纹理 W9 接入真实方块图集 lightmap 用 16x16 全亮占位
    //swapchain 重建时旧资源 Dispose 后重建匹配新 extent
    private void CreateWorldResources(int w, int h)
    {
        //预编译 3 个 terrain pipeline 避免 OnRecordCommandBuffer 首次编译卡顿
        var cache = _device.PipelineCache;
        cache.Precompile(WorldRenderPipelines.SOLID_TERRAIN);
        cache.Precompile(WorldRenderPipelines.CUTOUT_TERRAIN);
        cache.Precompile(WorldRenderPipelines.TRANSLUCENT_TERRAIN);

        //depth image D32Sfloat DepthAttachment 与 swapchain 同 extent
        _depthImage = _device.CreateImage(new GpuImageDescription
        {
            Width = w,
            Height = h,
            Format = GpuImageFormat.D32Sfloat,
            Usage = GpuImageUsage.DepthAttachment
        });
        //depth image 首次 layout 转换 Undefined→DepthStencilAttachmentOptimal
        //Upload 对 DepthAttachment 不读像素只做 barrier dynamic rendering 期望 layout 就位
        _depthImage.Upload(ReadOnlySpan<byte>.Empty);

        //set 0 MATRICES_PROJECTION 1 uniform Mat4 vertex 可见
        var viewProjLayoutDesc = new GpuDescriptorLayoutDescription();
        viewProjLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Vertex
        });
        _worldViewProjLayout = _device.CreateDescriptorLayout(viewProjLayoutDesc);
        _worldViewProjBuffer = _device.CreateBuffer(64, GpuBufferUsage.UniformBuffer);
        _worldViewProjSet = _device.AllocateDescriptorSet(_worldViewProjLayout);
        _worldViewProjSet.WriteBuffer(0, _worldViewProjBuffer, 0, -1);

        //set 1 SAMPLER0_SAMPLER1 atlas + lightmap 双 sampler fragment 可见
        var samplerLayoutDesc = new GpuDescriptorLayoutDescription();
        samplerLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        samplerLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 1,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _worldSamplerLayout = _device.CreateDescriptorLayout(samplerLayoutDesc);

        //占位纹理共用 linear sampler 注入纹理自带 nearest sampler
        _worldSampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = true,
            RepeatAddress = false
        });

        //blockAtlas 已注入用真实图集否则创建占位 1x1 白色 RGBA8
        GpuImage blockAtlasImage;
        GpuSampler blockAtlasSampler;
        if (_injectedBlockAtlas is not null && _injectedBlockAtlas.AtlasImage is not null && _injectedBlockAtlas.Sampler is not null)
        {
            blockAtlasImage = _injectedBlockAtlas.AtlasImage;
            blockAtlasSampler = _injectedBlockAtlas.Sampler;
        }
        else
        {
            _blockAtlasImage = _device.CreateImage(new GpuImageDescription
            {
                Width = 1,
                Height = 1,
                Format = GpuImageFormat.R8G8B8A8Unorm,
                Usage = GpuImageUsage.SampledImage
            });
            _blockAtlasImage.Upload(new byte[] { 255, 255, 255, 255 });
            blockAtlasImage = _blockAtlasImage;
            blockAtlasSampler = _worldSampler;
        }

        //lightmap 已注入用真实 LightTexture 否则创建占位 16x16 全亮 RGBA8
        GpuImage lightmapImage;
        GpuSampler lightmapSampler;
        if (_injectedLightmap is not null && _injectedLightmap.Texture is not null && _injectedLightmap.Sampler is not null)
        {
            lightmapImage = _injectedLightmap.Texture;
            lightmapSampler = _injectedLightmap.Sampler;
        }
        else
        {
            _lightmapImage = _device.CreateImage(new GpuImageDescription
            {
                Width = 16,
                Height = 16,
                Format = GpuImageFormat.R8G8B8A8Unorm,
                Usage = GpuImageUsage.SampledImage
            });
            var lightmapPixels = new byte[16 * 16 * 4];
            for (var i = 0; i < lightmapPixels.Length; i += 4)
            {
                lightmapPixels[i] = 255;
                lightmapPixels[i + 1] = 255;
                lightmapPixels[i + 2] = 255;
                lightmapPixels[i + 3] = 255;
            }
            _lightmapImage.Upload(lightmapPixels);
            lightmapImage = _lightmapImage;
            lightmapSampler = _worldSampler;
        }

        _worldAtlasSet = _device.AllocateDescriptorSet(_worldSamplerLayout);
        _worldAtlasSet.WriteImage(0, blockAtlasImage, blockAtlasSampler);
        _worldAtlasSet.WriteImage(1, lightmapImage, lightmapSampler);
    }

    //DisposeWorldResources 释放世界渲染资源 swapchain 重建和 Cleanup 时调
    private void DisposeWorldResources()
    {
        _depthImage?.Dispose();
        _worldViewProjBuffer?.Dispose();
        _worldViewProjSet?.Dispose();
        _worldViewProjLayout?.Dispose();
        _worldSamplerLayout?.Dispose();
        _worldAtlasSet?.Dispose();
        _blockAtlasImage?.Dispose();
        _lightmapImage?.Dispose();
        _worldSampler?.Dispose();
        _depthImage = null;
        _worldViewProjBuffer = null;
        _worldViewProjSet = null;
        _worldViewProjLayout = null;
        _worldSamplerLayout = null;
        _worldAtlasSet = null;
        _blockAtlasImage = null;
        _lightmapImage = null;
        _worldSampler = null;
    }

    //CreateFontFromAssets 从 assets/minecraft/font/<id>.json 加载字体配置构造 GlyphFont
    //F7 默认加载 minecraft:alt 字体（ASCII+Sga）ascent=7 lineHeight=9 对标原版 BitmapProvider ascent
    //assets 路径不存在时 fallback 到 FontAtlas 系统字体 _font=null
    private GlyphFont? CreateFontFromAssets()
    {
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        if (!Directory.Exists(assetsRoot))
        {
            _logger.Warning($"assets 目录不存在 {assetsRoot} GlyphFont 初始化 fallback 到 FontAtlas");
            return null;
        }
        var accessor = new AssetsFontResourceAccessor(assetsRoot);
        //默认加载 minecraft:alt 字体对标原版默认字体
        var fontId = "minecraft:alt";
        var (ns, path) = fontId.Split(':', 2) switch
        {
            var parts when parts.Length > 1 => (parts[0], parts[1]),
            _ => ("minecraft", fontId)
        };
        var fontPath = $"{ns}:font/{path}.json";
        var stream = accessor.OpenResource(fontPath);
        if (stream == null)
        {
            _logger.Warning($"字体配置不存在 {fontPath} GlyphFont 初始化 fallback 到 FontAtlas");
            return null;
        }
        List<IGlyphProvider.Conditional> providers;
        using (stream)
        {
            providers = FontProviderDefinitionLoader.Load(stream, accessor);
        }
        var fontSet = new FontSet();
        fontSet.Reload(providers, new HashSet<FontOption>());
        //alt.json 的 BitmapProvider ascent=7 lineHeight=8 对标原版 ASCII 位图字体
        return new GlyphFont(fontSet, _glyphStitcher!, ascent: 7, lineHeight: 9);
    }

    //PrecompileGuiPipelines 预编译 GUI 系列 pipeline 到 PipelineCache 命中缓存零编译
    //声明式 pipeline 默认 extent 800x600 测试场景匹配 swapchain
    //生产环境非 800x600 窗口需启用动态 viewport 后续优化
    private void PrecompileGuiPipelines()
    {
        var cache = _device.PipelineCache;
        _guiPipeline = cache.Precompile(RenderPipelines.GUI);
        cache.Precompile(RenderPipelines.GUI_INVERT);
        cache.Precompile(RenderPipelines.GUI_TEXT);
        cache.Precompile(RenderPipelines.GUI_TEXTURED);
        //F7 预编译字体 pipeline 避免 Bake 时首次编译卡顿
        cache.Precompile(RenderPipelines.GUI_TEXT_GRAYSCALE);
        cache.Precompile(RenderPipelines.GUI_TEXT_SEE_THROUGH);
        cache.Precompile(RenderPipelines.GUI_TEXT_POLYGON_OFFSET);
        cache.Precompile(RenderPipelines.GUI_TEXT_GRAYSCALE_SEE_THROUGH);
        cache.Precompile(RenderPipelines.GUI_TEXT_GRAYSCALE_POLYGON_OFFSET);
    }

    //CreateBlurResources 创建 blur 后处理资源 offscreen+temp 双纹理+uniform+descriptor
    //BeforeBlur 渲染到 offscreen 水平 blur offscreen→temp 垂直 blur temp→swapchain
    //swapchain 重建时旧资源 Dispose 后重建匹配新 extent
    private void CreateBlurResources(int w, int h)
    {
        var imageDesc = new GpuImageDescription
        {
            Width = w,
            Height = h,
            Format = GpuImageFormat.R8G8B8A8Unorm,
            Usage = GpuImageUsage.ColorAttachment | GpuImageUsage.SampledImage
        };
        _blurOffscreen = _device.CreateImage(imageDesc);
        _blurTemp = _device.CreateImage(imageDesc);
        _blurSampler = _device.CreateSampler(new GpuSamplerDescription
        {
            LinearFilter = true,
            RepeatAddress = false
        });
        //IN_SAMPLER layout 1 combined image sampler binding 0 fragment 可见
        var samplerLayoutDesc = new GpuDescriptorLayoutDescription();
        samplerLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _blurSamplerLayout = _device.CreateDescriptorLayout(samplerLayoutDesc);
        _blurOffscreenDescriptorSet = _device.AllocateDescriptorSet(_blurSamplerLayout);
        _blurOffscreenDescriptorSet.WriteImage(0, _blurOffscreen, _blurSampler);
        _blurTempDescriptorSet = _device.AllocateDescriptorSet(_blurSamplerLayout);
        _blurTempDescriptorSet.WriteImage(0, _blurTemp, _blurSampler);
        //BLUR_CONFIG layout 1 uniform buffer binding 0 fragment 可见
        var uniformLayoutDesc = new GpuDescriptorLayoutDescription();
        uniformLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _blurUniformLayout = _device.CreateDescriptorLayout(uniformLayoutDesc);
        _blurUniformBuffer = _device.CreateBuffer(16, GpuBufferUsage.UniformBuffer);
        _blurUniformDescriptorSet = _device.AllocateDescriptorSet(_blurUniformLayout);
        _blurUniformDescriptorSet.WriteBuffer(0, _blurUniformBuffer, 0, -1);
        _blurPipeline = _device.PipelineCache.Precompile(RenderPipelines.BLUR);
    }

    //DisposeBlurResources 释放 blur 资源 swapchain 重建和 Cleanup 时调
    private void DisposeBlurResources()
    {
        _blurOffscreen?.Dispose();
        _blurTemp?.Dispose();
        _blurSampler?.Dispose();
        _blurUniformBuffer?.Dispose();
        _blurUniformDescriptorSet?.Dispose();
        _blurOffscreenDescriptorSet?.Dispose();
        _blurTempDescriptorSet?.Dispose();
        _blurSamplerLayout?.Dispose();
        _blurUniformLayout?.Dispose();
        _blurPipeline?.Dispose();
        _blurOffscreen = null;
        _blurTemp = null;
        _blurSampler = null;
        _blurUniformBuffer = null;
        _blurUniformDescriptorSet = null;
        _blurOffscreenDescriptorSet = null;
        _blurTempDescriptorSet = null;
        _blurSamplerLayout = null;
        _blurUniformLayout = null;
        _blurPipeline = null;
    }

    //UpdateBlurUniform 上传 BlurConfig vec4 Data xy=BlurDir z=Radius w=0
    //水平 pass BlurDir=(1,0) 垂直 pass BlurDir=(0,1) 每帧 2 次更新
    private void UpdateBlurUniform(float dirX, float dirY)
    {
        _blurUniformBuffer!.Upload<float>(new float[] { dirX, dirY, BlurRadius, 0f });
    }

    //OnInitialized 窗口初始化完成后调 CreateInput 创建输入上下文订阅鼠标键盘事件入队
    //KeyChar 文本输入暂未接入待 TextBox 等控件需要时补
    protected override void OnInitialized()
    {
        _inputContext = _window.CreateInput();
        foreach (var mouse in _inputContext.Mice)
        {
            mouse.MouseDown += (_, b) => EnqueueMouse(b, true);
            mouse.MouseUp += (_, b) => EnqueueMouse(b, false);
            mouse.MouseMove += (_, p) => _inputQueue.Enqueue(new MouseMoveInput((int)p.X, (int)p.Y));
        }
        foreach (var keyboard in _inputContext.Keyboards)
        {
            keyboard.KeyDown += (_, k, _) => _inputQueue.Enqueue(new KeyInput((int)k, true));
            keyboard.KeyUp += (_, k, _) => _inputQueue.Enqueue(new KeyInput((int)k, false));
            keyboard.KeyChar += (_, c) => _inputQueue.Enqueue(new CharInput(c));
        }
    }

    //EnqueueMouse 把 Silk MouseButton 转 GuiMouseButton 后连同当前鼠标坐标入队
    private void EnqueueMouse(MouseButton b, bool down)
    {
        var btn = b switch
        {
            MouseButton.Left => GuiMouseButton.Left,
            MouseButton.Right => GuiMouseButton.Right,
            MouseButton.Middle => GuiMouseButton.Middle,
            _ => GuiMouseButton.None
        };
        if (btn == GuiMouseButton.None) return;
        var mouse = _inputContext?.Mice.FirstOrDefault();
        var pos = mouse?.Position ?? default;
        var x = (int)pos.X;
        var y = (int)pos.Y;
        _inputQueue.Enqueue(down
            ? new MouseDownInput(btn, x, y)
            : new MouseUpInput(btn, x, y));
    }

    //PollInput 把输入队列派发到 GuiWindow 由 MinecraftClient.Tick 每帧调用
    public void PollInput()
    {
        while (_inputQueue.TryDequeue(out var ev))
        {
            switch (ev)
            {
                case MouseDownInput m:
                    //原始事件先发给 Game 层再走控件命中 两边都能收到
                    RawMouseDown?.Invoke(m.Button, m.X, m.Y);
                    Window.ProcessMouseDown(m.Button, m.X, m.Y);
                    break;
                case MouseUpInput m:
                    RawMouseUp?.Invoke(m.Button, m.X, m.Y);
                    Window.ProcessMouseUp(m.Button, m.X, m.Y);
                    break;
                case MouseMoveInput m:
                    RawMouseMove?.Invoke(m.X, m.Y);
                    Window.ProcessMouseMove(m.X, m.Y);
                    break;
                case KeyInput k:
                    if (k.Down)
                    {
                        //RawKeyDown 暴露原始键码由 Game 层 ScreenManager 识别业务键
                        //不消费原始事件继续派发到焦点控件兼容无业务订阅场景
                        RawKeyDown?.Invoke(k.Key);
                        Window.ProcessKeyDown(k.Key);
                    }
                    else Window.ProcessKeyUp(k.Key);
                    break;
                case CharInput c:
                    Window.ProcessKeyChar(c.Char);
                    break;
            }
        }
    }

    //OnSwapchainRecreated swapchain extent 变了重建依赖 extent 的资源
    //_resourceManager 重建 Projection 重算 + 字体/图像纹理 DescriptorSet 重建
    //_renderContext 重建尺寸变了 _guiRenderer 跨帧复用 buffer 不重建
    //PipelineCache 清空重建 pipeline extent 匹配新 swapchain
    //末尾触发 SwapchainRecreated 事件让 ScreenManager.Resized 设 _layoutDirty 由 Tick 线程消费
    //阶段7 Render 线程触发持 _resourceLock 与 Tick 线程 SubmitFrame/RegisterTexture 互斥
    //锁内调 Window.UpdateSurfaceSize 改 GuiScale 后立即重建 _renderContext 保证引用一致
    //锁外清空 _latestSnapshot 防 Render 线程读到旧 TextureSetup 悬挂引用 resize 后 1 帧 clear-only 可接受
    protected override void OnSwapchainRecreated()
    {
        GuiResourceManager? oldResourceManager;
        var w = (int)_swapchainExtent.Width;
        var h = (int)_swapchainExtent.Height;
        lock (_resourceLock)
        {
            oldResourceManager = _resourceManager;
            Window.UpdateSurfaceSize(w, h);
            _resourceManager = new GuiResourceManager(_device, w, h, _fontAtlas, _logger);
            //F7 重建 GlyphStitcher+Font 新 GuiResourceManager 需要重新注册图集
            _glyphStitcher?.Dispose();
            _glyphStitcher = new GlyphStitcher(_device, _resourceManager);
            _font = CreateFontFromAssets();
            //P0 重建 GuiSpriteManager 旧 textureId 失效清空缓存让 DrawSprite 重新加载
            _spriteManager = new GuiSpriteManager(Path.Combine(AppContext.BaseDirectory, "assets"), _resourceManager);
            _renderContext = new GuiRenderContext(_renderState, w, h, Window.GuiScale,
                _font, _resourceManager.FontAtlas, _resourceManager.FontTexture,
                id => _resourceManager.ResolveTexture(id),
                _spriteManager);
            _device.PipelineCache.Clear();
            PrecompileGuiPipelines();
            //blur 资源 extent 跟 swapchain 旧资源 Dispose 后重建匹配新 extent
            DisposeBlurResources();
            CreateBlurResources(w, h);
            //ItemAtlas 不重建只重新注册 AtlasTexture 拿新 textureId 旧 _resourceManager 已 Dispose
            CreateItemAtlasResources();
            //世界渲染资源 depth image 需匹配新 extent 重建 UBO/sampler 跨 resize 复用但 layout 依赖 _device 不变
            DisposeWorldResources();
            CreateWorldResources(w, h);
        }
        oldResourceManager?.Dispose();
        Interlocked.Exchange(ref _latestSnapshot, null);
        SwapchainRecreated?.Invoke();
    }

    //SubmitFrame submission 阶段构造 RenderState 并深拷贝快照发布到 _latestSnapshot
    //Tick 线程调持 _resourceLock 读 _renderContext/_renderState 引用稳定不被 OnSwapchainRecreated 替换
    //锁内 Window.Render 调 _textureResolver 读字典与 RegisterTexture/OnSwapchainRecreated 互斥
    //P15 PreparePip 移到 OnRecordCommandBuffer 调用 vkQueueSubmit 必须在 Render 线程串行
    //Tick 线程 SubmitFrame 和 Render 线程 DrawFrame 都调 vkQueueSubmit 到同一 queue 需外部同步
    //阶段7 Tick/Render 解耦 Render 线程只读快照不调本方法
    public void SubmitFrame()
    {
        GuiRenderContext? ctx;
        GuiRenderState? rs;
        var sw = Stopwatch.StartNew();
        lock (_resourceLock)
        {
            ctx = _renderContext;
            rs = _renderState;
            if (ctx is null || rs is null) return;
            rs.Reset();
            ctx.BeginFrame();
            Window.Render(ctx);
        }
        var snapshot = rs.Snapshot();
        Interlocked.Exchange(ref _latestSnapshot, snapshot);
        SubmissionCpuMs = sw.Elapsed.TotalMilliseconds;
    }

    //OnBeforeRun 主线程 _window.Run 前启动 Tick 线程进入 20tps 循环
    //Run() 调用本方法后才进入 _window.Run 阻塞此时 _resourceManager 等已就绪
    protected override void OnBeforeRun()
    {
        _tickThreadRunning = true;
        _tickThread = new Thread(TickThreadLoop)
        {
            IsBackground = true,
            Name = "NetCraft.TickThread"
        };
        _tickThread.Start();
    }

    //OnAfterRun 主线程 _window.Run 退出后 Join Tick 线程
    //_window.Close 已让 _window.Run 退出本方法置 _tickThreadRunning=false 让 Tick 循环自然退出
    //Join 超时 2s 防卡死 Tick 异常时已 RequestClose 主线程必到此处
    //Join 后检查 _tickException 非空则重新抛让 MinecraftClient.Run finally 感知
    protected override void OnAfterRun()
    {
        _tickThreadRunning = false;
        var t = _tickThread;
        if (t is not null && !t.Join(TickJoinTimeoutMs))
        {
            throw new TimeoutException($"Tick 线程 {TickJoinTimeoutMs}ms 未退出疑似死锁");
        }
        if (_tickException is not null)
        {
            var ex = _tickException;
            _tickException = null;
            throw new InvalidOperationException("Tick 线程异常", ex);
        }
    }

    //TickThreadLoop 20tps 业务更新循环
    //Stopwatch 测量每周期耗时 不足 50ms 则 Thread.Sleep 补足保持稳定 20tps
    //每周期先 FrameTick.Invoke(delta) 让 MinecraftClient 做业务再 SubmitFrame 发布快照
    //异常捕获存 _tickException 并 RequestClose 让主线程 OnAfterRun 重新抛
    private void TickThreadLoop()
    {
        try
        {
            var watch = Stopwatch.StartNew();
            var lastElapsed = 0.0;
            var tickCount = 0;
            var lastTpsTime = 0.0;
            while (_tickThreadRunning)
            {
                var elapsed = watch.Elapsed.TotalSeconds;
                var delta = elapsed - lastElapsed;
                lastElapsed = elapsed;
                FrameTick?.Invoke(delta);
                SubmitFrame();
                tickCount++;
                if (elapsed - lastTpsTime >= 1.0)
                {
                    TickRate = tickCount;
                    tickCount = 0;
                    lastTpsTime = elapsed;
                }
                var frameEnd = watch.Elapsed.TotalSeconds;
                var sleepSeconds = TickTargetInterval - (frameEnd - elapsed);
                if (sleepSeconds > 0)
                {
                    var sleepMs = (int)(sleepSeconds * 1000);
                    if (sleepMs > 0) Thread.Sleep(sleepMs);
                }
                else
                {
                    //已超时让出时间片避免忙等下周期立即开始
                    Thread.Yield();
                }
            }
        }
        catch (Exception ex)
        {
            _tickException = ex;
            RequestClose();
        }
    }

    //OnRecordCommandBuffer render 阶段读 _latestSnapshot 快照 PreparePip+Prepare+Upload+Draw
    //submission 已移到 SubmitFrame 由 Tick 线程调本方法只读快照
    //P15 PreparePip 在 Render 线程持锁调 PIP renderer.Prepare 做 offscreen 渲染+blit
    //vkQueueSubmit 必须在 Render 线程串行 Tick 线程 SubmitFrame 不做 GPU 提交避免 queue 竞争
    //首帧无快照时只 clear 不绑 pipeline 不 Draw
    //阶段7 持 _resourceLock 保护 _resourceManager 字段引用和内部字典与 Tick 线程 RegisterTexture 互斥
    //_guiPipeline/cmd/clearColor 不依赖 _resourceManager 锁外准备
    protected override void OnRecordCommandBuffer(VulkanCommandBuffer cmd, ImageView colorImageView)
    {
        var snapshot = _latestSnapshot;
        var bg = Window.BackgroundColor;
        var clearColor = new Vector4(bg.R, bg.G, bg.B, bg.A);
        if (snapshot is null)
        {
            cmd.BeginRecording();
            //首帧 Tick 未发布快照只 clear swapchain 不 Draw
            using var clearPass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
                _guiPipeline, colorImageView, clearColor, null, 0f);
            clearPass.Close();
            cmd.EndRecording();
            return;
        }
        var sw = Stopwatch.StartNew();
        //P15 PreparePip 在主 cmd BeginRecording 之前做 PIP offscreen 渲染+blit
        //vkQueueSubmit 不嵌套在主 cmd recording 中避免驱动崩溃
        //PIP renderer.Prepare 创建独立 encoder Submit 完成后才 BeginRecording 主 cmd
        lock (_resourceLock)
        {
            _guiRenderer.PreparePip(snapshot, Window.GuiScale);
            //W8.5 世界渲染 Prepare+Upload 移到 BeginRecording 之前避免 DeviceLocal buffer 的 RunOneTimeCommand
            //QueueWaitIdle 在主 cmd 录制期间调用导致驱动状态损坏 CmdDrawIndexed ACCESS_VIOLATION
            if (WorldRenderEnabled && _depthImage is not null)
            {
                _worldViewProjBuffer!.Upload<Matrix4x4>(new[] { _levelRenderer!.ViewProj });
                _levelRenderer.Prepare();
                _levelRenderer.Upload(_device);
            }
        }
        cmd.BeginRecording();
        lock (_resourceLock)
        {
            _resourceManager.UpdateProjection();
            _guiRenderer.Prepare(snapshot);
            _guiRenderer.Upload(_device);
            //世界渲染启用时走世界+GUI 叠加 pass blur 后处理暂不与世界渲染同帧 W8 后续优化
            if (WorldRenderEnabled && _depthImage is not null)
            {
                RenderWorldAndGuiPass(cmd, colorImageView, clearColor);
            }
            else if (snapshot.HasBlurSplit && _blurPipeline is not null && _blurOffscreen is not null && _blurTemp is not null)
            {
                RenderBlurPasses(cmd, colorImageView, clearColor);
            }
            else
            {
                RenderSinglePass(cmd, colorImageView, clearColor);
            }
        }
        RenderCpuMs = sw.Elapsed.TotalMilliseconds;
        cmd.EndRecording();
    }

    //RenderSinglePass 无 blur 分段单 render pass 全量渲染到 swapchain
    private void RenderSinglePass(VulkanCommandBuffer cmd, ImageView colorImageView, Vector4 clearColor)
    {
        using var pass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
            _guiPipeline, colorImageView, clearColor, null, 0f);
        pass.BindDescriptorSet(_resourceManager.GlobalsDescriptorSet, 0);
        pass.BindDescriptorSet(_resourceManager.ProjectionDescriptorSet, 1);
        _guiRenderer.Draw(pass,
            p => _device.PipelineCache.Precompile(p),
            t => _resourceManager.ResolveDescriptorSet(t));
        pass.Close();
    }

    //RenderWorldAndGuiPass 世界渲染+GUI 叠加两段 pass
    //世界 pass 清色清深 Solid→Cutout→Translucent 顺序绘制 terrain
    //GUI pass LoadOp=Load 保留世界颜色无深度叠加 GUI 控件
    //Prepare/Upload 已在 OnRecordCommandBuffer 的 BeginRecording 之前完成避免 RunOneTimeCommand 污染主 cmd
    private void RenderWorldAndGuiPass(VulkanCommandBuffer cmd, ImageView colorImageView, Vector4 clearColor)
    {
        //世界 pass 初始 pipeline 用 SOLID_TERRAIN 携带 depth layout DepthStencilAttachmentOptimal
        //LevelRenderer.Draw 内部 SetPipeline 切换 Solid/Cutout/Translucent 共享同 layout 重复绑 desc 无害
        using var worldPass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
            _device.PipelineCache.Precompile(WorldRenderPipelines.SOLID_TERRAIN),
            colorImageView, clearColor, _depthImage, 1.0f);
        _levelRenderer!.Draw(worldPass,
            p => _device.PipelineCache.Precompile(p),
            p =>
            {
                p.BindDescriptorSet(_worldViewProjSet!, 0);
                p.BindDescriptorSet(_worldAtlasSet!, 1);
            });
        worldPass.Close();

        //GUI pass LoadOp=Load 保留世界渲染结果 null depth 不附加深度附件
        using var guiPass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
            _guiPipeline, colorImageView, clearColor, AttachmentLoadOp.Load, null, 0f);
        guiPass.BindDescriptorSet(_resourceManager.GlobalsDescriptorSet, 0);
        guiPass.BindDescriptorSet(_resourceManager.ProjectionDescriptorSet, 1);
        _guiRenderer.Draw(guiPass,
            p => _device.PipelineCache.Precompile(p),
            t => _resourceManager.ResolveDescriptorSet(t));
        guiPass.Close();
    }

    //RenderBlurPasses 分段渲染 BeforeBlur→水平blur→垂直blur→AfterBlur
    //BeforeBlur 渲染到 offscreen 水平 blur offscreen→temp 垂直 blur temp→swapchain
    //AfterBlur 段 LoadOp=Load 保留模糊背景叠加 GUI 控件
    private void RenderBlurPasses(VulkanCommandBuffer cmd, ImageView colorImageView, Vector4 clearColor)
    {
        var offscreen = (VulkanImage)_blurOffscreen!;
        var temp = (VulkanImage)_blurTemp!;
        var blurPipeline = _blurPipeline!;
        var firstAfterBlur = _guiRenderer.FirstMeshIndexAfterBlur;

        //BeforeBlur 段渲染到 offscreen layout 转 ColorAttachmentOptimal LoadOp=Clear 清屏
        offscreen.TransitionLayout(cmd.Handle, ImageLayout.ColorAttachmentOptimal);
        using (var beforePass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
            _guiPipeline, offscreen.View, clearColor, AttachmentLoadOp.Clear, null, 0f))
        {
            beforePass.BindDescriptorSet(_resourceManager.GlobalsDescriptorSet, 0);
            beforePass.BindDescriptorSet(_resourceManager.ProjectionDescriptorSet, 1);
            _guiRenderer.DrawRange(beforePass,
                p => _device.PipelineCache.Precompile(p),
                t => _resourceManager.ResolveDescriptorSet(t),
                0, firstAfterBlur);
            beforePass.Close();
        }

        //水平 blur offscreen→temp offscreen 转 ShaderReadOnly temp 转 ColorAttachmentOptimal
        offscreen.TransitionLayout(cmd.Handle, ImageLayout.ShaderReadOnlyOptimal);
        temp.TransitionLayout(cmd.Handle, ImageLayout.ColorAttachmentOptimal);
        UpdateBlurUniform(1f, 0f);
        using (var hBlurPass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
            blurPipeline, temp.View, clearColor, AttachmentLoadOp.DontCare, null, 0f))
        {
            hBlurPass.BindDescriptorSet(_blurOffscreenDescriptorSet!, 0);
            hBlurPass.BindDescriptorSet(_blurUniformDescriptorSet!, 1);
            hBlurPass.DisableScissor();
            hBlurPass.Draw(6, 1, 0, 0);
            hBlurPass.Close();
        }

        //垂直 blur temp→swapchain temp 转 ShaderReadOnlyOptimal swapchain LoadOp=Clear 覆盖
        temp.TransitionLayout(cmd.Handle, ImageLayout.ShaderReadOnlyOptimal);
        UpdateBlurUniform(0f, 1f);
        using (var vBlurPass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
            blurPipeline, colorImageView, clearColor, AttachmentLoadOp.Clear, null, 0f))
        {
            vBlurPass.BindDescriptorSet(_blurTempDescriptorSet!, 0);
            vBlurPass.BindDescriptorSet(_blurUniformDescriptorSet!, 1);
            vBlurPass.DisableScissor();
            vBlurPass.Draw(6, 1, 0, 0);
            vBlurPass.Close();
        }

        //AfterBlur 段渲染到 swapchain LoadOp=Load 保留垂直 blur 结果叠加 GUI 控件
        using (var afterPass = new VulkanRenderPass(_device.Api, _device.DynamicRenderingExt, cmd.Handle,
            _guiPipeline, colorImageView, clearColor, AttachmentLoadOp.Load, null, 0f))
        {
            afterPass.BindDescriptorSet(_resourceManager.GlobalsDescriptorSet, 0);
            afterPass.BindDescriptorSet(_resourceManager.ProjectionDescriptorSet, 1);
            _guiRenderer.DrawRange(afterPass,
                p => _device.PipelineCache.Precompile(p),
                t => _resourceManager.ResolveDescriptorSet(t),
                firstAfterBlur, _guiRenderer.Meshes.Count);
            afterPass.Close();
        }
    }

    //OnCleanupPipelineResources 销毁新路径资源
    //InputContext 显式 Dispose 释放 Silk.NET.Input.Glfw 静态字典的 window→context 映射
    //避免 window handle 复用时 CreateInput 抛 More than one input context
    //此时 _window 还未 Reset(GLFW 回调有效)Dispose 安全 VulkanAppBase.Cleanup 顺序保证
    protected override void OnCleanupPipelineResources()
    {
        _resourceManager.Dispose();
        _guiRenderer.Dispose();
        DisposeBlurResources();
        //ItemAtlas/ItemPipRenderer 不依赖 swapchain extent 在 Cleanup 释放
        _itemAtlas?.Dispose();
        _itemPipRenderer?.Dispose();
        _itemAtlas = null;
        _itemPipRenderer = null;
        _itemAtlasTextureId = 0;
        //F7 释放 GlyphStitcher 及其管理的 FontTexture 图集
        _glyphStitcher?.Dispose();
        _glyphStitcher = null;
        _font = null;
        if (_inputContext is not null)
        {
            _inputContext.Dispose();
            _inputContext = null;
        }
        //释放注入的真实纹理图集/lightmap device 销毁前必须释放 GPU 资源
        _injectedBlockAtlas?.Dispose();
        _injectedBlockAtlas = null;
        _injectedLightmap?.Dispose();
        _injectedLightmap = null;
    }

    public override void Dispose()
    {
        if (_disposed) return;
        base.Dispose();
        Window.Dispose();
        _disposed = true;
    }
}

//InputEvent 输入事件基类由 Silk 回调线程入队 PollInput 出队派发
internal abstract record InputEvent;
internal sealed record MouseDownInput(GuiMouseButton Button, int X, int Y) : InputEvent;
internal sealed record MouseUpInput(GuiMouseButton Button, int X, int Y) : InputEvent;
internal sealed record MouseMoveInput(int X, int Y) : InputEvent;
internal sealed record KeyInput(int Key, bool Down) : InputEvent;
internal sealed record CharInput(char Char) : InputEvent;
