using System.Diagnostics;
using System.Numerics;
using System.Threading;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Client.Language;
using NetCraft.Game.Client.Render;
using NetCraft.Game.Client.Render.Atlas;
using NetCraft.Game.Client.Render.Entity;
using NetCraft.Game.Client.Render.Model;
using NetCraft.Game.Client.Render.World;
using NetCraft.Game.Gui;
using NetCraft.Game.Gui.Screens;
using NetCraft.Game.Network;
using NetCraft.Game.World.Entity;
using NetCraft.Gpu;
using NetCraft.Gpu.Vulkan;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Resources;

namespace NetCraft.Game.Client;

//MinecraftClient 客户端主循环对应原版 net.minecraft.client.Minecraft
//持有 GameConfig 与运行状态提供 Run 与 Stop 主循环骨架
//阶段 11.35 接入循环空壳 11.46 加帧率控制 11.49 接入双模式 + Tick 四件套
//双模式有 gpuApp 走窗口驱动 Tick 挂 FrameUpdate 无 gpuApp 走 while+sleep 供测试
//S3 持 ClientLevel/LevelRenderer 世界渲染链路 ConnectServer 建立网络连接收区块显示地形
public sealed class MinecraftClient : IDisposable
{
    //TargetFps 目标每秒帧数对齐 60 FPS
    public const int TargetFps = 60;
    //TargetFrameMillis 单帧目标时长约 16.67ms
    public const int TargetFrameMillis = 1000 / TargetFps;

    private volatile bool _running;
    private long _frameCount;
    private readonly CancellationTokenSource _shutdownCts = new();
    //SleepBudgetMillis 单帧 sleep 预算测试场景设 0 加速跑测生产用默认 16ms
    private int _sleepBudgetMillis = TargetFrameMillis;
    //GpuApp 可空 null 时走 Headless 模式供服务器集成和无 GPU 测试
    private readonly VulkanGuiApp? _gpuApp;
    //Connection 可空 null 时跳过网络包处理供单机或测试场景
    private Connection? _connection;
    //ScreenManager 可空 null 时跳过屏幕逻辑供 Headless 测试
    private readonly ScreenManager? _screens;
    //ServerResources 可空 null 时跳过 Tags 等数据驱动查询供 Headless 测试
    private readonly ReloadableServerResources? _rsr;
    //ResourceManager 资源管理器供方块模型加载与纹理收集
    private readonly ResourceManager? _resourceManager;
    //Language 客户端语言表 可空 null 时没有语言切换能力供 Headless 测试
    private readonly ClientLanguage? _language;
    //ClientLevel 客户端世界装网络区块 ConnectServer 后由监听器填充
    private readonly ClientLevel _level = new();
    //Camera 世界渲染相机 Tick 按本地玩家状态更新
    private readonly Camera _camera = new();
    //世界渲染链路 swapchain 首帧就绪后创建注入 gpuApp
    private SectionRenderDispatcher? _worldDispatcher;
    private LevelRenderer? _worldRenderer;
    private bool _worldRendererInitialized;
    //Lightmap 光照贴图 Tick 按 Level.SkyBrightness 刷新 内部脏检查不重复上传
    private LightTexture? _lightTexture;

    //GameConfig 客户端配置 options.txt 加载结果
    public GameConfig Config { get; }

    //Player 本地玩家实体 HUD 和业务读取 Health/Food/Pos 等状态
    public Player Player { get; } = new();

    //GpuApp 持有的 Vulkan GUI 应用窗口驱动模式非空
    public VulkanGuiApp? GpuApp => _gpuApp;

    //Connection 持有的网络连接非空时 Tick 调其 Tick 处理入站包
    public Connection? Connection => _connection;

    //Screens 屏幕管理器非空时驱动屏幕生命周期
    public ScreenManager? Screens => _screens;

    //ServerResources 服务端可重载资源集合非空时供客户端 Tags 等数据驱动查询
    public ReloadableServerResources? ServerResources => _rsr;

    //Resources 资源管理器非空时语言选择界面切完语言调 Reload 重跑重载链
    public ResourceManager? Resources => _resourceManager;

    //Language 客户端语言表非空时语言选择界面读写当前语言码与可选清单
    public ClientLanguage? Language => _language;

