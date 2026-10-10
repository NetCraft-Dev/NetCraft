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
using NetCraft.Util;
using NetCraft.Util.Random;
using BootstrapClass = NetCraft.Bootstrap.Bootstrap;
using GameConfiguredWorldCarver = NetCraft.Game.World.Level.LevelGen.Carver.ConfiguredWorldCarver;
//The registry and level definitions each have a DimensionType, the former is a marker interface, this fixes the reference to the real Game-layer type
using GameDimensionType = NetCraft.Game.World.Level.LevelGen.Dimension.DimensionType;

namespace NetCraft.Game;
//ServerMain, the server main entry point
//Maps to vanilla net.minecraft.server.Main
//Chains kernel initialization + startup argument parsing + server business dispatch
//This class is itself the EXE entry point and can also be referenced by NetCraft.Loader for dispatch
public static class ServerMain
{
    private static int _started;

    //Run the server startup main function
    //The process entry is in NetCraft.ServerExe where mod bootstrap also happens, this only handles the startup flow itself
    //args command line arguments, kernel-recognized ones are consumed and the rest are passed to GameOptions through an event
    public static void Run(string[] args)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            Log.Warning("ServerMain already started, ignoring duplicate call");
            //Log.Debug("Run exit");
            return;
        }
        Log.Debug($"Run entry args={string.Join(",", args)}");

        Log.SetClassSource(typeof(ServerMain));
        //The role segment in the crash report file name, the handler shared with the kernel uses it to tell client from server
        CrashHandler.Role = "server";
        Log.Info("NetCraft server starting");

        //GUI is enabled by default, both --nogui and bare nogui disable it, matching vanilla Main's handling of both spellings
        //nogui is declared as a kernel flag for LaunchOptions to swallow, so it does not leak into GameOptions as an unknown argument
        LaunchOptions.DeclareKernelFlag("nogui");
        //noconsole disables the terminal console, maps to Paper's --noconsole and --nojline, for scripts and CI
        LaunchOptions.DeclareKernelFlag("noconsole");
        //potato easter egg toggle, also swallowed by the kernel so GameOptions does not treat it as an unknown argument
        LaunchOptions.DeclareKernelFlag("potato");
        //trace begins a runtime trace capture at startup, so a boot that misbehaves can be looked at afterwards
        LaunchOptions.DeclareKernelFlag("trace");
        var useGui = Array.IndexOf(args, "--nogui") < 0 && Array.IndexOf(args, "nogui") < 0;
        var useConsole = Array.IndexOf(args, "--noconsole") < 0;
        var useTrace = Array.IndexOf(args, "--trace") >= 0;

        //1. Create GameOptions and subscribe to the kernel's unrecognized-argument event
        var options = new GameOptions();
        options.Subscribe();

        //2. Initialize the kernel, triggering LaunchOptions.Parse
        NetCraftKernel.Initialize(args);

        //3. Parse the remaining pending arguments
        options.FlushPending();

        //4. Set the --output-dir override base, AppContext.BaseDirectory when not passed
        //   Downstream reads everything from AppPaths to avoid relative paths being interpreted against the runtime working directory
        AppPaths.SetOverride(options.GetOptionOrDefault("output-dir", string.Empty));

        //4.0 --trace begins a capture here rather than any earlier: the line above is what decides where the trace directory
        //    sits, and starting before it would write under the program directory even when --output-dir moved everything
        //    else. It is still ahead of the resource extraction and the world work, so the capture covers the whole boot
        if (useTrace) RuntimeTraceRecorder.Start(AppPaths.TracesDir);

        //4.1 Load server.properties, generate defaults when absent
        //    Port, max-players, difficulty, online mode, PVP, view distance and so on
        //    Depends only on AppPaths and not on resources, pulled early so everything afterwards knows which language to speak
        var settings = ServerSettings.LoadOrGenerate(AppPaths.ServerPropertiesPath);

        //4.2 Load the language table by the language code in config, falls back to en_us for an unsupported code
        //    The kernel has already extracted language files to lang/, the kernel load used the default code and this step pins it down
        NcLanguage.Load(settings.NcLanguage);
        Log.Info($"Server settings port {settings.ServerPort} level {settings.LevelName} gamemode {settings.Gamemode} difficulty {settings.Difficulty} language {settings.NcLanguage}");

        //5. Extract jar resources to assets/ and data/ and copy pack.mcmeta to the root
        //   Aligned with step 5 of ClientMain so the server can also load vanilla resources and data-driven content
        //   Must precede BootstrapClass.BootStrap since the latter freezes registries and they can no longer be written to after freezing
        AssetsExtractor.Extract(options);

        //6. Build the ResourceManager and load the vanilla pack
        //   The vanilla pack reads the assets/ and data/ subtrees and pack.mcmeta rooted at BaseDirectory
        var registryAccess = BuiltInRegistries.CreateRegistryAccess();
        var resourceManager = new ResourceManager();
        resourceManager.AddPack(new Pack(
            id: Identifier.WithDefaultNamespace("vanilla"),
            title: "Minecraft",
            description: "The default data for Minecraft",
            priority: 0,
            isBuiltin: true,
            resources: new FolderPackResources("vanilla", AppPaths.BaseDirectory)));

        //6.1 Load again after attaching the resource pack, this time vanilla translations are picked up too
        //    NC's own text is still the batch under lang/, both sides merge into one table
        LanguageTable.Load(resourceManager, settings.NcLanguage);

        //7. The first half of Game layer bootstrap, must precede data-driven loading
        //   The density function type table and blocks are codec dependencies of element JSON, missing them fails the whole batch of decoding
        //   The environment attribute table must also be registered first for the biome attributes field to look up by key
        DataComponents.Bootstrap();
        GameBootstrap.BootstrapBeforeDataLoad();
        EnvironmentAttributes.RegisterAll();

        //8. Data-driven loading of registry elements, must precede Freeze
        //   With a jar, biomes/density functions/noise/noise settings take the vanilla true values under data/minecraft/worldgen/
        //   Without a jar, 0 items are loaded and step 9 Noises.Bootstrap and NoiseGeneratorSettings.Overworld fall back
        //   The worldgen registries are object-keyed on the Registries side, Boxed boxes elements decoded by the strongly typed codec for loading
        //   Elements reference each other, the loader retries in rounds until the referenced targets are in place
        var registryLoad = RegistryDataLoader.Load(resourceManager, registryAccess, new RegistryData[]
        {
            new RegistryData<NoiseParameters>(
                (WritableRegistry<NoiseParameters>)BuiltInRegistries.NOISE, NoiseParameters.Codec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.DENSITY_FUNCTION, DensityFunctionCodec.Instance),
            //configured_carver must come before biome, the biome carvers field looks them up by registry name when parsing
            new RegistryData<ConfiguredWorldCarver>(
                (WritableRegistry<ConfiguredWorldCarver>)BuiltInRegistries.CONFIGURED_CARVER,
                GameConfiguredWorldCarver.ElementCodec),
            //configured_feature / placed_feature must also come before biome, the biome features field looks them up by registry name
            new RegistryData<NetCraft.Registry.ConfiguredFeature>(
                (WritableRegistry<NetCraft.Registry.ConfiguredFeature>)BuiltInRegistries.CONFIGURED_FEATURE,
                NetCraft.Game.World.Level.LevelGen.Features.ConfiguredFeature.ElementCodec),
            new RegistryData<NetCraft.Registry.PlacedFeature>(
                (WritableRegistry<NetCraft.Registry.PlacedFeature>)BuiltInRegistries.PLACED_FEATURE,
                NetCraft.Game.World.Level.LevelGen.Placement.PlacedFeature.ElementCodec),
            new RegistryData<Biome>((WritableRegistry<Biome>)BuiltInRegistries.BIOME, Biome.DirectCodec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.MULTI_NOISE_BIOME_SOURCE_PARAMETER_LIST, MultiNoiseBiomeSourceParameterList.Codec),
            RegistryData.Boxed((WritableRegistry<object>)BuiltInRegistries.NOISE_SETTINGS, NoiseGeneratorSettings.Codec),
            //dimension_type, decides the height range / coordinate scale / skylight and ceiling of each dimension
            new RegistryData<NetCraft.Registry.DimensionType>(
                (WritableRegistry<NetCraft.Registry.DimensionType>)BuiltInRegistries.DIMENSION_TYPE,
                GameDimensionType.ElementCodec),
            //The structure series has four kinds, order processor_list → template_pool → structure → structure_set
            //Template pool elements look up processor lists by registry name, structures look up template pools, and structure sets reference structures last
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

        //8.1 Dimension type fallback and level definition loading
        //    dimension_type is loaded by step 8's data-driven pass, without a data pack the three required dimensions are filled in from built-in constants
        //    level_stem has no separate directory, it is embedded in the dimensions section of worldgen/world_preset/<preset>.json
        DimensionTypes.RegisterBuiltin();
        var loadedDimensions = WorldPresets.Load(resourceManager, registryAccess, WorldPresets.Normal);
        Log.Info(loadedDimensions.Count == 0
            ? "Level definitions not loaded (resource pack lacks world_preset), dimension setup falls back to built-ins"
            : $"Level definitions loaded: {loadedDimensions.Count} dimensions {string.Join(",", loadedDimensions)}");

        //9. The second half of Game layer bootstrap and the built-in registry freeze
        //   Noises.Bootstrap skips keys already filled by step 8's data-driven pass, ensuring the vanilla true values in JSON win
        //   Must complete before BootstrapClass.BootStrap since BootStrap freezes all registries
        GameBootstrap.BootstrapAfterDataLoad();
        //9.1 Structure template manager injection, must come after the structure series is loaded, otherwise it cannot get the loaded jigsaw structure instances
        GameBootstrap.InjectStructureTemplates(resourceManager);
        BootstrapClass.BootStrap();

        //10. Trigger data-driven reloads such as Tags
        //    Must come after BootstrapClass.BootStrap since BindAll requires the registries to be frozen
        //    LoadResources registers TagsReloadListener which calls LoadBuiltinTags+BindAll
        var rsr = ReloadableServerResources.LoadResources(resourceManager, registryAccess);
        Log.Info($"Server resources loaded: {rsr.Listeners.Count} listeners, {resourceManager.Packs.Count} packs");

        //12. Initialize world storage
        //   Follows native LevelStorageSource.createWorldStorage to load anvil region files
        //   NetCraft.Storage implements LevelStorage wrapping RegionFileStorage as the world storage entry point
        var levelStorage = new LevelStorage(AppPaths.WorldsDir);
        var levelAccess = levelStorage.CreateAccess(settings.LevelName);
        Log.Info($"World storage ready {levelAccess.WorldDir}");

        //Read level.dat, when present the world metadata is restored from the save and the seed prefers world_gen_settings.dat
        //A new world seed comes from level-seed in server.properties and is fixed after the first write to disk
        //A corrupt level.dat refuses startup, silently treating it as a new world would overwrite the old save when initServer fixes it
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

        //13. Build the DataFixer and DedicatedServer instances and start the main loop
        //   GameDataFixers.BuildV1_21Fixer registers 13 Schemas and 18 Fixes covering the 1.20.2 to 1.21.4 upgrade chain
        //   DedicatedServer internally creates the OVERWORLD dimension SimpleRegionStorage and PersistentServerLevel to hook into the save
        //   Hooking in NoiseBasedChunkGenerator + MultiNoiseBiomeSource puts the world generation subsystem to real use
        //   Noise settings prefer the data-driven minecraft:overworld from step 8, falling back to hardcoded without a jar
        //   rsr holds the ResourceManager and Tags for later /reload commands
        //   server.Run blocks the current thread until server.Stop is called, Stop forces a disk flush
        var dataFixer = GameDataFixers.BuildV1_21Fixer();
        var random = RandomSource.Create(seed);
        //Assemble the overworld generator from the level definition, falling back to the hardcoded fallback when the data pack lacks world_preset
        //Noise settings and the biome parameter list both prefer the data-driven true values from step 8
        var overworldStem = WorldPresets.Get(LevelKeys.OVERWORLD.Identifier);
        var chunkGenerator = overworldStem?.Generator ?? BuildFallbackOverworldGenerator();
        Log.Info(overworldStem is null
            ? "Overworld generator uses hardcoded fallback (level definitions not loaded)"
            : "Overworld generator comes from level definition minecraft:overworld");

        //In GUI mode the server main loop must run on a background thread (the main thread is reserved for the Avalonia message loop)
        //The thread object is registered with DedicatedServer first and the loop body starts once the server is built
        DedicatedServer? guiServer = null;
        var serverThread = useGui
            ? new Thread(() => { guiServer!.InitServer(); guiServer.Run(); })
            { Name = "NetCraft-Server", IsBackground = true }
            : null;

        //Each dimension's type and level definition come from the data pack, DedicatedServer builds worlds one by one from the registries
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

        //BuildFallbackOverworldGenerator, the overworld generator when there is no data pack
        //Equivalent to the data-driven path, only noise settings and the parameter list come from hardcoded constants
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

        //13.1 Subscribe to runtime GC and JIT events, the memory graph baseline red and purple dots and debug logs all come from here
        //Startup GCs must be visible too so this subscribes early, a failure only affects the dots and does not affect the server
        GcEventMonitor.Start();
        JitEventMonitor.Start();

        //Register Ctrl+C for graceful shutdown, in GUI mode the window then closes by itself
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            server.Stop();
        };

        if (useGui)
        {
            //14.1 With GUI: the server main loop goes on a background thread and the main thread is given to Avalonia until the window closes
            //The terminal console stays in both modes, matching vanilla keeping console command input with the GUI on
            Log.Info("Server GUI enabled, closing the window stops the server; pass --nogui or nogui to run headless");
            using ReplConsole? repl = useConsole ? ReplConsole.Start(server) : null;
            guiServer = server;
            serverThread!.Start();
            ServerGuiHost.Run(server, args);
        }
        else
        {
            //14.2 Without GUI: the original path is used, the main thread runs directly, matching vanilla runServer calling initServer then ticking
            //The console must be started before Run blocks the main thread, it opens its own background thread to read keys
            //When input or output is redirected it only starts a line-reading thread, that is the path scripts and pipes use to feed commands
            using ReplConsole? repl = useConsole ? ReplConsole.Start(server) : null;
            server.InitServer();
            server.Run();
        }

        //15. Cleanup and exit
        GcEventMonitor.Stop();
        options.Unsubscribe();
        Log.Info("NetCraft server stopped");
        //Log.Debug("Run exit");
    }

    //WaitForServer blocks the calling thread until the server shuts down
    //The external EXE uses this method to keep the process alive
    public static void WaitForServer(CancellationToken cancellationToken = default)
    {
        Log.Debug($"WaitForServer entry cancellationToken={cancellationToken}");
        try
        {
            cancellationToken.WaitHandle.WaitOne();
        }
        catch (OperationCanceledException)
        {
            //Normal exit
        }
        //Log.Debug("WaitForServer exit");
    }

    //ParseLevelSeed parses the level-seed field in server.properties
    //Empty or non-numeric returns a random long, numeric returns the long.Parse result, aligned with vanilla seed semantics
    private static long ParseLevelSeed(string? seedStr)
    {
        if (string.IsNullOrWhiteSpace(seedStr)) return RandomSupport.GenerateUniqueSeed();
        if (long.TryParse(seedStr, out var seed)) return seed;
        Log.Warning($"level-seed is not a number {seedStr}, using a random seed");
        return RandomSupport.GenerateUniqueSeed();
    }
}
