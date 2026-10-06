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
//The registry and level definitions each have their own DimensionType; the former is a marker interface, so this always refers to the real Game-layer type
using GameDimensionType = NetCraft.Game.World.Level.LevelGen.Dimension.DimensionType;

namespace NetCraft.Game; 

//ClientMain client main entry
//Maps to vanilla net.minecraft.client.main.Main
//Chains kernel initialization + launch argument parsing + asset extraction + client game logic scheduling
//This class is itself the EXE entry, and can also be referenced by NetCraft.Loader and dispatched to
public static class ClientMain
{
    private static int _started;

    //Run client startup main function
    //The process entry is in NetCraft.ClientExe and mod bootstrap happens there too; this only handles the startup flow itself
    //args command-line arguments; kernel-recognized ones are consumed, unhandled ones are passed to GameOptions via events
    public static void Run(string[] args)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            Log.Warning("ClientMain already started, ignoring duplicate call");
            //Log.Debug("Run exit");
            return;
        }
        Log.Debug($"Run entry args={string.Join(",", args)}");

        Log.SetClassSource(typeof(ClientMain));
        //The role segment in the crash report file name; the handler shared with the kernel uses it to tell client from server
        CrashHandler.Role = "client";
        Log.Info("NetCraft client starting");

        //1. Create GameOptions and subscribe to the kernel's unhandled-argument event
        //   Must subscribe before NetCraftKernel.Initialize to receive the event
        var options = new GameOptions();
        options.Subscribe();

        //2. Initialize the kernel (triggers LaunchOptions.Parse; kernel-recognized args are consumed, unhandled ones fire the event)
        NetCraftKernel.Initialize(args);

        //3. Parse remaining pending --opt arguments; without a following value they degrade to flags
        options.FlushPending();

        //4. Set --output-dir to override the base; if not passed, use AppContext.BaseDirectory
        //   Downstream reads uniformly from AppPaths to avoid relative paths being interpreted as the runtime working directory
        AppPaths.SetOverride(options.GetOptionOrDefault("output-dir", string.Empty));

        //5. Extract jar and audio assets to a fixed directory
        //   Core game logic later loads directly from assets and assets/sounds without depending on this step
        //   Also copy pack.mcmeta to the root, alongside assets/data
        //   Must run before BootStrap because the latter freezes the registry; after freezing, data-driven loading has nowhere to write
        AssetsExtractor.Extract(options);

        //5.1 Complete the language assets
        //   The jar only carries en_us; other languages are in the resource object storage and land in assets/minecraft/lang and the root pack.mcmeta by resource index
        //   Must run before ResourceManager construction; the folder pack enumerates the directory's files all at once when constructed
        LanguageAssets.Prepare(
            options.GetOptionOrDefault("assets-dir", string.Empty),
            options.GetOptionOrDefault("asset-index", string.Empty));

        //6. First half of Game-layer bootstrap, must run before data-driven loading
        //   The density function type table/block element JSON codecs depend on it; missing it makes the whole batch fail to decode
        //   The environment attribute table must also be registered first; the biome's attributes field looks it up by key
        DataComponents.Bootstrap();
        GameBootstrap.BootstrapBeforeDataLoad();
        EnvironmentAttributes.RegisterAll();

        //7. Construct ResourceManager and data-driven load registry elements, consistent with the server
        //   The client also needs vanilla-accurate biome/density function/noise/noise settings, otherwise singleplayer terrain differs from the server
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
            //configured_carver must come before biome; resolving the biome's carvers field looks them up by registry name
            new RegistryData<ConfiguredWorldCarver>(
                (WritableRegistry<ConfiguredWorldCarver>)BuiltInRegistries.CONFIGURED_CARVER,
                GameConfiguredWorldCarver.ElementCodec),
            //configured_feature / placed_feature must also come before biome; the biome's features field looks them up by registry name
            new RegistryData<NetCraft.Registry.ConfiguredFeature>(
                (WritableRegistry<NetCraft.Registry.ConfiguredFeature>)BuiltInRegistries.CONFIGURED_FEATURE,
                NetCraft.Game.World.Level.LevelGen.Features.ConfiguredFeature.ElementCodec),
            new RegistryData<NetCraft.Registry.PlacedFeature>(
                (WritableRegistry<NetCraft.Registry.PlacedFeature>)BuiltInRegistries.PLACED_FEATURE,
                NetCraft.Game.World.Level.LevelGen.Placement.PlacedFeature.ElementCodec),
            new RegistryData<Biome>((WritableRegistry<Biome>)BuiltInRegistries.BIOME, Biome.DirectCodec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST, MultiNoiseBiomeSourceParameterList.Codec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.NOISE_SETTINGS, NoiseGeneratorSettings.Codec),
            //dimension_type dimension type; client rendering takes sky light/ceiling/skybox parameters by dimension
            new RegistryData<NetCraft.Registry.DimensionType>(
                (WritableRegistry<NetCraft.Registry.DimensionType>)BuiltInRegistries.DIMENSION_TYPE,
                GameDimensionType.ElementCodec),
            //The four structure-related kinds, in the same order as ServerMain; the client only needs to resolve the structure sets by registry name for syncing
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
        //Dimension type fallback: fill in built-in constants when there are no data packs; the client does not load level definitions (no chunk generator needed)
        DimensionTypes.RegisterBuiltin();

        //8. Second half of Game-layer bootstrap and built-in registry freeze
        GameBootstrap.BootstrapAfterDataLoad();
        BootstrapClass.BootStrap();

        //9. Load the client config options.txt
        //   demo mode, fullscreen, render distance, FOV, gamma etc. are read from options.txt
        //   Returns the default config when the file does not exist; does not throw
        //   Runs before the resource reload; when the language table is attached to the reload chain it assembles for the language code here
        var gameConfig = GameConfig.Load(AppPaths.OptionsPath);
        if (options.HasFlag("demo")) gameConfig.Demo = true;
        if (options.HasFlag("fullscreen")) gameConfig.Fullscreen = true;
        Log.Info($"Client config loaded render distance {gameConfig.RenderDistance} FOV {gameConfig.Fov} language {gameConfig.Language}");

        //9.1 Install the table once for the client language code; falls back to en_us when an unsupported code is configured
        //   The resource reload in step 10 installs it again, this time also including the vanilla translations from resource packs
        NcLanguage.Load(gameConfig.Language);

        //10. Trigger data-driven reload of Tags and the language table
        //   The client also needs Tags for item/block tag queries (such as tool tier checks)
        //   Must run after BootstrapClass.BootStrap because BindAll requires the Registry to be frozen
        //   The language table is attached as an extra listener; to switch languages change LanguageCode and run the reload once more to swap the table
        var clientLanguage = new ClientLanguage(gameConfig.Language);
        var rsr = ReloadableServerResources.LoadResources(resourceManager, registryAccess,
            new PreparableReloadListener[] { clientLanguage });
        Log.Info($"Client resources loaded: {rsr.Listeners.Count} listeners, {resourceManager.Packs.Count} packs, {clientLanguage.AvailableLanguages.Count} languages");

        //11. Create the MinecraftClient instance and start the main loop
        //   Phase 11.49 passes a VulkanGuiApp for window-driven mode
        //   MinecraftClient owns gpuApp and releases it on Dispose
        //   rsr is passed for later client Tags queries or /reload reloads
        //   resourceManager is passed to build the block atlas and world render chain once the swapchain is ready
        //   S3 texture injection and world render wiring are handled uniformly by MinecraftClient.EnsureWorldRenderer
        var gpuApp = new VulkanGuiApp(gameConfig.EnableVsync, 800, 600);
        // minecraft.Run blocks the current thread until minecraft.Stop is called
        using var minecraft = new MinecraftClient(gameConfig, gpuApp, rsr: rsr, resourceManager: resourceManager,
            language: clientLanguage);

        //Register Ctrl+C to trigger a graceful shutdown
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            minecraft.Stop();
        };

        minecraft.Run();

        //12. Clean up and exit
        options.Unsubscribe();
        Log.Info("NetCraft client stopped");
        //Log.Debug("Run exit");
    }
}
