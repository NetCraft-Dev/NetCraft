namespace NetCraft.Config;
//Debug switches only matter in Debug builds, in Release the compiler eliminates the unreachable branches
//Maps to vanilla SharedConstants.IS_RUNNING_IN_IDE and the 80-odd debug flags
public static class DebugFlags
{
    //Whether running in the IDE or a development environment, affects log verbosity and strict checks
#if DEBUG
    public const bool IsRunningInIde = true;
#else
    public const bool IsRunningInIde = false;
#endif
    //Enable strict registry freeze checks, registering again after freeze throws
    public const bool StrictRegistryFreeze = true;
    //Enable strict size checks during NBT parsing, prevents a malicious save from causing an OOM
    public const bool StrictNbtAccounter = true;
    //Enable protocol field order validation, only effective in Debug builds
    public const bool StrictProtocolFieldOrder = false;
    //Enable the Vulkan validation layer, only effective in Debug builds, noticeable performance impact
    public const bool VulkanValidationLayer = IsRunningInIde;
    //Enable Vulkan debug markers for RenderDoc or Tracy integration
    public const bool VulkanDebugMarker = IsRunningInIde;
    //Enable Tracy profiling integration, maps to vanilla TracingExecutor
    public const bool TracyProfiling = false;
    //Enable byte-level self-verification for chunk serialization, reads back immediately after writing and compares
    public const bool ChunkSerializeSelfVerify = false;
    //Enable byte-level self-verification for NBT serialization
    public const bool NbtSerializeSelfVerify = false;
    //Enable codec field write order validation
    public const bool CodecFieldOrderVerify = false;
    //Enable ambiguity detection in the command dispatcher, prints warnings at startup
    public const bool CommandAmbiguityCheck = IsRunningInIde;
    //Enable full assertions in the threading detector, noticeable performance impact
    public const bool ThreadingDetectorAssert = false;
    //Enable determinism verification for WorldGen, ensures identical results for the same seed
    public const bool WorldgenDeterminismCheck = false;
    //Enable hit statistics for the BlockState cache
    public const bool BlockStateCacheStats = false;
    //Enable hit statistics for registry lookups
    public const bool RegistryLookupStats = false;
    //Enable memory allocation tracking for the DOT memory profiler integration
    public const bool MemoryAllocationTracking = false;
    //Enable packet traffic statistics, aggregated by PacketType
    public const bool PacketTrafficStats = false;
    //Enable fine-grained profiling of entity ticks, aggregated by entity type
    public const bool EntityTickProfile = false;
    //Disable world save writes while debugging, maps to vanilla SharedConstants.DEBUG_DONT_SAVE_WORLD
    public const bool DebugDontSaveWorld = false;
}
