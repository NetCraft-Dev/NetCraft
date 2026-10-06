namespace NetCraft.Config;
//Performance optimization switches
public static class OptimizationFlags
{
    //NBT serialization (2.1/2.2)
    //Enable compile-time Source Generator for NBT Codec (replaces reflection)
    //A C# exclusive advantage Java cannot achieve
    public const bool NbtCodecSourceGenerator = true;
    //Enable the MemoryMappedFile + Span implementation for NBT IO
    //The C2ME approach, already validated
    public const bool NbtIoMemoryMapped = true;
    //Enable a NativeMemory pool for NBT writes (avoids LOH pressure)
    public const bool NbtWriteBufferPooled = true;
    //Enable FrozenDictionary indexing after registry freeze
    //The ModernFix approach, already validated
    public const bool RegistryFrozenDictionary = true;
    //Turn intrusive holder into a struct (readonly struct + index)
    public const bool IntrusiveHolderStruct = true;
    //Enable parallel registration during the registry bootstrap phase
    public const bool RegistryBootstrapParallel = false;
    //BlockState (2.5)
    //Enable int encoding + readonly struct for BlockState
    //The FerriteCore FastMap approach, already validated (saves about 600MB)
    public const bool BlockStateIntEncoded = true;
    //Enable compact array storage for BlockState properties (replaces Map<Property, Comparable>)
    //The FerriteCore FastMap equivalent
    public const bool BlockStatePropertyCompactArray = true;
    //Enable precomputation + array backing for BlockStateCache
    //The FerriteCore blockstatecache module approach, already validated
    public const bool BlockStateCachePrecomputed = true;
    //Network (2.6/2.7)
    //Enable static virtual dispatch for StreamCodec (replaces reflection byNameCodec)
    public const bool StreamCodecStaticDispatch = true;
    //Enable BitOperations hardware acceleration for VarInt writes
    public const bool VarIntBitOperations = true;
    //Enable ArrayPool pooling for packet byte buffers
    public const bool PacketBufferPooled = true;
    //Save IO (2.8)
    //Enable the MemoryMappedFile implementation for RegionFile
    //The C2ME approach, already validated
    public const bool RegionFileMemoryMapped = true;
    //Enable the System.Threading.Channels async pipeline for IOWorker
    public const bool IoWorkerChannels = true;
    //Enable zero-copy Span writes for chunk serialization
    public const bool ChunkSerializeZeroCopy = true;
    //Data structures (2.10/2.11)
    //Enable SIMD bulk read/write for PalettedContainer
    //Optimization point 2.10, using both the ModernFix and C2ME approaches, double validated
    public const bool PalettedContainerSimd = true;
    //Enable FrozenDictionary + ImmutableArray for ClassInstanceMultiMap
    //Optimization point 2.11
    public const bool ClassInstanceMultiMapFrozen = true;
    //Enable the Span<ulong> + BitOperations implementation for BitStorage
    //The ModernFix CompactBitStorage equivalent
    public const bool BitStorageSpanBased = true;
    //Command (2.9)
    //Turn brigadier CommandNode into readonly struct + ImmutableArray
    //Optimization point 2.9
    public const bool CommandNodeStruct = true;
    //Enable string.Intern pooling for command string keys
    public const bool CommandStringIntern = true;
    //Profiler (2.12)
    //Enable stack allocation of Span<char> for profiler paths
    //Optimization point 2.12
    public const bool ProfilerSpanPath = true;
    //Enable the zero-allocation InterpolatedStringHandler for the profiler
    public const bool ProfilerZeroAlloc = true;
    //Memory (borrowed from FerriteCore)
    //Enable string.Intern pooling for model resource paths
    //The FerriteCore mrl module approach, already validated
    public const bool ModelResourceLocationIntern = true;
    //Enable sharing identical instances in the VoxelShape cache
    //The FerriteCore approach, already validated
    public const bool VoxelShapeShared = true;
    //Lightweight ThreadingDetector (PalettedContainer memory optimization)
    //The FerriteCore threaddetec module approach, already validated at 10-15MB saved
    public const bool ThreadingDetectorLightweight = true;
    //Enable compact storage for PatchedDataComponentMap
    //The FerriteCore datacomponents module approach, already validated
    public const bool PatchedDataComponentMapCompact = true;
    //Rendering backend
    //Enable ObjectPool pooling for Vulkan command buffers
    public const bool VulkanCommandBufferPooled = true;
    //Enable direct Span memcpy for Vulkan resource uploads
    public const bool VulkanResourceUploadSpan = true;
    //Enable multithreaded Vulkan command recording (one CommandPool per thread)
    public const bool VulkanMultiThreadedRecording = true;
    //Enable automatic barrier insertion in the framegraph
    public const bool FrameGraphAutoBarrier = true;
    //Enable on-disk caching of shaderc SPIR-V compilation output
    public const bool SpirvCacheDisk = true;
    //Startup
    //Enable lazy binding of the registry bootstrap at startup
    //The ModernFix lazy bootstrap equivalent
    public const bool LazyBootstrap = true;
    //Enable a dedicated thread for resource reload
    //The ModernFix dedicated_reload_executor equivalent
    public const bool DedicatedReloadExecutor = true;
    //Chunk generation (2.14)
    //Enable multicore parallel scheduling for chunk generation, concurrency follows the core count, single-threaded serial when off
    //The C2ME Concurrent Chunk Management Engine approach
    public const bool ChunkGenerationParallel = true;
}
