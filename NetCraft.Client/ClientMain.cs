using System.Threading;
using NetCraft;
using NetCraft.Game.Bootstrap;
using NetCraft.Game.Client;
using NetCraft.Game.Client.Language;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Dimension;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Gpu;
using NetCraft.Gpu.Vulkan;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Registry.Environment;
using NetCraft.Resources;
using NetCraft.Util;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;
using GameConfiguredWorldCarver = NetCraft.Game.World.Level.LevelGen.Carver.ConfiguredWorldCarver;
//注册表与关卡定义各有一个 DimensionType 前者是标记接口 这里固定指 Game 层的真实类型
using GameDimensionType = NetCraft.Game.World.Level.LevelGen.Dimension.DimensionType;

namespace NetCraft.Game; 

//ClientMain 客户端主入口
//对应原版 net.minecraft.client.main.Main
//串联 内核初始化 + 启动参数解析 + 素材提取 + 客户端业务调度
//本类自身即 EXE 入口 也可被 NetCraft.Loader 引用后分发调用
public static class ClientMain
{
    private static int _started;

    //Run 客户端启动主函数
    //进程入口在 NetCraft.ClientExe 里 模组引导也在那边做 这里只负责启动流程本身
    //args 命令行参数 内核识别的消费未识别的通过事件传给 GameOptions
    public static void Run(string[] args)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            Log.Warning("ClientMain already started, ignoring duplicate call");
            //Log.Debug("Run 出口");
            return;
        }
        Log.Debug($"Run entry args={string.Join(",", args)}");

        Log.SetClassSource(typeof(ClientMain));
        //崩溃报告文件名里的角色段 与内核共用的处理器靠它区分客户端与服务端
        CrashHandler.Role = "client";
        Log.Info("NetCraft client starting");

        //1. 创建 GameOptions 订阅内核未识别参数事件
        //   必须在 NetCraftKernel.Initialize 之前订阅才能收到事件
        var options = new GameOptions();
        options.Subscribe();

        //2. 初始化内核（触发 LaunchOptions.Parse 内核识别的消费未识别的发出事件）
        NetCraftKernel.Initialize(args);

        //3. 解析剩余挂起的 --opt 参数若无后续 value 降级为 flag
        options.FlushPending();

        //4. 设置 --output-dir 覆盖基准未传则用 AppContext.BaseDirectory
        //   下游统一从 AppPaths 取避免相对路径被解释为运行时工作目录
        AppPaths.SetOverride(options.GetOptionOrDefault("output-dir", string.Empty));

        //5. 提取 jar 和音频素材到固定目录
        //   核心业务后续直接从 assets 和 assets/sounds 加载不依赖本步骤
        //   同时把 pack.mcmeta 复制到根目录与 assets/data 同级
        //   必须早于 BootStrap 因后者会 Freeze 注册表 冻结后数据驱动加载无处可写
        AssetsExtractor.Extract(options);

        //5.1 补齐语言素材
        //    jar 只带 en_us 其余语言在资源对象存储里 按资源索引落到 assets/minecraft/lang 与根 pack.mcmeta
        //    必须早于 ResourceManager 构造 文件夹资源包构造时会一次性枚举目录内的文件
        LanguageAssets.Prepare(
            options.GetOptionOrDefault("assets-dir", string.Empty),
            options.GetOptionOrDefault("asset-index", string.Empty));

        //6. Game 层引导前半段 必须在数据驱动加载之前
        //   密度函数类型表/方块是元素 JSON 的 codec 依赖 缺了会整批解码失败
        //   环境属性表也要先注册 biome 的 attributes 字段按键查这张表
        DataComponents.Bootstrap();
        GameBootstrap.BootstrapBeforeDataLoad();
        EnvironmentAttributes.RegisterAll();

        //7. 构造 ResourceManager 并数据驱动加载注册表元素 与服务端保持一致
        //   客户端本地也需要原版真值的群系/密度函数/噪声/噪声设置 否则单人世界地形与服务端不同
        var registryAccess = BuiltInRegistries.CreateRegistryAccess();
        var resourceManager = new ResourceManager();
        resourceManager.AddPack(new Pack(
            id: Identifier.WithDefaultNamespace("vanilla"),
            title: "Minecraft",
            description: "The default data for Minecraft",
            priority: 0,
            isBuiltin: true,
            resources: new FolderPackResources("vanilla", AppPaths.BaseDirectory)));
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
            //dimension_type 维度类型 客户端渲染要按维度的天光/天花板/天空盒取参数
            new RegistryData<NetCraft.Registry.DimensionType>(
                (WritableRegistry<NetCraft.Registry.DimensionType>)BuiltInRegistries.DIMENSION_TYPE,
                GameDimensionType.ElementCodec),
            //structure 系列四类 顺序与 ServerMain 一致 客户端只需按注册名解析出结构集合用于同步
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
        Log.Info($"Client registry data loaded: {registryLoad.LoadedCount} elements, {registryLoad.Errors.Count} errors");
        foreach (var error in registryLoad.Errors) Log.Warning($"Registry element failed to load: {error}");
        //维度类型兜底 无数据包时用内置常量补齐 客户端不装载关卡定义(不需要区块生成器)
        DimensionTypes.RegisterBuiltin();

        //8. Game 层引导后半段与内置注册表 freeze
        GameBootstrap.BootstrapAfterDataLoad();
        BootstrapClass.BootStrap();

        //9. 加载客户端配置 options.txt
        //   demo 模式 fullscreen 渲染距离 FOV gamma 等字段从 options.txt 读取
        //   文件不存在返回默认配置不抛
        //   早于资源重载 语言表挂重载链时要按这里的语言码装配
        var gameConfig = GameConfig.Load(AppPaths.OptionsPath);
        if (options.HasFlag("demo")) gameConfig.Demo = true;
        if (options.HasFlag("fullscreen")) gameConfig.Fullscreen = true;
        Log.Info($"Client config loaded render distance {gameConfig.RenderDistance} FOV {gameConfig.Fov} language {gameConfig.Language}");

        //9.1 按客户端语言码装一次表 配了不支持的语言码时退回 en_us
        //    第 10 步的资源重载会再装一次 那次一并带上资源包里的原版译名
        NcLanguage.Load(gameConfig.Language);

        //10. 触发 Tags 与语言表等数据驱动重载
        //   客户端同样需要 Tags 用于物品/方块标签查询（如工具等级判断）
        //   必须在 BootstrapClass.BootStrap 之后因 BindAll 要求 Registry 已 Freeze
        //   语言表作为附加监听器挂进来 切语言改 LanguageCode 再走一次重载即可换表
        var clientLanguage = new ClientLanguage(gameConfig.Language);
        var rsr = ReloadableServerResources.LoadResources(resourceManager, registryAccess,
            new PreparableReloadListener[] { clientLanguage });
        Log.Info($"Client resources loaded: {rsr.Listeners.Count} listeners, {resourceManager.Packs.Count} packs, {clientLanguage.AvailableLanguages.Count} languages");

        //11. 创建 MinecraftClient 实例并启动主循环
        //   阶段 11.49 传入 VulkanGuiApp 走窗口驱动模式
        //   MinecraftClient 持有 gpuApp 所有权 Dispose 时释放
        //   rsr 传入供后续客户端 Tags 查询或 /reload 重载
        //   resourceManager 传入供 swapchain 就绪后构建方块图集与世界渲染链路
        //   S3 纹理注入与世界渲染接线由 MinecraftClient.EnsureWorldRenderer 统一处理
        var gpuApp = new VulkanGuiApp(gameConfig.EnableVsync, 800, 600);
        // minecraft.Run 阻塞当前线程直到 minecraft.Stop 被调用
        using var minecraft = new MinecraftClient(gameConfig, gpuApp, rsr: rsr, resourceManager: resourceManager,
            language: clientLanguage);

        //注册 Ctrl+C 触发优雅关闭
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            minecraft.Stop();
        };

        minecraft.Run();

        //12. 清理退出
        options.Unsubscribe();
        Log.Info("NetCraft client stopped");
        //Log.Debug("Run 出口");
    }
}
