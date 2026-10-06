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

//MinecraftClient client main loop, maps to vanilla net.minecraft.client.Minecraft
//Holds GameConfig and runtime state, providing the Run and Stop main loop skeleton
//Phase 11.35 added the empty loop shell, 11.46 added frame rate control, 11.49 added dual modes + the Tick quartet
//Dual mode: with gpuApp it runs window-driven with Tick on FrameUpdate; without gpuApp it runs while+sleep for tests
//S3 holds the ClientLevel/LevelRenderer world render chain; ConnectServer establishes a network connection, receives chunks, and displays terrain
public sealed class MinecraftClient : IDisposable
{
    //TargetFps target frames per second, aligned to 60 FPS
    public const int TargetFps = 60;
    //TargetFrameMillis target per-frame duration, about 16.67ms
    public const int TargetFrameMillis = 1000 / TargetFps;

    private volatile bool _running;
    private long _frameCount;
    private readonly CancellationTokenSource _shutdownCts = new();
    //SleepBudgetMillis per-frame sleep budget; tests set 0 to speed up, production uses the 16ms default
    private int _sleepBudgetMillis = TargetFrameMillis;
    //GpuApp nullable; null runs Headless mode for server integration and GPU-less tests
    private readonly VulkanGuiApp? _gpuApp;
    //Connection nullable; null skips network packet processing for singleplayer or test scenarios
    private Connection? _connection;
    //ScreenManager nullable; null skips screen logic for Headless tests
    private readonly ScreenManager? _screens;
    //ServerResources nullable; null skips data-driven queries such as Tags for Headless tests
    private readonly ReloadableServerResources? _rsr;
    //ResourceManager resource manager for block model loading and texture collection
    private readonly ResourceManager? _resourceManager;
    //Language client language table; nullable, null means no language switching, for Headless tests
    private readonly ClientLanguage? _language;
    //ClientLevel client world holding network chunks, filled by the listener after ConnectServer
    private readonly ClientLevel _level = new();
    //Camera world render camera, updated each Tick from the local player state
    private readonly Camera _camera = new();
    //World render chain created once the swapchain is ready on the first frame and injected into gpuApp
    private SectionRenderDispatcher? _worldDispatcher;
    private LevelRenderer? _worldRenderer;
    private bool _worldRendererInitialized;
    //Lightmap light texture refreshed each Tick from Level.SkyBrightness; internal dirtiness check avoids re-upload
    private LightTexture? _lightTexture;

    //GameConfig client config, the options.txt load result
    public GameConfig Config { get; }

    //Player local player entity; HUD and game logic read Health/Food/Pos etc.
    public Player Player { get; } = new();

    //GpuApp held Vulkan GUI app, non-null in window-driven mode
    public VulkanGuiApp? GpuApp => _gpuApp;

    //Connection held network connection; when non-null, Tick calls its Tick to process inbound packets
    public Connection? Connection => _connection;

    //Screens screen manager; when non-null it drives the screen lifecycle
    public ScreenManager? Screens => _screens;

    //ServerResources server reloadable resource set; when non-null it serves client data-driven queries such as Tags
    public ReloadableServerResources? ServerResources => _rsr;

    //Resources resource manager; when non-null, the language screen calls Reload after switching language to rerun the reload chain
    public ResourceManager? Resources => _resourceManager;

    //Language client language table; when non-null, the language screen reads/writes the current code and available list
    public ClientLanguage? Language => _language;

    //Level client world chunk data source, for diagnostics
    public ClientLevel Level => _level;

    //SetScreen switches screens, delegated to ScreenManager; null means close the current screen
    public void SetScreen(Screen? screen) => _screens?.SetScreen(screen);

    //PushScreen pushes the current screen onto the stack for submenus; Esc pops back
    public void PushScreen(Screen screen) => _screens?.PushScreen(screen);

    //PopScreen pops back to the previous level; if empty, closes the current screen
    public void PopScreen() => _screens?.PopScreen();

    public MinecraftClient(GameConfig config, VulkanGuiApp? gpuApp = null, Connection? connection = null, ReloadableServerResources? rsr = null, ResourceManager? resourceManager = null, ClientLanguage? language = null)
    {
        Config = config;
        _gpuApp = gpuApp;
        _connection = connection;
        _rsr = rsr;
        _resourceManager = resourceManager;
        _language = language;
        //With a GpuApp, create a ScreenManager to own the window control tree
        _screens = gpuApp is not null ? new ScreenManager(this, gpuApp.Window) : null;
        if (gpuApp is not null && _screens is not null)
        {
            //Subscribe to the swapchain-recreated event so on window resize ScreenManager re-layouts the current Screen
            gpuApp.SwapchainRecreated += () => _screens.Resized();
            //Raw key and mouse events must reach the screen manager; a missed subscription would prevent Esc/F3/number keys/Q and mouse clicks from reaching a Screen
            gpuApp.RawKeyDown += _screens.HandleRawKeyDown;
            gpuApp.RawMouseDown += _screens.HandleRawMouseDown;
            gpuApp.RawMouseUp += _screens.HandleRawMouseUp;
            gpuApp.RawMouseMove += _screens.HandleRawMouseMove;
        }
        //S3 once the swapchain is ready, build the block atlas into gpuApp and create the world render chain
        if (gpuApp is not null)
            gpuApp.SwapchainRecreated += EnsureWorldRenderer;
    }

