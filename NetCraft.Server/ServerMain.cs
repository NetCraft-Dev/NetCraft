using System.IO;
using System.Threading;
using NetCraft;
using NetCraft.Config;
using NetCraft.DataFixer;
using NetCraft.Game.Bootstrap;
using NetCraft.Game.DFU;
using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Dimension;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Registry.Environment;
using NetCraft.Resources;
using NetCraft.Server.Diagnostics;
using NetCraft.Server.Gui;
using NetCraft.Server.ServerConsole;
using NetCraft.Storage;
using NetCraft.Util.Random;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;
using GameConfiguredWorldCarver = NetCraft.Game.World.Level.LevelGen.Carver.ConfiguredWorldCarver;
//注册表与关卡定义各有一个 DimensionType 前者是标记接口 这里固定指 Game 层的真实类型
using GameDimensionType = NetCraft.Game.World.Level.LevelGen.Dimension.DimensionType;

namespace NetCraft.Game;

//ServerMain 服务端主入口
//对应原版 net.minecraft.server.Main
//串联 内核初始化 + 启动参数解析 + 服务端业务调度
//本类自身即 EXE 入口 也可被 NetCraft.Loader 引用后分发调用
public static class ServerMain
{
    private static int _started;

    //Run 服务端启动主函数
    //进程入口在 NetCraft.Server.Exe 里 模组引导也在那边做 这里只负责启动流程本身
    //args 命令行参数 内核识别的消费未识别的通过事件传给 GameOptions
    public static void Run(string[] args)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            Log.Warning("ServerMain already started, ignoring duplicate call");
            //Log.Debug("Run 出口");
            return;
        }
        Log.Debug($"Run entry args={string.Join(",", args)}");

        Log.SetClassSource(typeof(ServerMain));
        Log.Info("NetCraft server starting");

        //GUI 默认启用 --nogui 与裸 nogui 都能关 对齐原版 Main 对两种写法的判定
        //nogui 声明成内核 flag 让 LaunchOptions 吞掉 免得漏进 GameOptions 当成未知参数
        LaunchOptions.DeclareKernelFlag("nogui");
        //noconsole 关掉终端命令行 对应 Paper 的 --noconsole 与 --nojline 给脚本与 CI 用
        LaunchOptions.DeclareKernelFlag("noconsole");
        //potato 彩蛋开关 同样交给内核吞掉 免得被 GameOptions 当成未知参数
        LaunchOptions.DeclareKernelFlag("potato");
        var useGui = Array.IndexOf(args, "--nogui") < 0 && Array.IndexOf(args, "nogui") < 0;
        var useConsole = Array.IndexOf(args, "--noconsole") < 0;

        //1. 创建 GameOptions 订阅内核未识别参数事件
        var options = new GameOptions();
        options.Subscribe();

        //2. 初始化内核触发 LaunchOptions.Parse
        NetCraftKernel.Initialize(args);

        //3. 解析剩余挂起参数
        options.FlushPending();

        //4. 设置 --output-dir 覆盖基准未传则用 AppContext.BaseDirectory
        //   下游统一从 AppPaths 取避免相对路径被解释为运行时工作目录
        AppPaths.SetOverride(options.GetOptionOrDefault("output-dir", string.Empty));

        //4.1 加载 server.properties 不存在则生成默认
        //    端口 max-players 难度 正版验证 PVP 视野距离等
        //    只依赖 AppPaths 不依赖资源 提前到这里是为了让后面全程知道该说哪国话
        var settings = ServerSettings.LoadOrGenerate(AppPaths.ServerPropertiesPath);

        //4.2 按配置里的语言码装语言表 配了不支持的语言码时退回 en_us
        //    语言文件已由内核解压到根目录 lang/ 内核那次装的是默认码 这一步才把它定死
        NcLanguage.Load(settings.NcLanguage);
        Log.Info($"Server settings port {settings.ServerPort} level {settings.LevelName} gamemode {settings.Gamemode} difficulty {settings.Difficulty} language {settings.NcLanguage}");

        //5. 提取 jar 资源到 assets/ 与 data/ 同时把 pack.mcmeta 复制到根目录
        //   与 ClientMain 步骤 5 对齐让服务端也能加载原版资源与数据驱动内容
        //   必须早于 BootstrapClass.BootStrap 因后者会 Freeze 注册表 冻结后无法再写入
        AssetsExtractor.Extract(options);

        //6. 构造 ResourceManager 加载 vanilla pack
        //   vanilla pack 以 BaseDirectory 为根读 assets/ 与 data/ 子树与 pack.mcmeta
        var registryAccess = BuiltInRegistries.CreateRegistryAccess();
        var resourceManager = new ResourceManager();
        resourceManager.AddPack(new Pack(
            id: Identifier.WithDefaultNamespace("vanilla"),
            title: "Minecraft",
            description: "The default data for Minecraft",
            priority: 0,
            isBuiltin: true,
            resources: new FolderPackResources("vanilla", AppPaths.BaseDirectory)));

        //6.1 挂上资源包后再装一次 这次能一并取到原版译名
        //    NC 自有文案仍是根目录 lang/ 那批 两边合进同一张表
        LanguageTable.Load(resourceManager, settings.NcLanguage);

        //7. Game 层引导前半段 必须在数据驱动加载之前
        //   密度函数类型表/方块是元素 JSON 的 codec 依赖 缺了会整批解码失败
        //   环境属性表也要先注册 biome 的 attributes 字段按键查这张表
        DataComponents.Bootstrap();
        GameBootstrap.BootstrapBeforeDataLoad();
        EnvironmentAttributes.RegisterAll();

        //8. 数据驱动加载注册表元素 必须早于 Freeze
        //   有 jar 时群系/密度函数/噪声/噪声设置取 data/minecraft/worldgen/ 下的原版真值
        //   无 jar 时加载 0 项 由第 9 步 Noises.Bootstrap 与 NoiseGeneratorSettings.Overworld 兜底
        //   worldgen 注册表在 Registries 侧是 object 键 用 Boxed 把强类型 codec 解出的元素装箱装载
        //   元素之间互相引用 加载器多轮重试直到引用目标就位
        var registryLoad = RegistryDataLoader.Load(resourceManager, registryAccess, new RegistryData[]
        {
            new RegistryData<NoiseParameters>(
                (WritableRegistry<NoiseParameters>)BuiltInRegistries.NOISE, NoiseParameters.Codec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.DENSITY_FUNCTION, DensityFunctionCodec.Instance),
            //configured_carver 必须排在 biome 之前 群系的 carvers 字段解析时要按注册名查它们
            new RegistryData<ConfiguredWorldCarver>(
                (WritableRegistry<ConfiguredWorldCarver>)BuiltInRegistries.CONFIGURED_CARVER,
                GameConfiguredWorldCarver.ElementCodec),
            //configured_feature / placed_feature 同样要排在 biome 之前 群系的 features 字段要按注册名查它们
            new RegistryData<NetCraft.Registry.ConfiguredFeature>(
                (WritableRegistry<NetCraft.Registry.ConfiguredFeature>)BuiltInRegistries.CONFIGURED_FEATURE,
                NetCraft.Game.World.Level.LevelGen.Features.ConfiguredFeature.ElementCodec),
            new RegistryData<NetCraft.Registry.PlacedFeature>(
                (WritableRegistry<NetCraft.Registry.PlacedFeature>)BuiltInRegistries.PLACED_FEATURE,
                NetCraft.Game.World.Level.LevelGen.Placement.PlacedFeature.ElementCodec),
            new RegistryData<Biome>((WritableRegistry<Biome>)BuiltInRegistries.BIOME, Biome.DirectCodec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST, MultiNoiseBiomeSourceParameterList.Codec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.NOISE_SETTINGS, NoiseGeneratorSettings.Codec),
            //dimension_type 维度类型 决定各维度的高度范围/坐标缩放/天光与天花板
            new RegistryData<NetCraft.Registry.DimensionType>(
                (WritableRegistry<NetCraft.Registry.DimensionType>)BuiltInRegistries.DIMENSION_TYPE,
                GameDimensionType.ElementCodec),
            //structure 系列四类 顺序 processor_list → template_pool → structure → structure_set
            //模板池的元素要按注册名查处理器列表 结构要查模板池 结构集合最后引用结构
            new RegistryData<NetCraft.Registry.StructureProcessorList>(
                (WritableRegistry<NetCraft.Registry.StructureProcessorList>)BuiltInRegistries.PROCESSOR_LIST,
                NetCraft.Game.World.Level.LevelGen.Structure.StructureProcessorList.ElementCodec),
            new RegistryData<NetCraft.Registry.StructureTemplatePool>(
                (WritableRegistry<NetCraft.Registry.StructureTemplatePool>)BuiltInRegistries.TEMPLATE_POOL,
                NetCraft.Game.World.Level.LevelGen.Structure.StructureTemplatePool.ElementCodec),
            new RegistryData<NetCraft.Registry.Structure>(
                (WritableRegistry<NetCraft.Registry.Structure>)BuiltInRegistries.STRUCTURE,
                NetCraft.Game.World.Level.LevelGen.Structure.StructureCodecs.ElementCodec),
            new RegistryData<NetCraft.Registry.StructureSet>(
                (WritableRegistry<NetCraft.Registry.StructureSet>)BuiltInRegistries.STRUCTURE_SET,
                NetCraft.Game.World.Level.LevelGen.Structure.StructureSetCodecs.ElementCodec),
        });
        Log.Info($"Registry data loaded: {registryLoad.LoadedCount} elements, {registryLoad.Errors.Count} errors");
        foreach (var error in registryLoad.Errors) Log.Warning($"Registry element failed to load: {error}");

        //8.1 维度类型兜底与关卡定义装载
        //    dimension_type 已随第 8 步数据驱动装载 无数据包时用内置常量补齐三个必需维度
        //    level_stem 没有独立目录 内嵌在 worldgen/world_preset/<预设>.json 的 dimensions 段
        DimensionTypes.RegisterBuiltin();
        var loadedDimensions = WorldPresets.Load(resourceManager, registryAccess, WorldPresets.Normal);
        Log.Info(loadedDimensions.Count == 0
            ? "Level definitions not loaded (resource pack lacks world_preset), dimension setup falls back to built-ins"
            : $"Level definitions loaded: {loadedDimensions.Count} dimensions {string.Join(",", loadedDimensions)}");

        //9. Game 层引导后半段与内置注册表 freeze
        //   Noises.Bootstrap 对已被第 8 步数据驱动填充的键跳过 保证 JSON 里的原版真值优先
        //   必须在 BootstrapClass.BootStrap 之前完成因 BootStrap 会 Freeze 所有注册表
        GameBootstrap.BootstrapAfterDataLoad();
        //9.1 结构模板管理器注入 必须在结构系列装载之后 否则拿不到已装载的 jigsaw 结构实例
        GameBootstrap.InjectStructureTemplates(resourceManager);
        BootstrapClass.BootStrap();

        //10. 触发 Tags 等数据驱动重载
        //    必须在 BootstrapClass.BootStrap 之后因 BindAll 要求 Registry 已 Freeze
        //    LoadResources 注册 TagsReloadListener 调 LoadBuiltinTags+BindAll
        var rsr = ReloadableServerResources.LoadResources(resourceManager, registryAccess);
        Log.Info($"Server resources loaded: {rsr.Listeners.Count} listeners, {resourceManager.Packs.Count} packs");

        //12. 初始化世界存储
        //   参考原生 LevelStorageSource.createWorldStorage 加载 anvil 区域文件
        //   NetCraft.Storage 已实现 LevelStorage 包装 RegionFileStorage 提供世界存储入口
        var levelStorage = new LevelStorage(AppPaths.WorldsDir);
        var levelAccess = levelStorage.CreateAccess(settings.LevelName);
        Log.Info($"World storage ready {levelAccess.WorldDir}");

        //读 level.dat 存在则世界元数据从存档恢复种子优先取 world_gen_settings.dat
        //新世界种子来自 server.properties 的 level-seed 首次落盘后固化
        //level.dat 损坏时拒绝启动 静默当新世界会在 initServer 固化时把旧存档覆盖掉
        LevelData? levelData;
        try
        {
            levelData = LevelData.Load(levelAccess.WorldDir);
        }
        catch (InvalidDataException e)
        {
            Log.Error($"level.dat of level {settings.LevelName} cannot be read, refusing to start as a new world to avoid overwriting the save: {e.Message}");
            Log.Error($"Repair or move {levelAccess.WorldDir} then start again");
            return;
        }
        long seed;
        if (levelData is not null)
        {
            seed = WorldGenSettingsData.ReadSeed(levelAccess.WorldDir) ?? ParseLevelSeed(settings.LevelSeed);
            Log.Info($"Existing world restored: seed {seed} game time {levelData.GameTime} spawn ({levelData.SpawnX},{levelData.SpawnY},{levelData.SpawnZ})");
        }
        else
        {
            seed = ParseLevelSeed(settings.LevelSeed);
        }

        //13. 构建 DataFixer 与 DedicatedServer 实例并启动主循环
        //   GameDataFixers.BuildV1_21Fixer 注册 13 个 Schema 与 18 个 Fix 覆盖 1.20.2 到 1.21.4 升级链
        //   DedicatedServer 内部创建 OVERWORLD 维度 SimpleRegionStorage 与 PersistentServerLevel 接入存档
        //   接入 NoiseBasedChunkGenerator + MultiNoiseBiomeSource 让世界生成子系统真正被使用
        //   噪声设置优先取第 8 步数据驱动的 minecraft:overworld 无 jar 时回退硬编码
        //   rsr 持有 ResourceManager 与 Tags 供后续 /reload 命令重载
        //   server.Run 阻塞当前线程直到 server.Stop 被调用 Stop 时强制刷盘
        var dataFixer = GameDataFixers.BuildV1_21Fixer();
        var random = RandomSource.Create(seed);
        //按关卡定义装配主世界生成器 数据包缺 world_preset 时退回硬编码兜底
        //噪声设置与群系参数表都优先取第 8 步数据驱动的真值
        var overworldStem = WorldPresets.Get(LevelKeys.OVERWORLD.Identifier);
        var chunkGenerator = overworldStem?.Generator ?? BuildFallbackOverworldGenerator();
        Log.Info(overworldStem is null
            ? "Overworld generator uses hardcoded fallback (level definitions not loaded)"
            : "Overworld generator comes from level definition minecraft:overworld");

        //GUI 模式服务端主循环要跑在后台线程(主线程留给 Avalonia 消息循环)
        //先把线程对象交给 DedicatedServer 登记 循环体等 server 建好再启动
        DedicatedServer? guiServer = null;
        var serverThread = useGui
            ? new Thread(() => { guiServer!.InitServer(); guiServer.Run(); })
            { Name = "NetCraft-Server", IsBackground = true }
            : null;

        //各维度的维度类型与关卡定义都来自数据包 由 DedicatedServer 按注册表逐个建世界
        using var server = new DedicatedServer(
            serverThread ?? Thread.CurrentThread,
            settings,
            levelAccess,
            dataFixer,
            chunkGenerator: chunkGenerator,
            random: random,
            rsr: rsr,
            worldSeed: seed,
            levelData: levelData);

        //BuildFallbackOverworldGenerator 无数据包时的主世界生成器
        //与数据驱动路径等价 只是噪声设置与参数表取硬编码常量
        static ChunkGenerator BuildFallbackOverworldGenerator()
        {
            var loadedSettings = BuiltInRegistries.NOISE_SETTINGS
                .GetValue(Identifier.WithDefaultNamespace("overworld")) as NoiseGeneratorSettings;
            var noiseSettings = loadedSettings ?? NoiseGeneratorSettings.Overworld();
            var parameterList = BuiltInRegistries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST
                .GetValue(Identifier.WithDefaultNamespace("overworld")) as MultiNoiseBiomeSourceParameterList;
            var biomeSource = parameterList is not null
                ? new MultiNoiseBiomeSource(parameterList)
                : new MultiNoiseBiomeSource();
            return new NoiseBasedChunkGenerator(biomeSource, noiseSettings);
        }

        //13.1 订阅运行时 GC 与 JIT 事件 内存图基线红紫点与调试日志都从这来
        //启动期的 GC 也要看得见所以尽早订阅 起不来只影响打点不会牵连服务端
        GcEventMonitor.Start();
        JitEventMonitor.Start();

        //注册 Ctrl+C 触发优雅关闭 GUI 模式下窗口会随后自行关闭
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Stop();
        };

        if (useGui)
        {
            //14.1 有 GUI：服务端主循环放后台线程 主线程交给 Avalonia 直到窗口关闭
            //终端那一路命令行两种模式都留着 对应原版开 GUI 也保留控制台命令输入
            Log.Info("Server GUI enabled, closing the window stops the server; pass --nogui or nogui to run headless");
            using ReplConsole? repl = useConsole ? ReplConsole.Start(server) : null;
            guiServer = server;
            serverThread!.Start();
            ServerGuiHost.Run(server, args);
        }
        else
        {
            //14.2 无 GUI：沿用原路径 主线程直接跑 对应原版 runServer 先 initServer 再 tick
            //命令行要在 Run 阻塞主线程之前起来 它自己开后台线程读键
            //输入或输出被重定向时它只挂一条读行线程 脚本与管道喂命令走的就是那条
            using ReplConsole? repl = useConsole ? ReplConsole.Start(server) : null;
            server.InitServer();
            server.Run();
        }

        //15. 清理退出
        GcEventMonitor.Stop();
        options.Unsubscribe();
        Log.Info("NetCraft server stopped");
        //Log.Debug("Run 出口");
    }

    //WaitForServer 阻塞调用线程直到服务端关闭
    //外部 EXE 用此方法保持进程不退出
    public static void WaitForServer(CancellationToken cancellationToken = default)
    {
        Log.Debug($"WaitForServer entry cancellationToken={cancellationToken}");
        try
        {
            cancellationToken.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            //正常退出
        }
        //Log.Debug("WaitForServer 出口");
    }

    //ParseLevelSeed 解析 server.properties 中的 level-seed 字段
    //空或非数字返回随机 long 数字返回 long.Parse 结果对齐原版 seed 语义
    private static long ParseLevelSeed(string? seedStr)
    {
        if (string.IsNullOrWhiteSpace(seedStr)) return RandomSupport.GenerateUniqueSeed();
        if (long.TryParse(seedStr, out var seed)) return seed;
        Log.Warning($"level-seed is not a number {seedStr}, using a random seed");
        return RandomSupport.GenerateUniqueSeed();
    }
}