    //Level 客户端世界区块数据源供诊断
    public ClientLevel Level => _level;

    //SetScreen 切换屏幕委托给 ScreenManager null 表示关闭当前屏幕
    public void SetScreen(Screen? screen) => _screens?.SetScreen(screen);

    //PushScreen 压栈当前屏幕进子菜单 Esc 可回上一级
    public void PushScreen(Screen screen) => _screens?.PushScreen(screen);

    //PopScreen 弹栈回上一级栈空关闭当前屏幕
    public void PopScreen() => _screens?.PopScreen();

    public MinecraftClient(GameConfig config, VulkanGuiApp? gpuApp = null, Connection? connection = null, ReloadableServerResources? rsr = null, ResourceManager? resourceManager = null, ClientLanguage? language = null)
    {
        Config = config;
        _gpuApp = gpuApp;
        _connection = connection;
        _rsr = rsr;
        _resourceManager = resourceManager;
        _language = language;
        //有 GpuApp 时创建 ScreenManager 接管窗口控件树
        _screens = gpuApp is not null ? new ScreenManager(this, gpuApp.Window) : null;
        if (gpuApp is not null && _screens is not null)
        {
            //订阅 swapchain 重建事件窗口 resize 时让 ScreenManager 重布局当前 Screen
            gpuApp.SwapchainRecreated += () => _screens.Resized();
            //按键与鼠标原始事件必须接到屏幕管理器 漏订阅会让 Esc/F3/数字键/Q 与鼠标点击都到不了 Screen
            gpuApp.RawKeyDown += _screens.HandleRawKeyDown;
            gpuApp.RawMouseDown += _screens.HandleRawMouseDown;
            gpuApp.RawMouseUp += _screens.HandleRawMouseUp;
            gpuApp.RawMouseMove += _screens.HandleRawMouseMove;
        }
        //S3 swapchain 就绪后构建方块图集注入 gpuApp 并创建世界渲染链路
        if (gpuApp is not null)
            gpuApp.SwapchainRecreated += EnsureWorldRenderer;
    }

    //EnsureWorldRenderer 首次 swapchain 就绪时构建方块图集与 LevelRenderer 注入 gpuApp
    //图集构建失败保持占位纹理世界渲染仍可用 mesh 纹理走占位色
    private void EnsureWorldRenderer()
    {
        if (_worldRendererInitialized) return;
        _worldRendererInitialized = true;
        if (_gpuApp is null || _resourceManager is null) return;
        try
        {
            var collector = new BlockTextureCollector(_resourceManager);
            var sprites = collector.Collect();
            BlockTextureAtlas? atlas = null;
            if (sprites.Count > 0)
            {
                atlas = new BlockTextureAtlas(_gpuApp.Device);
                if (!atlas.Build(sprites))
                {
                    atlas.Dispose();
                    atlas = null;
                    Log.Warning($"Block texture atlas has {sprites.Count} sprites exceeding the limit, build failed, falling back to placeholder");
                }
            }
            else
            {
                Log.Warning("Block texture collection is empty, skipping real atlas injection and using placeholder texture");
            }
            if (atlas is not null)
            {
                _gpuApp.SetBlockAtlas(atlas);
                _lightTexture = new LightTexture(_gpuApp.Device);
                _gpuApp.SetLightmap(_lightTexture);
                Log.Info($"Block texture atlas built sprite={sprites.Count}");
            }
            var loader = new BlockModelLoader(_resourceManager);
            var baker = new BlockModelBaker(atlas ?? PlaceholderAtlas);
            var mapper = new BlockStateModelMapper(_resourceManager, loader, baker);
            //注入光照采样器 mesh 顶点 light 属性从 ClientLevel 取区块光照
            var builder = new ChunkMeshBuilder(mapper, new ChunkLightSampler(_level));
            var pool = new GpuBufferPool((size, usage) => _gpuApp.Device.CreateHostVisibleBuffer(size, usage));
            _worldDispatcher = new SectionRenderDispatcher(_level, builder, pool, workerCount: 2);
            _worldDispatcher.Start();
            _worldRenderer = new LevelRenderer(_level, _camera, _worldDispatcher);
            //实体渲染调度器随世界渲染一起建
            //掉落物单独持一个模型映射实例 后台网格构建线程已在用同一个 mapper 缓存字典 共用会并发读写
            var entityDispatcher = new EntityRenderDispatcher(pool);
            entityDispatcher.Register(EntityTypes.ITEM.Id.ToString(),
                new ItemEntityRenderer(new BlockStateModelMapper(_resourceManager, loader, baker)));
            _worldRenderer.EntityDispatcher = entityDispatcher;
            //移动方块通道单独持一份模型映射与网格生成器 后台网格构建线程已在用同一个 mapper 缓存字典
            _worldRenderer.MovingBlocks = new MovingBlockRenderer(_level,
                new ChunkMeshBuilder(new BlockStateModelMapper(_resourceManager, loader, baker),
                    new ChunkLightSampler(_level)), pool);
            _gpuApp.SetLevelRenderer(_worldRenderer);
            Log.Info("World render pipeline created");
        }
        catch (Exception ex)
        {
            Log.Warning($"World render pipeline creation failed {ex.Message}");
        }
    }