    //EnsureWorldRenderer builds the block atlas and LevelRenderer into gpuApp the first time the swapchain is ready
    //If atlas building fails, the placeholder texture remains and world rendering still works; mesh textures use the placeholder color
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
            //Inject the light sampler; mesh vertex light attributes fetch chunk light from ClientLevel
            var builder = new ChunkMeshBuilder(mapper, new ChunkLightSampler(_level));
            var pool = new GpuBufferPool((size, usage) => _gpuApp.Device.CreateHostVisibleBuffer(size, usage));
            _worldDispatcher = new SectionRenderDispatcher(_level, builder, pool, workerCount: 2);
            _worldDispatcher.Start();
            _worldRenderer = new LevelRenderer(_level, _camera, _worldDispatcher);
            //The entity render dispatcher is created along with world rendering
            //Dropped items hold their own model mapper instance; the background mesh-building thread already uses that mapper's cache dictionary, and sharing it would cause concurrent reads/writes
            var entityDispatcher = new EntityRenderDispatcher(pool);
            entityDispatcher.Register(EntityTypes.ITEM.Id.ToString(),
                new ItemEntityRenderer(new BlockStateModelMapper(_resourceManager, loader, baker)));
            _worldRenderer.EntityDispatcher = entityDispatcher;
            //The moving block pass holds its own model mapper and mesh builder; the background mesh-building thread already uses that mapper's cache dictionary
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

    //PlaceholderAtlas placeholder used when the atlas is absent, avoiding a null reference in BlockModelBaker
    //sprite UV covers the whole [0,1] image; the mesh samples the placeholder texture as a solid color
    private static ITextureAtlas PlaceholderAtlas => new PlaceholderTextureAtlas();

    //PlaceholderTextureAtlas placeholder atlas mapping all texture names to the same 16x16 sprite
    private sealed class PlaceholderTextureAtlas : ITextureAtlas
    {
        //16x16 sprite covers the full UV of a 16x16 atlas
        private static readonly TextureAtlasSprite s_sprite =
            new("placeholder", 0, 0, 16, 16, 16, 16, null);

        public TextureAtlasSprite? GetSprite(string name) => s_sprite;
    }

    //ConnectServer: a background thread connects to the server; on success it holds the Connection and goes Login->Configuration->Play
    //On entering the world it switches to GameScreen; network chunks load into ClientLevel via ClientGamePacketListenerImpl
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
                    //The container screen is switched in a network callback; the network layer does not know the GUI layer, and the row count is decided by the menu type
                    //Only chest types have a ready screen; crafting-table-style screens are not done yet, so unrecognized types do not open a screen to avoid wrongly opening a chest
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

    //Running whether currently in the main loop
    public bool Running => _running;

    //FrameCount accumulated frame count, for diagnostics
    public long FrameCount => _frameCount;

    //SleepBudgetMillis per-frame sleep budget; tests set 0 to speed up, production uses the 16ms default
    //Only effective in Headless mode; window-driven mode controls frame rate via vsync
    public int SleepBudgetMillis
    {
        get => _sleepBudgetMillis;
        set => _sleepBudgetMillis = Math.Max(0, value);
    }

    //Run main loop entry; blocks the calling thread until Stop is called
    //With gpuApp, Render goes through the window loop and Tick is dispatched by FrameTick from gpuApp's internal Tick thread
    //Without gpuApp it runs while+sleep at 60 FPS for Headless tests
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

    //OnFrameTick VulkanGuiApp's Tick thread triggers this callback at 20tps to run the game's Tick quartet
    //After phase 7 decoupled Tick/Render, all game logic is on the Tick thread; SubmitFrame is called automatically by VulkanGuiApp
    private void OnFrameTick(double delta)
    {
        Tick(delta);
    }

    //Tick single-frame logic quartet
    //Input.Poll dispatches input from the queue to GuiWindow when there is a GPU
    //Layout handles the post-resize dirty flag, exclusively owning the control tree with no contention
    //Gui.Update calls Window.Update to advance control animation state
    //Network.ProcessPackets processes inbound packets; chunk loading and Player sync happen here
    //Camera updates the world render camera from the local Player state
    //Gpu.Render: VulkanGuiApp automatically calls SubmitFrame after FrameTick to publish the snapshot
    private void Tick(double delta)
    {
        _gpuApp?.PollInput();
        _screens?.ProcessLayoutIfDirty();
        _gpuApp?.Window.Update(delta);
        _screens?.Tick();
        _connection?.Tick();
        //The local world advances one frame after packet processing, converting this frame to a number of ticks by the server-sent tick rate
        //When the server is frozen it does not advance ticks, so entity animation stops
        _level.Tick(delta);
        //Sky brightness currently has no client time source, constant noon; it will be computed from time once time sync is added
        _lightTexture?.Update(1.0f);
        UpdateCamera();
        _frameCount++;
    }

    //UpdateCamera updates the camera from the local player position/orientation and the perspective from Config.Fov/RenderDistance
    //Before the player position is synced, uses a default spawn-point top-down view so there is something on screen at start
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

    //Stop triggers main loop exit, called by window close or a ShutdownHook
    //In window-driven mode calls gpuApp.RequestClose so _window.Run exits
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
