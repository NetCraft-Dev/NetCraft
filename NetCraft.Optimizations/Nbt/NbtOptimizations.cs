using NetCraft.Config;
using NetCraft.Nbt;

namespace NetCraft.Optimizations.Nbt;

//NBT 优化模块对应核心优化点 2.1 / 2.2
//2.2 NBT IO MemoryMapped 集成于 NetCraft.Nbt/NbtIo.cs 提供 WithMemoryMapped 重载
//2.1 NBT Codec SG 待 NetCraft.Nbt.SourceGenerator 项目独立实现
public static class NbtOptimizations
{
    public const string ModuleName = "NBT Optimization";
    public const string TargetSubsystem = "NetCraft.Nbt";

    //对应优化点 2.1 NBT Codec 用 Source Generator 编译期生成替代反射
    //C# 独家优势 Java 无法实现待 SG 项目落地
    public static bool IsCodecSourceGeneratorEnabled => OptimizationFlags.NbtCodecSourceGenerator;

    //对应优化点 2.2 NBT IO 用 MemoryMappedFile + Span 实现
    //采用 C2ME 方案已验证 NbtIo 提供 WithMemoryMapped 重载
    public static bool IsIoMemoryMappedEnabled => OptimizationFlags.NbtIoMemoryMapped;

    //对应优化点 2.2 NBT 写入用 NativeMemory 池避免 LOH 压力
    public static bool IsWriteBufferPooledEnabled => OptimizationFlags.NbtWriteBufferPooled;

    //IsOptimized 检查三个开关是否全开判断 NBT 优化是否启用
    public static bool IsOptimized =>
        IsCodecSourceGeneratorEnabled && IsIoMemoryMappedEnabled && IsWriteBufferPooledEnabled;
}