    //PlaceholderAtlas 图集缺位时的占位实现避免 BlockModelBaker 空引用
    //sprite UV 覆盖 [0,1] 全图 mesh 采样占位纹理单色
    private static ITextureAtlas PlaceholderAtlas => new PlaceholderTextureAtlas();

    //PlaceholderTextureAtlas 占位图集所有纹理名映射到同一 16x16 sprite
    private sealed class PlaceholderTextureAtlas : ITextureAtlas
    {
        //16x16 sprite 在 16x16 图集 UV 全覆盖
        private static readonly TextureAtlasSprite s_sprite =
            new("placeholder", 0, 0, 16, 16, 16, 16, null);

        public TextureAtlasSprite? GetSprite(string name) => s_sprite;
    }

    //ConnectServer 后台线程连接服务器成功后持 Connection 走 Login->Configuration->Play
    //进入世界时切 GameScreen 网络区块经 ClientGamePacketListenerImpl 装载 ClientLevel
    public void ConnectServer(string host, int port)
    {
        Log.Info($"Connecting to server {host}:{port}");
        ClientConnector.Connect(host, port, _level, Player,
            onJoinWorld: () =>
            {
                _screens?.SetScreen(new GameScreen());
                Log.Info("Entered game, switching to GameScreen");
            },
            onConnected: conn =>
            {
                _connection = conn;
                if (conn.Listener is ClientGamePacketListenerImpl listener)
                {
                    //容器界面在网络回调里切 网络层不认识 GUI 层 行数由菜单类型决定
                    //只有箱式类型有现成界面 工作台那类界面还没做 不认识的类型不开屏免得错开成箱子
                    listener.OnOpenScreen = packet =>
                    {
                        if (ChestScreen.IsChestKind(packet.Kind))
                            _screens?.SetScreen(new ChestScreen(packet.Title, ChestScreen.RowsFor(packet.Kind)));
                    };
                    listener.OnContainerClose = () =>
                    {
                        if (_screens?.Current is ChestScreen) _screens.SetScreen(new GameScreen());
                    };
                }
            },
            onFailed: reason => Log.Error($"Failed to connect to server {reason}"));
    }

    //Running 是否在主循环中
    public bool Running => _running;

    //FrameCount 累计帧数用于诊断
    public long FrameCount => _frameCount;

    //SleepBudgetMillis 单帧 sleep 预算测试场景设 0 加速跑测生产用默认 16ms
    //仅 Headless 模式生效窗口驱动模式由 vsync 控制帧率
    public int SleepBudgetMillis
    {
        get => _sleepBudgetMillis;
        set => _sleepBudgetMillis = Math.Max(0, value);
    }

