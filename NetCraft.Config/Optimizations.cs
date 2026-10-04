namespace NetCraft.Config;
//性能优化开关
public static class OptimizationFlags
{
    //NBT序列化（2.1/2.2）
    //启用NBT Codec的Source Generator编译期生成（替代反射）
    //C#独家优势Java无法实现
    public const bool NbtCodecSourceGenerator = true;
    //启用NBT IO的MemoryMappedFile+Span实现
    //采用C2ME方案已验证
    public const bool NbtIoMemoryMapped = true;
    //启用NBT写入的NativeMemory池（避免LOH压力）
    public const bool NbtWriteBufferPooled = true;
    //启用注册表freeze后的FrozenDictionary索引
    //采用ModernFix方案已验证
    public const bool RegistryFrozenDictionary = true;
    //启用intrusive holder的struct化（readonly struct+索引）
    public const bool IntrusiveHolderStruct = true;
    //启用注册表bootstrap阶段的并行注册
    public const bool RegistryBootstrapParallel = false;
    //BlockState（2.5）
    //启用BlockState的int编码+readonly struct化
    //采用FerriteCore FastMap方案，已验证(可节省约600MB内存)
    public const bool BlockStateIntEncoded = true;
    //启用BlockState属性的紧凑数组存储（替代Map<Property, Comparable>）
    //采用FerriteCore FastMap等价方案
    public const bool BlockStatePropertyCompactArray = true;
    //启用BlockStateCache的预计算+数组化
    //采用FerriteCore blockstatecache模块方案，已验证
    public const bool BlockStateCachePrecomputed = true;
    //网络（2.6/2.7）
    //启用StreamCodec的static virtual分发（替代反射byNameCodec）
    public const bool StreamCodecStaticDispatch = true;
    //启用VarInt写入的BitOperations硬件加速
    public const bool VarIntBitOperations = true;
    //启用packet字节缓冲的ArrayPool池化
    public const bool PacketBufferPooled = true;
    //存档IO（2.8）
    //启用RegionFile的MemoryMappedFile实现
    //采用C2ME方案，已验证
    public const bool RegionFileMemoryMapped = true;
    //启用IOWorker的System.Threading.Channels异步管道
    public const bool IoWorkerChannels = true;
    //启用chunk序列化的零拷贝Span写入
    public const bool ChunkSerializeZeroCopy = true;
    //数据结构（2.10/2.11）
    //启用PalettedContainer的SIMD批量读写
    //对应优化点2.10，采用ModernFix和C2ME方案，双重验证
    public const bool PalettedContainerSimd = true;
    //启用ClassInstanceMultiMap的FrozenDictionary+ImmutableArray
    //对应优化点2.11
    public const bool ClassInstanceMultiMapFrozen = true;
    //启用BitStorage的Span<ulong>+BitOperations实现
    //采用ModernFix CompactBitStorage等价方案
    public const bool BitStorageSpanBased = true;
    //命令（2.9）
    //启用brigadier CommandNode的readonly struct+ImmutableArray化
    //对应优化点2.9
    public const bool CommandNodeStruct = true;
    //启用命令字符串key的string.Intern池化
    public const bool CommandStringIntern = true;
    //Profiler（2.12）
    //启用Profiler路径的Span<char>栈分配
    //对应优化点2.12
    public const bool ProfilerSpanPath = true;
    //启用Profiler的InterpolatedStringHandler零分配
    public const bool ProfilerZeroAlloc = true;
    //内存（FerriteCore借鉴）
    //启用模型资源路径的string.Intern池化
    //采用FerriteCore mrl模块方案，已验证
    public const bool ModelResourceLocationIntern = true;
    //启用VoxelShape缓存的相同形状共用实例
    //采用FerriteCore方案，已验证
    public const bool VoxelShapeShared = true;
    //启用ThreadingDetector的轻量化（PalettedContainer内存优化）
    //采用FerriteCore threaddetec模块方案，已验证可节省10-15MB内存
    public const bool ThreadingDetectorLightweight = true;
    //启用PatchedDataComponentMap的紧凑存储
    //采用FerriteCore datacomponents模块方案，已验证
    public const bool PatchedDataComponentMapCompact = true;
    //渲染后端
    //启用Vulkan命令缓冲的ObjectPool池化
    public const bool VulkanCommandBufferPooled = true;
    //启用Vulkan资源上传的Span直接memcpy
    public const bool VulkanResourceUploadSpan = true;
    //启用Vulkan多线程命令录制（每线程独立CommandPool）
    public const bool VulkanMultiThreadedRecording = true;
    //启用framegraph的自动屏障插入
    public const bool FrameGraphAutoBarrier = true;
    //启用shaderc SPIR-V编译产物磁盘缓存
    public const bool SpirvCacheDisk = true;
    //启动
    //启用启动时注册表bootstrap的延迟绑定
    //采用ModernFix lazy bootstrap等价方案
    public const bool LazyBootstrap = true;
    //启用资源reload的专用线程
    //采用ModernFix dedicated_reload_executor等价方案
    public const bool DedicatedReloadExecutor = true;
    //区块生成（2.14）
    //启用区块生成的多核并行调度，并发度按处理器核数，关闭时单线程串行
    //采用C2ME Concurrent Chunk Management Engine方案
    public const bool ChunkGenerationParallel = true;
}