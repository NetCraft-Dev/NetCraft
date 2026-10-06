using NetCraft.Config;
using NetCraft.Nbt;

namespace NetCraft.Optimizations.Nbt;

//NBT optimization module, covers core optimization points 2.1 / 2.2
//2.2 NBT IO MemoryMapped integration lives in NetCraft.Nbt/NbtIo.cs, which provides a WithMemoryMapped overload
//2.1 NBT Codec SG pending an independent NetCraft.Nbt.SourceGenerator project
public static class NbtOptimizations
{
    public const string ModuleName = "NBT Optimization";
    public const string TargetSubsystem = "NetCraft.Nbt";

    //Optimization point 2.1, NBT Codec uses compile-time Source Generator output instead of reflection
    //A C# exclusive advantage Java cannot achieve, pending the SG project
    public static bool IsCodecSourceGeneratorEnabled => OptimizationFlags.NbtCodecSourceGenerator;

    //Optimization point 2.2, NBT IO implemented with MemoryMappedFile + Span
    //The C2ME approach, already validated, NbtIo provides a WithMemoryMapped overload
    public static bool IsIoMemoryMappedEnabled => OptimizationFlags.NbtIoMemoryMapped;

    //Optimization point 2.2, NBT writes use a NativeMemory pool to avoid LOH pressure
    public static bool IsWriteBufferPooledEnabled => OptimizationFlags.NbtWriteBufferPooled;

    //IsOptimized checks whether all three toggles are on to decide if NBT optimization is enabled
    public static bool IsOptimized =>
        IsCodecSourceGeneratorEnabled && IsIoMemoryMappedEnabled && IsWriteBufferPooledEnabled;
}
