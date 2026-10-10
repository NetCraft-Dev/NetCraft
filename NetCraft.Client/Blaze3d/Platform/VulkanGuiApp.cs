using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Gui;
using NetCraft.Client.Resources.Metadata.Gui;
using Silk.NET.Input;
using Silk.NET.Vulkan;
using VkFormat = Silk.NET.Vulkan.Format;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Blaze3d;

namespace NetCraft.Client.Blaze3d.Platform;

//VulkanGuiApp GUI rendering main program
//Extends VulkanAppBase; stage 5 switches to the new GuiRenderContext+GuiRenderer+GuiResourceManager path
//The submission phase Window.Render(GuiRenderContext) submits RenderStates
//The render phase GuiRenderer.Draw(IRenderPass, pipelineResolver, descriptorResolver)
//Stage 11.49 subscribes to Silk Input events, caching them into a queue for PollInput to dispatch to GuiWindow
public sealed unsafe class VulkanGuiApp : VulkanAppBase
{
    //5a new path fields replacing the legacy VulkanGuiRenderer
    private GuiRenderState _renderState = null!;
    private GuiRenderer _guiRenderer = null!;
    private GuiRenderContext _renderContext = null!;
    private GuiResourceManager _resourceManager = null!;
    //_guiPipeline the precompiled RenderPipelines.GUI, precompiled so the first frame does not stutter
    private CompiledRenderPipeline _guiPipeline = null!;
    private FontAtlas? _fontAtlas;
    //F7 GlyphFont dynamic bake path GlyphStitcher+GlyphFont replacing FontAtlas
    private GlyphStitcher? _glyphStitcher;
    private GlyphFont? _font;
    //P0 GuiSpriteManager caches GuiSprites by identifier, lazily loading PNG+.mcmeta
    //On swapchain recreation ClearCache invalidates old TextureSetups
    private GuiSpriteManager? _spriteManager;
    //The logging abstraction decoupled from NetCraft.Logging, passed to GuiResourceManager
    private readonly IGpuLogger _logger;
    //Input event queue Silk callback threads enqueue; MinecraftClient.Tick calls PollInput to dequeue and dispatch
    private readonly ConcurrentQueue<InputEvent> _inputQueue = new();
    //IInputContext the input context returned by CreateInput, released on Dispose
    private IInputContext? _inputContext;
    //_latestSnapshot Tick writes publish with Interlocked.Exchange, read volatile on the Render thread
    //Stage 7 Tick/Render decoupling; Render reads only snapshots and never accesses _renderState
    private volatile GuiRenderState? _latestSnapshot;
    //_resourceLock protects _resourceManager/_renderContext replacement and keeps references stable during submission
    private readonly object _resourceLock = new();

    //Blur post-processing resources: the BeforeBlur segment renders to offscreen, horizontally blurs to temp and vertically blurs to the swapchain
    //The AfterBlur segment uses LoadOp=Load to preserve the blurred background with GUI widgets overlaid
    private GpuTexture? _blurOffscreen;
    private GpuTexture? _blurTemp;
    private GpuTextureView? _blurOffscreenView;
    private GpuTextureView? _blurTempView;
    private GpuSampler? _blurSampler;
    private GpuBuffer? _blurUniformBuffer;
    //BlurRadius sample step radius controlling blur strength; larger is blurrier
    private const float BlurRadius = 2.0f;

    //P15 ItemItemAtlas renders the 3D item atlas into AtlasTexture for Hotbar item icons to sample
    //Independent of swapchain extent and reused across resizes; textureId is re-registered when _resourceManager is rebuilt
    private ItemItemAtlas? _itemAtlas;
    //ItemAtlasTextureId the textureId obtained by registering AtlasTexture with GuiResourceManager, for GameScreen DrawImage
    private int _itemAtlasTextureId;
    //P15 ItemPipRenderer oversized-item PIP offscreen renderer registered with GuiRenderer, dispatched by ItemPipState
    private ItemPipRenderer? _itemPipRenderer;

    //W7 world render resources depth image + ViewProj UBO + atlas/lightmap sampler descriptor set
    //IWorldRenderer injected by the Game layer; VulkanGuiApp schedules Prepare/Upload/Draw and manages GPU resources
    //Using an interface avoids a circular dependency where NetCraft.Gpu references NetCraft.Game
    private IWorldRenderer? _levelRenderer;
    private GpuTexture? _depthImage;
    private GpuTextureView? _depthView;
    private GpuBuffer? _worldViewProjBuffer;
    private GpuTexture? _blockAtlasImage;
    private GpuTexture? _lightmapImage;
    private GpuTextureView? _worldBlockAtlasView;
    private GpuSampler? _worldBlockAtlasSampler;
    private GpuTextureView? _worldLightmapView;
    private GpuSampler? _worldLightmapSampler;
    private GpuSampler? _worldSampler;
    //_injectedBlockAtlas/_injectedLightmap real textures injected by the Game layer; when null CreateWorldResources uses placeholders
    //Injected resources are managed by this class and released in OnCleanupPipelineResources; DisposeWorldResources does not release them
    private BlockTextureAtlas? _injectedBlockAtlas;
    private LightTexture? _injectedLightmap;
    //Whether world rendering is enabled; OnRecordCommandBuffer checks this to decide whether to call the world RenderPass
    public bool WorldRenderEnabled => _levelRenderer is not null;