    //Run 主循环入口阻塞调用线程直到 Stop 被调用
    //有 gpuApp 时 Render 走窗口循环 Tick 由 gpuApp 内部 Tick 线程驱动 FrameTick 派发
    //无 gpuApp 时走 while+sleep 保持 60 FPS 供 Headless 测试
    public void Run()
    {
        if (_running) return;
        _running = true;
        if (_gpuApp is not null)
        {
            Log.Info($"MinecraftClient window-driven mode started render distance {Config.RenderDistance} FOV {Config.Fov}");
            _screens?.SetScreen(new TitleScreen());
            _gpuApp.FrameTick += OnFrameTick;
            try
            {
                _gpuApp.Run();
            }
            finally
            {
                _gpuApp.FrameTick -= OnFrameTick;
                _running = false;
                Log.Info($"MinecraftClient window-driven loop exited, total frames {_frameCount}");
            }
            return;
        }
        Log.Info($"MinecraftClient headless mode started render distance {Config.RenderDistance} FOV {Config.Fov}");
        var watch = Stopwatch.StartNew();
        try
        {
            while (_running)
            {
                var frameStart = watch.Elapsed;
                Tick(TargetFrameMillis / 1000.0);
                if (_sleepBudgetMillis > 0)
                {
                    var elapsed = (int)(watch.Elapsed - frameStart).TotalMilliseconds;
                    var remaining = _sleepBudgetMillis - elapsed;
                    if (remaining > 0) Thread.Sleep(remaining);
                }
            }
        }
        finally
        {
            _running = false;
            Log.Info($"MinecraftClient headless loop exited, total frames {_frameCount}");
        }
    }

    //OnFrameTick VulkanGuiApp.Tick 线程 20tps 触发本回调做业务四件套
    //阶段7 Tick/Render 解耦后业务全在 Tick 线程 SubmitFrame 由 VulkanGuiApp 自动调
    private void OnFrameTick(double delta)
    {
        Tick(delta);
    }

    //Tick 单帧逻辑四件套
    //Input.Poll 有 GPU 时从队列派发输入到 GuiWindow
    //Layout 处理 resize 后的脏标记独占控件树无竞争
    //Gui.Update 调 Window.Update 推进控件动画状态
    //Network.ProcessPackets 处理入站包区块装载 Player 同步在此发生
    //Camera 按本地 Player 状态更新世界渲染相机
    //Gpu.Render 由 VulkanGuiApp 在 FrameTick 后自动调 SubmitFrame 发布快照
    private void Tick(double delta)
    {
        _gpuApp?.PollInput();
        _screens?.ProcessLayoutIfDirty();
        _gpuApp?.Window.Update(delta);
        _screens?.Tick();
        _connection?.Tick();
        //本地世界在包处理之后推进一帧 按服务端下发的刻率折算本帧该走几刻
        //服务端冻结时它不涨刻 实体动画随之停住
        _level.Tick(delta);
        //天光亮度当前无客户端时间源 恒定正午 接入时间同步后改为按时间计算
        _lightTexture?.Update(1.0f);
        UpdateCamera();
        _frameCount++;
    }

    //UpdateCamera 按本地玩家位置朝向更新相机透视为 Config.Fov/RenderDistance
    //玩家未同步位置前默认出生点俯视角保证开局有画面
    private void UpdateCamera()
    {
        if (_worldRenderer is null) return;
        var player = Player;
        _camera.SetPosition(new Vector3((float)player.Pos.X, (float)player.Pos.Y, (float)player.Pos.Z));
        _camera.SetRotation(player.YRot, player.XRot);
        var width = _gpuApp?.Window.SurfaceWidth ?? 800;
        var height = _gpuApp?.Window.SurfaceHeight ?? 600;
        var fov = Config.Fov * MathF.PI / 180f;
        var zFar = Math.Max(64f, Config.RenderDistance * 16f * 2f);
        _camera.UpdatePerspective(fov, width, height, 0.05f, zFar);
    }

    //Stop 触发主循环退出由窗口关闭或 ShutdownHook 调用
    //窗口驱动模式下调 gpuApp.RequestClose 让 _window.Run 退出
    public void Stop()
    {
        Log.Info("MinecraftClient received stop signal");
        _running = false;
        _shutdownCts.Cancel();
        _gpuApp?.RequestClose();
    }

    public void Dispose()
    {
        if (_gpuApp is not null && _screens is not null)
        {
            _gpuApp.RawKeyDown -= _screens.HandleRawKeyDown;
            _gpuApp.RawMouseDown -= _screens.HandleRawMouseDown;
            _gpuApp.RawMouseUp -= _screens.HandleRawMouseUp;
            _gpuApp.RawMouseMove -= _screens.HandleRawMouseMove;
        }
        _shutdownCts.Dispose();
        _worldRenderer?.Dispose();
        _worldDispatcher?.Dispose();
        _connection?.Dispose();
        _gpuApp?.Dispose();
    }
}
