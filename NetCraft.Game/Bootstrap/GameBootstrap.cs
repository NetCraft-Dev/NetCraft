using NetCraft.Game.Commands.Synchronization;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Carver;
using NetCraft.Game.World.Level.LevelGen.Features.Impl.Misc;
using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Game.World.Level.Material;
using NetCraft.Game.World.Clock;
using NetCraft.Game.World.Timeline;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Storage.Paletted;
using WorldCarverBootstrap = NetCraft.Game.World.Level.LevelGen.Carver.WorldCarver;

namespace NetCraft.Game.Bootstrap;

//GameBootstrap Game-layer bootstrap entry, maps to the Game-layer extension of vanilla net.minecraft.server.Bootstrap
//Called before BootstrapClass.BootStrap to trigger Game-layer built-in registry population for blocks/items/entities
//Ensures business flows such as NoiseBasedChunkGenerator.FillFromNoise can access registered BlockState
public static class GameBootstrap
{
    //Vanilla Bootstrap is only called from the startup thread, so a single volatile flag suffices
    //Here chunk packet construction runs on connection threads and chunk generation on the ThreadPool, so a lock is required to run only once
    //Later threads must wait until bootstrap truly finishes before returning; returning on the flag alone would read a half-built registry
    private static readonly object _gate = new();
    private static bool _bootstrapped;
    private static bool _preDataBootstrapped;
    //_ready fast-path flag for a fully finished bootstrap; the generation path calls Bootstrap three times per chunk, so it uses this to bypass the lock
    //Kept separate from the two above: those must be set first to prevent reentrancy, but setting them early does not mean the work is done
    private static volatile bool _ready;

    static GameBootstrap() => Log.SetClassSource(typeof(GameBootstrap));

    //Bootstrap Game-layer bootstrap entry, triggers built-in registry population for blocks/items
    //Repeated calls are idempotent and return immediately to avoid re-registration
    //The idempotent branch logs nothing; chunk packet serialization calls it once per packet and debug logging would flood the log file
    public static void Bootstrap()
    {
        //Once bootstrap is fully done this lock-free fast path is taken; calls on the generation hot path all stop here
        if (_ready) return;
        BootstrapBeforeDataLoad();
        BootstrapAfterDataLoad();
    }

    //BootstrapBeforeDataLoad bootstrap before the data-driven registry load
    //Element JSON codecs reference blocks/density function types/biomes; these must be in place before decoding
    //Callers that skip the data-driven load just run Bootstrap once
    public static void BootstrapBeforeDataLoad()
    {
        lock (_gate)
        {
            if (_preDataBootstrapped) return;
            //Set the flag before running; if registration reenters, Monitor is reentrant so there is no deadlock, and a reentry after the flag is set returns immediately
            _preDataBootstrapped = true;
            //Log.Debug("BootstrapBeforeDataLoad enter");
            Log.Info("Game layer bootstrap started");
            Blocks.Bootstrap();
            RegisterFluids();
            //Ticket types must be registered before the registry freezes; chunk_tickets.dat restores tickets by registry name
            NetCraft.Storage.TicketType.Bootstrap();
            WorldCarverBootstrap.RegisterAll();
            //Feature and placement modifier registries must be filled before data load, otherwise the whole configured_feature/placed_feature batch fails to decode
            NetCraft.Game.World.Level.LevelGen.Features.FeatureBootstrap.RegisterAll();
            NetCraft.Game.World.Level.LevelGen.Placement.PlacementModifierBootstrap.RegisterAll();
            //Structure placement types must be in place before worldgen/structure_set loads, otherwise placement type dispatch finds no target
            NetCraft.Game.World.Level.LevelGen.Structure.StructureBootstrap.RegisterAll();
            //Piece types must be in place before structure loading, otherwise a serialized piece id resolves to no implementation
            NetCraft.Game.World.Level.LevelGen.Structure.StructurePieceBootstrap.RegisterAll();
            //Processor types plus rule tests/pos rules/block entity modifiers must be in place before processor_list loads
            NetCraft.Game.World.Level.LevelGen.Structure.StructureProcessorBootstrap.RegisterAll();
            //Pool element types must be in place before worldgen/template_pool loads, otherwise element_type dispatch finds no target
            NetCraft.Game.World.Level.LevelGen.Structure.StructurePoolBootstrap.RegisterAll();
            Items.Bootstrap();
            //The dispense behavior table is registered per item instance, so it must come after items are filled
            NetCraft.Game.World.Level.Block.Dispenser.DispenseBehaviors.Bootstrap();
            GameRules.Bootstrap();
            //Particle types are registered in vanilla declaration order; commands and particle packets both encode by registry id
            NetCraft.Game.World.Particle.ParticleTypes.Bootstrap();
            //Mob effects are registered in vanilla declaration order; commands and effect packets both encode by registry id
            NetCraft.Game.World.Effect.MobEffects.Bootstrap();
            ArgumentTypeInfos.Bootstrap();
            //Entity attributes are registered before entity types; entity construction builds its table from attributes, and this must also precede registry freeze
            NetCraft.Registry.EntityAttribute.Attributes.Bootstrap();
            EntityTypes.Bootstrap();
            //Entity sub-predicates are registered by name, for the entity predicate composite to dispatch by type name
            NetCraft.Game.Advancements.Predicates.Entity.EntitySubPredicates.Bootstrap();
            //Damage types are registered in vanilla declaration order; damage sources and death messages both look up by registry
            NetCraft.Registry.DamageTypes.Bootstrap();
            //The default attribute table for entity types must be assembled before any entity is constructed
            NetCraft.Game.World.Entity.DefaultAttributes.Bootstrap();
            BlockEntityTypes.Bootstrap();
            MenuTypes.Bootstrap();
            WorldClocks.Bootstrap();
            Timelines.Bootstrap();
            DensityFunctionBootstrap.RegisterAll();
            //Permission and predicate types are registered into their own MapCodec registries, used to dispatch encoding/decoding
            PermissionTypes.Bootstrap(BuiltInRegistries.PERMISSION_TYPE);
            PermissionCheckTypes.Bootstrap(BuiltInRegistries.PERMISSION_CHECK_TYPE);
            //Log.Debug("BootstrapBeforeDataLoad exit");
        }
    }