    //Device exposes the GpuDevice for the Game layer to create GpuBufferPool and other GPU resources on SwapchainRecreated
    //_device is ready when SwapchainRecreated fires; access before construction returns a null-forgiving instance
    public GpuDevice Device => _device;

    //GuiWindow created externally and passed in, letting test code pre-arrange widgets
    public GuiWindow Window { get; }

    //RawKeyDown raw key code events subscribed by the Game layer to detect game keys without depending on Silk.Input
    public event Action<int>? RawKeyDown;

    //RawMouseDown/RawMouseUp raw mouse button events with window coordinates, subscribed by the Game layer for game interaction
    //GuiWindow dispatches only on widget hits and cannot see world interaction (like block breaking), so a separate raw channel is required
    public event Action<GuiMouseButton, int, int>? RawMouseDown;
    public event Action<GuiMouseButton, int, int>? RawMouseUp;

    //RawMouseMove raw mouse move events with window coordinates for screens to record the hover position
    public event Action<int, int>? RawMouseMove;

    //SwapchainRecreated fired after swapchain recreation; ScreenManager.Resized re-lays out the current Screen
    //The Game layer subscribes to trigger Screen.Init, re-arranging widgets for the new window size
    public event System.Action? SwapchainRecreated;

    //FrameTick game update event fired by the Tick thread at 20tps, replacing the legacy FrameUpdate
    //MinecraftClient subscribes to do PollInput/Window.Update/Screens.Tick/Connection.Tick
    //After the event returns this class immediately calls SubmitFrame to publish a snapshot for the Render thread
    public event Action<double>? FrameTick;

    //TickThread the 20tps game update thread, the core of the stage 7 Tick/Render decoupling
    //Run is IsBackground so it does not block process exit; OnAfterRun Joins to synchronize
    //_tickThreadRunning false makes the loop exit; RequestClose triggers _window.Close so _window.Run exits
    private Thread? _tickThread;
    private volatile bool _tickThreadRunning;
    //_tickException an uncaught Tick thread exception rethrown by the main thread after OnAfterRun Joins
    private Exception? _tickException;
    //TickTargetInterval 20tps corresponds to 50ms, consistent with vanilla Minecraft
    private const double TickTargetInterval = 1.0 / 20.0;
    //TickJoinTimeoutMs OnAfterRun waits for the Tick thread to exit and throws TimeoutException on timeout to prevent a hang
    private const int TickJoinTimeoutMs = 2000;
    //Perf acceptance metrics for GameScreen F3 to display
    //SubmissionCpuMs CPU time of the last frame's submission (Window.Render building RenderStates)
    //RenderCpuMs CPU time of the last frame's render (Prepare+Upload+Draw)
    //TickRate actual tps over the last second, target 20
    //DrawCallCount/MeshCount/VertexCount passed through from GuiRenderer's same-named fields
    //PipelineHits/PipelineMisses passed through from PipelineCache's same-named fields
    public double SubmissionCpuMs { get; private set; }
    public double RenderCpuMs { get; private set; }
    public int TickRate { get; private set; }
    public int DrawCallCount => _guiRenderer?.DrawCallCount ?? 0;
    public int MeshCount => _guiRenderer?.MeshCount ?? 0;
    public int VertexCount => _guiRenderer?.VertexCount ?? 0;
    public int PipelineHits => _vkDevice?.PipelineHits ?? 0;
    public int PipelineMisses => _vkDevice?.PipelineMisses ?? 0;

    //ItemAtlas the 3D item atlas for GameScreen to call GetOrUpdate for UVs + trigger DrawToSlot
    //ItemAtlasTextureId the AtlasTexture's textureId for GameScreen to call DrawImage and sample the atlas
    //null/0 means not created (headless mode or before OnCreatePipelineResources)
    public ItemItemAtlas? ItemAtlas => _itemAtlas;
    public int ItemAtlasTextureId => _itemAtlasTextureId;
    //ItemPipRenderer oversized-item PIP renderer for GameLayer/tests to RegisterItem item models
    //null means not created (before OnCreatePipelineResources)
    public ItemPipRenderer? ItemPipRenderer => _itemPipRenderer;

    //SetLevelRenderer injects the world renderer; the Game layer calls it after creating a LevelRenderer
    //When null world rendering is disabled; OnRecordCommandBuffer skips the world RenderPass and renders only the GUI
    public void SetLevelRenderer(IWorldRenderer? renderer) => _levelRenderer = renderer;

    //SetBlockAtlas injects the real block texture atlas, built by the Game layer with BlockTextureCollector+BlockTextureAtlas.Build
    //null restores the placeholder texture; CreateWorldResources checks this field to choose real or placeholder
    //After injection ownership transfers to this class and OnCleanupPipelineResources releases it; not released on swapchain recreation
    public void SetBlockAtlas(BlockTextureAtlas? atlas) => _injectedBlockAtlas = atlas;

    //SetLightmap injects the real lightmap; the Game layer injects it after creating a LightTexture
    //null restores the placeholder texture; after injection ownership transfers to this class and OnCleanupPipelineResources releases it
    public void SetLightmap(LightTexture? lightmap) => _injectedLightmap = lightmap;

