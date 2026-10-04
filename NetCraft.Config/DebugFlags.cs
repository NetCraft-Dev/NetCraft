namespace NetCraft.Config;
//调试开关仅Debug构建有意义，Release编译时编译器会消除不可达分支
//对应原版SharedConstants.IS_RUNNING_IN_IDE和80余个调试标志
public static class DebugFlags
{
    //是否在IDE或开发环境运行，影响日志详细度和严格检查
#if DEBUG
    public const bool IsRunningInIde = true;
#else
    public const bool IsRunningInIde = false;
#endif
    //启用严格的注册表freeze检查，freeze后再次注册时抛出异常
    public const bool StrictRegistryFreeze = true;
    //启用NBT解析的严格大小检查，防止恶意存档导致OOM
    public const bool StrictNbtAccounter = true;
    //启用协议字段顺序验证，仅在Debug构建下有效
    public const bool StrictProtocolFieldOrder = false;
    //启用Vulkan validation layer，仅在Debug构建下有效，性能影响较大
    public const bool VulkanValidationLayer = IsRunningInIde;
    //启用Vulkan debug marker，用于RenderDoc或Tracy集成
    public const bool VulkanDebugMarker = IsRunningInIde;
    //启用Tracy性能分析集成，对应原版TracingExecutor
    public const bool TracyProfiling = false;
    //启用chunk序列化的字节级自校验，写入后立即读回并比较差异
    public const bool ChunkSerializeSelfVerify = false;
    //启用NBT序列化的字节级自校验
    public const bool NbtSerializeSelfVerify = false;
    //启用codec字段写入顺序验证
    public const bool CodecFieldOrderVerify = false;
    //启用命令dispatcher的歧义检测，启动时打印警告信息
    public const bool CommandAmbiguityCheck = IsRunningInIde;
    //启用threading detector的全量断言，性能影响较大
    public const bool ThreadingDetectorAssert = false;
    //启用WorldGen的确定性验证，确保同种子生成结果一致
    public const bool WorldgenDeterminismCheck = false;
    //启用BlockState缓存的命中统计
    public const bool BlockStateCacheStats = false;
    //启用注册表查找的命中统计
    public const bool RegistryLookupStats = false;
    //启用内存分配追踪，用于DOT内存分析器集成
    public const bool MemoryAllocationTracking = false;
    //启用packet流量统计，按PacketType聚合
    public const bool PacketTrafficStats = false;
    //启用实体tick的细粒度性能分析，按Entity Type聚合
    public const bool EntityTickProfile = false;
    //禁用世界存档写入，调试时用对应原版SharedConstants.DEBUG_DONT_SAVE_WORLD
    public const bool DebugDontSaveWorld = false;
}