    //BootstrapAfterDataLoad completion after the data-driven registry load
    //Noises.Bootstrap and RegisterBiomes both skip keys already filled by data-driven loading, so the vanilla truth from JSON wins
    public static void BootstrapAfterDataLoad()
    {
        lock (_gate)
        {
            if (_bootstrapped) return;
            _bootstrapped = true;
            Noises.Bootstrap();
            RegisterBiomes();
            Log.Debug("Game layer bootstrap finished, blocks registered");
            //Log.Debug("BootstrapAfterDataLoad exit");
            //Set the flag last: when the fast path reads true the bootstrap must truly be done; setting it early would let other threads read a half-built state
            _ready = true;
        }
    }

    //StructureTemplates structure template manager injected by world assembly
    //Loading a pool element piece takes its context from here; without injection, loaded structure pieces cannot place anything
    public static StructureTemplateManager? StructureTemplates { get; private set; }

    //InjectStructureTemplates injects the structure template manager into content that reads templates, after data load
    //Jigsaw structure assembly and fossil/template features both read data/<ns>/structure/<path>.nbt by registry name
    //Without injection these consume random numbers like vanilla then return false, so the world ends up with no jigsaw structure at all
    public static void InjectStructureTemplates(ResourceManager resources)
    {
        var manager = new StructureTemplateManager(resources);
        StructureTemplates = manager;
        foreach (var holder in BuiltInRegistries.STRUCTURE.ListElements())
        {
            if (holder.Value is JigsawStructure jigsaw) jigsaw.TemplateManager = manager;
        }
        FossilFeature.TemplateManager = manager;
        TemplateFeature.TemplateManager = manager;
    }

    //RegisterFluids registers the five built-in fluids
    //The carver's matching_fluids and the lake feature's fluid field both reference by registry name; missing one breaks the whole config decode
    //The implementation lives in Material/Fluids; this only triggers its static initialization to register in order
    private static void RegisterFluids() => _ = Fluids.Empty;

    //RegisterBiomes registers fallback biomes into BuiltInRegistries.BIOME and syncs every biome into the container factory
    //Real biomes are loaded by the data-driven step 8; this only adds a placeholder when BIOME has no plains (no-datapack case)
    //The container factory must see every biome: GlobalPalette.IdFor returns 0 for unregistered values, so when only plains is registered
    //Once a section's biome palette overflows to the global palette, biomes other than plains get silently written as id 0 and read back as plains
    private static void RegisterBiomes()
    {
        var plainsId = Identifier.WithDefaultNamespace("plains");
        if (!BuiltInRegistries.BIOME.ContainsKey(plainsId))
        {
            var plainsKey = ResourceKey<Biome>.Create(Registries.BIOME, plainsId);
            BuiltInRegistries.BIOME.Register(plainsKey, Biome.Plains, RegistrationInfo.BuiltIn);
        }
        //Registration order is the global id order in the container factory; iterating the registry keeps both sides consistent
        //RegisterBiome goes through IdMap's idempotent Add; re-registration returns the existing id and does not disturb order
        var factory = PalettedContainerFactory.Default;
        foreach (var key in BuiltInRegistries.BIOME.RegistryKeySet)
        {
            var biome = BuiltInRegistries.BIOME.GetValue(key);
            if (biome is not null) factory.RegisterBiome(Holder<Biome>.Direct(biome));
        }
        var plains = BuiltInRegistries.BIOME.GetValue(plainsId) ?? Biome.Plains;
        factory.RegisterBiome(Holder<Biome>.Direct(plains));
    }

    public static bool IsBootstrapped
    {
        get { lock (_gate) return _bootstrapped; }
    }

    public static void Reset()
    {
        lock (_gate)
        {
            _bootstrapped = false;
            _preDataBootstrapped = false;
            DensityFunctionBootstrap.Reset();
        }
    }
}