    //LevelRenderer perf metrics for GameScreen F3 to display world render stats
    public int WorldSectionCount => _levelRenderer?.SectionCount ?? 0;
    public int WorldVisibleSectionCount => _levelRenderer?.VisibleSectionCount ?? 0;
    public int WorldVertexCount => _levelRenderer?.TotalVertexCount ?? 0;
    public int WorldDrawCallCount => _levelRenderer?.DrawCallCount ?? 0;

    //RegisterTexture loads a PNG by path, registers it as a texture and returns a textureId for GuiImage to reference
    //Delegates to the current _resourceManager; after swapchain recreation _resourceManager is new so textureId may change
    //Screen.Init calls this method again during Resized re-layout to get new ids for GuiImage
    //_resourceManager is null before OnCreatePipelineResources and returns 0, using the placeholder
    //At the end of OnCreatePipelineResources it fires SwapchainRecreated so Screen re-Init gets real textureIds
    //Stage 7 holds _resourceLock to protect the _resourceManager field and inner dictionaries from racing with OnSwapchainRecreated rebuilds
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

    //enableVsync is passed by the caller from GameConfig.EnableVsync so the GPU layer does not depend on the GameConfig type
    public VulkanGuiApp(bool enableVsync, int width, int height, IGpuLogger? logger = null) : base(width, height)
    {
        EnableVsync = enableVsync;
        _logger = logger ?? new ConsoleGpuLogger();
        Window = new GuiWindow(width, height);
    }

    protected override string WindowTitle => "NetCraft.Gpu VulkanGuiApp";

    //OnCreatePipelineResources creates the new path's resources
    //_swapchainExtent/_swapchainImageFormat are ready at this point
    //First UpdateSurfaceSize recomputes guiScale, then GuiResourceManager/GuiRenderState/GuiRenderer/GuiRenderContext are created
    //Precompiles the GUI pipeline series to avoid runtime compile stutter
    //At the end it fires SwapchainRecreated to notify Screen to re-Init, when _resourceManager is ready to load textures
    //F7 initializes the Font dynamic bake path GlyphStitcher+FontSet+Font replacing FontAtlas
    protected override void OnCreatePipelineResources()
    {
        var w = (int)_swapchainExtent.Width;
        var h = (int)_swapchainExtent.Height;
        Window.UpdateSurfaceSize(w, h);
        _fontAtlas = FontAtlas.FromSystemFont();
        _resourceManager = new GuiResourceManager(_device, w, h, _fontAtlas, _logger);
        _renderState = new GuiRenderState();
        _guiRenderer = new GuiRenderer();
        //F7 creates the Font dynamic bake path GlyphStitcher, wiring into GuiResourceManager to register the atlas
        _glyphStitcher = new GlyphStitcher(_device, _resourceManager);
        _font = CreateFontFromAssets();
        //P0 creates GuiSpriteManager and injects it into GuiRenderContext so DrawSprite dispatches by .mcmeta
        _spriteManager = new GuiSpriteManager(Path.Combine(AppContext.BaseDirectory, "assets"), _resourceManager);
        _renderContext = new GuiRenderContext(_renderState, w, h, Window.GuiScale,
            _font, _resourceManager.FontAtlas, _resourceManager.FontTexture,
            id => _resourceManager.ResolveTexture(id),
            _spriteManager);
        PrecompileGuiPipelines();
        CreateBlurResources(w, h);
        CreateItemAtlasResources();
        //SwapchainRecreated fires first so the Game layer injects real textures+LevelRenderer, then world resources are created
        //The first world resource creation already uses the injected real atlas/lightmap instead of placeholders
        SwapchainRecreated?.Invoke();
        CreateWorldResources(w, h);
    }

    //CreateItemAtlasResources creates ItemItemAtlas+ItemPipRenderer and registers them with GuiRenderer+ResourceManager
    //_itemAtlas/_itemPipRenderer do not depend on swapchain extent and are reused across resizes
    //_itemAtlasTextureId depends on _resourceManager and re-RegisterImages on rebuild
    //ItemItemAtlas is only newed when _itemAtlas is null on first creation; later resizes skip creation and only re-register the textureId
    private void CreateItemAtlasResources()
    {
        if (_itemAtlas is null)
        {
            //512x512 atlas 64x64 slots 8x8=64 slots, matching the VulkanItemAtlasApp test config
            _itemAtlas = new ItemItemAtlas(_device, 512, 64);
            _itemPipRenderer = new ItemPipRenderer(_device);
            _guiRenderer.RegisterPipRenderer(_itemPipRenderer);
        }
        _itemAtlasTextureId = _resourceManager.RegisterImage(_itemAtlas.AtlasTexture);
    }

    //CreateWorldResources creates world render GPU resources depth image + ViewProj UBO + atlas/lightmap sampler
    //blockAtlas the first version uses a 1x1 white placeholder texture; W9 wires the real block atlas; lightmap uses a 16x16 full-bright placeholder
    //On swapchain recreation the old resources are Disposed and rebuilt to match the new extent
    private void CreateWorldResources(int w, int h)
    {
        //Precompiles the 3 terrain pipelines to avoid first-compile stutter in OnRecordCommandBuffer
        _device.PrecompilePipeline(WorldRenderPipelines.SOLID_TERRAIN);
        _device.PrecompilePipeline(WorldRenderPipelines.CUTOUT_TERRAIN);
        _device.PrecompilePipeline(WorldRenderPipelines.TRANSLUCENT_TERRAIN);

        //depth image D32Sfloat DepthAttachment with the same extent as the swapchain
        _depthImage = _device.CreateTexture(null, GpuTexture.UsageRenderAttachment, GpuFormat.D32Float, w, h, 1, 1);
        //First layout transition of the depth image Undefined→DepthStencilAttachmentOptimal
        //Upload does not read pixels for a DepthAttachment and only does the barrier so dynamic rendering sees the expected layout
        _depthImage.Upload(ReadOnlySpan<byte>.Empty);
        _depthView = _device.CreateTextureView(_depthImage);

        //set 0 MATRICES_PROJECTION, bound by the name "Matrices"
        _worldViewProjBuffer = _device.CreateBuffer(null, GpuBuffer.UsageUniform | GpuBuffer.UsageMapWrite, 64);

        //set 1 SAMPLER0_SAMPLER1 atlas + lightmap, bound by the names "Sampler0"/"Sampler1"
        //Placeholder textures share a linear sampler; injected textures bring their own nearest sampler
        _worldSampler = _device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear, 1, null);

        //If blockAtlas is injected use the real atlas, otherwise create a 1x1 white RGBA8 placeholder
        GpuTexture blockAtlasImage;
        GpuSampler blockAtlasSampler;
        if (_injectedBlockAtlas is not null && _injectedBlockAtlas.AtlasImage is not null && _injectedBlockAtlas.Sampler is not null)
        {
            blockAtlasImage = _injectedBlockAtlas.AtlasImage;
            blockAtlasSampler = _injectedBlockAtlas.Sampler;
        }
        else
        {
            _blockAtlasImage = _device.CreateTexture(null, GpuTexture.UsageTextureBinding, GpuFormat.Rgba8Unorm, 1, 1, 1, 1);
            _blockAtlasImage.Upload(new byte[] { 255, 255, 255, 255 });
            blockAtlasImage = _blockAtlasImage;
            blockAtlasSampler = _worldSampler;
        }

        //If lightmap is injected use the real LightTexture, otherwise create a 16x16 full-bright RGBA8 placeholder
        GpuTexture lightmapImage;
        GpuSampler lightmapSampler;
        if (_injectedLightmap is not null && _injectedLightmap.Texture is not null && _injectedLightmap.Sampler is not null)
        {
            lightmapImage = _injectedLightmap.Texture;
            lightmapSampler = _injectedLightmap.Sampler;
        }
        else
        {
            _lightmapImage = _device.CreateTexture(null, GpuTexture.UsageTextureBinding, GpuFormat.Rgba8Unorm, 16, 16, 1, 1);
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

        _worldBlockAtlasView = _device.CreateTextureView(blockAtlasImage);
        _worldBlockAtlasSampler = blockAtlasSampler;
        _worldLightmapView = _device.CreateTextureView(lightmapImage);
        _worldLightmapSampler = lightmapSampler;
    }

    //DisposeWorldResources releases world render resources, called on swapchain recreation and Cleanup
    private void DisposeWorldResources()
    {
        _depthView?.Dispose();
        _depthImage?.Dispose();
        _worldViewProjBuffer?.Dispose();
        _worldBlockAtlasView?.Dispose();
        _worldLightmapView?.Dispose();
        _blockAtlasImage?.Dispose();
        _lightmapImage?.Dispose();
        _worldSampler?.Dispose();
        _depthView = null;
        _depthImage = null;
        _worldViewProjBuffer = null;
        _worldBlockAtlasView = null;
        _worldBlockAtlasSampler = null;
        _worldLightmapView = null;
        _worldLightmapSampler = null;
        _blockAtlasImage = null;
        _lightmapImage = null;
        _worldSampler = null;
    }

    //CreateFontFromAssets loads the font config from assets/minecraft/font/<id>.json and builds a GlyphFont
    //F7 loads the minecraft:alt font by default (ASCII+Sga) with ascent=7 lineHeight=9, maps to the vanilla BitmapProvider ascent
    //When the assets path does not exist it falls back to the FontAtlas system font with _font=null
    private GlyphFont? CreateFontFromAssets()
    {
        var assetsRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        if (!Directory.Exists(assetsRoot))
        {
            _logger.Warning($"assets directory does not exist {assetsRoot}; GlyphFont initialization falls back to FontAtlas");
            return null;
        }
        var accessor = new AssetsFontResourceAccessor(assetsRoot);
        //Loads the minecraft:alt font by default, maps to the vanilla default font
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
            _logger.Warning($"font config does not exist {fontPath}; GlyphFont initialization falls back to FontAtlas");
            return null;
        }
        List<IGlyphProvider.Conditional> providers;
        using (stream)
        {
            providers = FontProviderDefinitionLoader.Load(stream, accessor);
        }
        var fontSet = new FontSet();
        fontSet.Reload(providers, new HashSet<FontOption>());
        //alt.json's BitmapProvider ascent=7 lineHeight=8, maps to the vanilla ASCII bitmap font
        return new GlyphFont(fontSet, _glyphStitcher!, ascent: 7, lineHeight: 9);
    }

    //PrecompileGuiPipelines precompiles the GUI pipeline series into PipelineCache for zero-compile cache hits
    //Declarative pipelines default to extent 800x600, matching the test swapchain
    //Production windows that are not 800x600 need a dynamic viewport; a later optimization
    private void PrecompileGuiPipelines()
    {
        _guiPipeline = _device.PrecompilePipeline(RenderPipelines.GUI);
        _device.PrecompilePipeline(RenderPipelines.GUI_INVERT);
        _device.PrecompilePipeline(RenderPipelines.GUI_TEXT);
        _device.PrecompilePipeline(RenderPipelines.GUI_TEXTURED);
        //F7 precompiles the font pipeline to avoid first-compile stutter during Bake
        _device.PrecompilePipeline(RenderPipelines.GUI_TEXT_GRAYSCALE);
        _device.PrecompilePipeline(RenderPipelines.GUI_TEXT_SEE_THROUGH);
        _device.PrecompilePipeline(RenderPipelines.GUI_TEXT_POLYGON_OFFSET);
        _device.PrecompilePipeline(RenderPipelines.GUI_TEXT_GRAYSCALE_SEE_THROUGH);
        _device.PrecompilePipeline(RenderPipelines.GUI_TEXT_GRAYSCALE_POLYGON_OFFSET);
    }

    //CreateBlurResources creates blur post-processing resources offscreen+temp double textures+uniform
    //BeforeBlur renders to offscreen, horizontally blurs offscreen→temp and vertically blurs temp→swapchain
    //On swapchain recreation the old resources are Disposed and rebuilt to match the new extent
    private void CreateBlurResources(int w, int h)
    {
        var imageUsage = GpuTexture.UsageRenderAttachment | GpuTexture.UsageTextureBinding;
        _blurOffscreen = _device.CreateTexture(null, imageUsage, GpuFormat.Rgba8Unorm, w, h, 1, 1);
        _blurTemp = _device.CreateTexture(null, imageUsage, GpuFormat.Rgba8Unorm, w, h, 1, 1);
        _blurOffscreenView = _device.CreateTextureView(_blurOffscreen);
        _blurTempView = _device.CreateTextureView(_blurTemp);
        _blurSampler = _device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear, 1, null);
        //The blur uniform is bound by the name "BlurConfig" declared in BindGroupLayouts.BLUR_CONFIG
        _blurUniformBuffer = _device.CreateBuffer(null, GpuBuffer.UsageUniform | GpuBuffer.UsageMapWrite, 16);
        _device.PrecompilePipeline(RenderPipelines.BLUR);
    }

    //DisposeBlurResources releases blur resources, called on swapchain recreation and Cleanup
    private void DisposeBlurResources()
    {
        _blurOffscreenView?.Dispose();
        _blurTempView?.Dispose();
        _blurOffscreen?.Dispose();
        _blurTemp?.Dispose();
        _blurSampler?.Dispose();
        _blurUniformBuffer?.Dispose();
        _blurOffscreenView = null;
        _blurTempView = null;
        _blurOffscreen = null;
        _blurTemp = null;
        _blurSampler = null;
        _blurUniformBuffer = null;
    }

    //UpdateBlurUniform uploads BlurConfig vec4 Data xy=BlurDir z=Radius w=0
    //Horizontal pass BlurDir=(1,0) vertical pass BlurDir=(0,1), 2 updates per frame
    private void UpdateBlurUniform(float dirX, float dirY)
    {
        _blurUniformBuffer!.Upload<float>(new float[] { dirX, dirY, BlurRadius, 0f });
    }

    //OnInitialized after window init calls CreateInput to create the input context and subscribe to mouse/keyboard events
    //KeyChar text input is not wired yet and will be added when widgets like TextBox need it
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

    //EnqueueMouse converts a Silk MouseButton to GuiMouseButton and enqueues it with the current mouse coordinates
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

    //PollInput dispatches the input queue to GuiWindow, called by MinecraftClient.Tick each frame
    public void PollInput()
    {
        while (_inputQueue.TryDequeue(out var ev))
        {
            switch (ev)
            {
                case MouseDownInput m:
                    //Raw events go to the Game layer first then through widget hit-testing so both receive them
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
                        //RawKeyDown exposes the raw key code for the Game layer's ScreenManager to detect game keys
                        //The raw event is not consumed and continues to the focused widget, supporting scenarios with no game subscriptions
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

    //OnSwapchainRecreated rebuilds extent-dependent resources when the swapchain extent changes
    //_resourceManager rebuilt Projection recomputed + font/image texture DescriptorSets rebuilt
    //_renderContext rebuilt because the size changed; _guiRenderer reuses buffers across frames and is not rebuilt
    //PipelineCache is emptied and pipelines rebuilt so the extent matches the new swapchain
    //At the end it fires the SwapchainRecreated event so ScreenManager.Resized sets _layoutDirty for the Tick thread to consume
    //Stage 7 the Render thread fires it holding _resourceLock, mutually exclusive with the Tick thread's SubmitFrame/RegisterTexture
    //Inside the lock it calls Window.UpdateSurfaceSize to change GuiScale then immediately rebuilds _renderContext for consistent references
    //Outside the lock it clears _latestSnapshot to keep the Render thread from reading a dangling old TextureSetup; a 1-frame clear-only after resize is acceptable
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
            //F7 rebuilds GlyphStitcher+Font; the new GuiResourceManager needs the atlas re-registered
            _glyphStitcher?.Dispose();
            _glyphStitcher = new GlyphStitcher(_device, _resourceManager);
            _font = CreateFontFromAssets();
            //P0 rebuilds GuiSpriteManager; old textureIds are invalidated and the cache is cleared so DrawSprite reloads
            _spriteManager = new GuiSpriteManager(Path.Combine(AppContext.BaseDirectory, "assets"), _resourceManager);
            _renderContext = new GuiRenderContext(_renderState, w, h, Window.GuiScale,
                _font, _resourceManager.FontAtlas, _resourceManager.FontTexture,
                id => _resourceManager.ResolveTexture(id),
                _spriteManager);
            _device.ClearPipelineCache();
            PrecompileGuiPipelines();
            //Blur resource extent follows the swapchain; old resources are Disposed and rebuilt to match the new extent
            DisposeBlurResources();
            CreateBlurResources(w, h);
            //ItemAtlas is not rebuilt; AtlasTexture is only re-registered for a new textureId since the old _resourceManager is Disposed
            CreateItemAtlasResources();
            //World render resources rebuild the depth image to match the new extent; UBO/sampler are reused across resizes but the layout depends on the unchanged _device
            DisposeWorldResources();
            CreateWorldResources(w, h);
        }
        oldResourceManager?.Dispose();
        Interlocked.Exchange(ref _latestSnapshot, null);
        SwapchainRecreated?.Invoke();
    }

    //SubmitFrame the submission phase builds RenderStates and publishes a deep-copied snapshot to _latestSnapshot
    //The Tick thread calls it holding _resourceLock, reading stable _renderContext/_renderState references not replaced by OnSwapchainRecreated
    //Inside the lock Window.Render calls _textureResolver, whose dictionary reads are mutually exclusive with RegisterTexture/OnSwapchainRecreated
    //P15 PreparePip moved to OnRecordCommandBuffer since vkQueueSubmit must be serial on the Render thread
    //The Tick thread's SubmitFrame and the Render thread's DrawFrame both call vkQueueSubmit on the same queue and need external synchronization
    //Stage 7 Tick/Render decoupling; the Render thread only reads snapshots and does not call this method
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

    //OnBeforeRun starts the Tick thread on the main thread before _window.Run, entering the 20tps loop
    //Run() calls this before entering the blocking _window.Run, when _resourceManager etc. are ready
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

    //OnAfterRun Joins the Tick thread on the main thread after _window.Run exits
    //_window.Close already made _window.Run exit; this method sets _tickThreadRunning=false so the Tick loop exits naturally
    //Join times out after 2s to prevent a hang; on a Tick exception RequestClose was already called so the main thread always reaches here
    //After Join it checks _tickException and rethrows if non-null so MinecraftClient.Run's finally sees it
    protected override void OnAfterRun()
    {
        _tickThreadRunning = false;
        var t = _tickThread;
        if (t is not null && !t.Join(TickJoinTimeoutMs))
        {
            throw new TimeoutException($"Tick thread {TickJoinTimeoutMs}ms did not exit, suspected deadlock");
        }
        if (_tickException is not null)
        {
            var ex = _tickException;
            _tickException = null;
            throw new InvalidOperationException("Tick thread exception", ex);
        }
    }

    //TickThreadLoop the 20tps game update loop
    //Stopwatch measures each cycle; if under 50ms Thread.Sleep makes up the difference to keep a stable 20tps
    //Each cycle first FrameTick.Invoke(delta) lets MinecraftClient do game logic then SubmitFrame publishes a snapshot
    //Exceptions are captured into _tickException and RequestClose lets the main thread's OnAfterRun rethrow
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
                    //Already over time, yield the timeslice to avoid busy-waiting and start the next cycle immediately
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

    //OnRecordCommandBuffer the render phase reads the _latestSnapshot snapshot for PreparePip+Prepare+Upload+Draw
    //Submission moved to SubmitFrame on the Tick thread; this method only reads snapshots
    //P15 PreparePip on the Render thread holds the lock and calls PIP renderer.Prepare for offscreen rendering+blit
    //vkQueueSubmit must be serial on the Render thread; the Tick thread's SubmitFrame does no GPU submission to avoid queue contention
    //On the first frame with no snapshot it only clears without binding a pipeline or drawing
    //Stage 7 holds _resourceLock to protect the _resourceManager field reference and inner dictionaries, mutually exclusive with the Tick thread's RegisterTexture
    //_guiPipeline/cmd/clearColor do not depend on _resourceManager and are prepared outside the lock
    protected override void OnRecordCommandBuffer(CommandEncoder encoder, GpuTextureView sceneView)
    {
        var snapshot = _latestSnapshot;
        var bg = Window.BackgroundColor;
        var clearColor = new Vector4(bg.R, bg.G, bg.B, bg.A);
        if (snapshot is null)
        {
            //First frame with no snapshot published by Tick: only clear the scene target without Draw
            var clearDescriptor = RenderPassDescriptor.Create(() => "gui-clear")
                .WithColorAttachment(sceneView, clearColor)
                .WithRenderArea(SceneRenderArea());
            using var clearPass = encoder.CreateRenderPass(clearDescriptor);
            return;
        }
        var sw = Stopwatch.StartNew();
        //P15 PreparePip does the PIP offscreen rendering+blit before the main recording
        //PIP renderer.Prepare creates an independent encoder and only after its Submit completes does the main recording start
        lock (_resourceLock)
        {
            _guiRenderer.PreparePip(snapshot, Window.GuiScale);
            //W8.5 world render Prepare+Upload moved before recording to avoid RunOneTimeCommand on device-local buffers
            if (WorldRenderEnabled && _depthImage is not null)
            {
                _worldViewProjBuffer!.Upload<Matrix4x4>(new[] { _levelRenderer!.ViewProj });
                _levelRenderer.Prepare();
                _levelRenderer.Upload(_device);
            }
        }
        lock (_resourceLock)
        {
            _resourceManager.UpdateProjection();
            _guiRenderer.Prepare(snapshot);
            _guiRenderer.Upload(_device);
            //When world rendering is enabled it uses a world+GUI composite pass; blur post-processing is not yet in the same frame as world rendering
            if (WorldRenderEnabled && _depthImage is not null)
            {
                RenderWorldAndGuiPass(encoder, sceneView, clearColor);
            }
            else if (snapshot.HasBlurSplit && _blurOffscreenView is not null && _blurTempView is not null)
            {
                RenderBlurPasses(encoder, sceneView, clearColor);
            }
            else
            {
                RenderSinglePass(encoder, sceneView, clearColor);
            }
        }
        RenderCpuMs = sw.Elapsed.TotalMilliseconds;
    }

    //SceneRenderArea the full-extent render area
    private NetCraft.Client.Blaze3d.Systems.RenderPass.RenderArea SceneRenderArea()
        => new(0, 0, (int)_swapchainExtent.Width, (int)_swapchainExtent.Height);

    //BindGuiGlobals writes the GUI global uniforms for the pipeline layout just bound
    private void BindGuiGlobals(NetCraft.Client.Blaze3d.Systems.RenderPass pass)
    {
        pass.SetUniform("Globals", _resourceManager.GlobalsBuffer);
        pass.SetUniform("Matrices", _resourceManager.ProjectionBuffer);
    }

    //RenderSinglePass single render pass without blur splitting, rendering everything to the scene target
    private void RenderSinglePass(CommandEncoder encoder, GpuTextureView sceneView, Vector4 clearColor)
    {
        var descriptor = RenderPassDescriptor.Create(() => "gui")
            .WithColorAttachment(sceneView, clearColor)
            .WithRenderArea(SceneRenderArea());
        using var pass = encoder.CreateRenderPass(descriptor);
        _guiRenderer.Draw(pass, BindGuiGlobals, t => _resourceManager.ResolveTextureBinding(t));
    }

    //RenderWorldAndGuiPass world rendering+GUI composite in two passes
    //The world pass clears color and depth, drawing terrain in Solid→Cutout→Translucent order
    //The GUI pass LoadOp=Load preserves the world color with no depth, overlaying GUI widgets
    //Prepare/Upload already completed before BeginRecording in OnRecordCommandBuffer, avoiding RunOneTimeCommand polluting the main cmd
    private void RenderWorldAndGuiPass(CommandEncoder encoder, GpuTextureView sceneView, Vector4 clearColor)
    {
        var area = SceneRenderArea();
        //The world pass clears color+depth and binds the view-projection matrix by name plus the atlas/lightmap samplers
        var worldDescriptor = RenderPassDescriptor.Create(() => "world")
            .WithColorAttachment(sceneView, clearColor)
            .WithDepthAttachment(_depthView!, 1.0)
            .WithRenderArea(area);
        using (var worldPass = encoder.CreateRenderPass(worldDescriptor))
        {
            _levelRenderer!.Draw(worldPass, BindWorldGlobals);
        }

        //The GUI pass uses LoadOp=Load to preserve the world render result with no depth attachment
        var guiDescriptor = RenderPassDescriptor.Create(() => "gui")
            .WithColorAttachment(sceneView)
            .WithRenderArea(area);
        using var guiPass = encoder.CreateRenderPass(guiDescriptor);
        _guiRenderer.Draw(guiPass, BindGuiGlobals, t => _resourceManager.ResolveTextureBinding(t));
    }

    //BindWorldGlobals writes the terrain uniforms for the pipeline layout just bound
    private void BindWorldGlobals(NetCraft.Client.Blaze3d.Systems.RenderPass pass)
    {
        pass.SetUniform("Matrices", _worldViewProjBuffer!);
        pass.BindTexture("Sampler0", _worldBlockAtlasView!, _worldBlockAtlasSampler!);
        pass.BindTexture("Sampler1", _worldLightmapView!, _worldLightmapSampler!);
    }

    //RenderBlurPasses segmented rendering BeforeBlur→horizontal blur→vertical blur→AfterBlur
    //BeforeBlur renders to offscreen, horizontally blurs offscreen→temp and vertically blurs temp→scene
    //The AfterBlur segment uses LoadOp=Load to preserve the blurred background with GUI widgets overlaid
    private void RenderBlurPasses(CommandEncoder encoder, GpuTextureView sceneView, Vector4 clearColor)
    {
        var offscreen = (VulkanImage)_blurOffscreen!;
        var temp = (VulkanImage)_blurTemp!;
        var vkEncoder = (VulkanCommandEncoder)encoder.Backend;
        var firstAfterBlur = _guiRenderer.FirstMeshIndexAfterBlur;
        var area = SceneRenderArea();

        //The BeforeBlur segment renders to offscreen after transitioning it to a color attachment layout
        offscreen.TransitionLayout(vkEncoder.Handle, ImageLayout.ColorAttachmentOptimal);
        using (var beforePass = encoder.CreateRenderPass(RenderPassDescriptor.Create(() => "blur-before")
            .WithColorAttachment(_blurOffscreenView!, clearColor)
            .WithRenderArea(area)))
        {
            _guiRenderer.DrawRange(beforePass, BindGuiGlobals, t => _resourceManager.ResolveTextureBinding(t), 0, firstAfterBlur);
        }

        //Horizontal blur offscreen→temp, offscreen to ShaderReadOnly, temp to ColorAttachmentOptimal
        offscreen.TransitionLayout(vkEncoder.Handle, ImageLayout.ShaderReadOnlyOptimal);
        temp.TransitionLayout(vkEncoder.Handle, ImageLayout.ColorAttachmentOptimal);
        UpdateBlurUniform(1f, 0f);
        using (var hBlurPass = encoder.CreateRenderPass(RenderPassDescriptor.Create(() => "blur-h")
            .WithColorAttachment(_blurTempView!, clearColor)
            .WithRenderArea(area)))
        {
            hBlurPass.SetPipeline(RenderPipelines.BLUR);
            hBlurPass.SetUniform("BlurConfig", _blurUniformBuffer!);
            hBlurPass.BindTexture("InSampler", _blurOffscreenView!, _blurSampler!);
            hBlurPass.DisableScissor();
            hBlurPass.Draw(6, 1, 0, 0);
        }

        //Vertical blur temp→scene, temp to ShaderReadOnlyOptimal, scene overwritten with LoadOp=Clear
        temp.TransitionLayout(vkEncoder.Handle, ImageLayout.ShaderReadOnlyOptimal);
        UpdateBlurUniform(0f, 1f);
        using (var vBlurPass = encoder.CreateRenderPass(RenderPassDescriptor.Create(() => "blur-v")
            .WithColorAttachment(sceneView, clearColor)
            .WithRenderArea(area)))
        {
            vBlurPass.SetPipeline(RenderPipelines.BLUR);
            vBlurPass.SetUniform("BlurConfig", _blurUniformBuffer!);
            vBlurPass.BindTexture("InSampler", _blurTempView!, _blurSampler!);
            vBlurPass.DisableScissor();
            vBlurPass.Draw(6, 1, 0, 0);
        }

        //The AfterBlur segment renders to the scene target with LoadOp=Load, preserving the vertical blur result
        using (var afterPass = encoder.CreateRenderPass(RenderPassDescriptor.Create(() => "blur-after")
            .WithColorAttachment(sceneView)
            .WithRenderArea(area)))
        {
            _guiRenderer.DrawRange(afterPass, BindGuiGlobals, t => _resourceManager.ResolveTextureBinding(t), firstAfterBlur, _guiRenderer.Meshes.Count);
        }
    }

    //OnCleanupPipelineResources destroys the new path's resources
    //InputContext is explicitly Disposed to release the Silk.NET.Input.Glfw static dictionary's window→context mapping
    //Avoids CreateInput throwing More than one input context when a window handle is reused
    //At this point _window has not been Reset (GLFW callbacks valid) so Dispose is safe; the VulkanAppBase.Cleanup order guarantees it
    protected override void OnCleanupPipelineResources()
    {
        _resourceManager.Dispose();
        _guiRenderer.Dispose();
        DisposeBlurResources();
        //ItemAtlas/ItemPipRenderer do not depend on swapchain extent and are released in Cleanup
        _itemAtlas?.Dispose();
        _itemPipRenderer?.Dispose();
        _itemAtlas = null;
        _itemPipRenderer = null;
        _itemAtlasTextureId = 0;
        //F7 releases GlyphStitcher and the FontTexture atlases it manages
        _glyphStitcher?.Dispose();
        _glyphStitcher = null;
        _font = null;
        if (_inputContext is not null)
        {
            _inputContext.Dispose();
            _inputContext = null;
        }
        //Release the injected real texture atlas/lightmap; GPU resources must be released before the device is destroyed
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

//InputEvent input event base class enqueued by Silk callback threads and dequeued/dispatched by PollInput
internal abstract record InputEvent;
internal sealed record MouseDownInput(GuiMouseButton Button, int X, int Y) : InputEvent;
internal sealed record MouseUpInput(GuiMouseButton Button, int X, int Y) : InputEvent;
internal sealed record MouseMoveInput(int X, int Y) : InputEvent;
internal sealed record KeyInput(int Key, bool Down) : InputEvent;
internal sealed record CharInput(char Char) : InputEvent;
