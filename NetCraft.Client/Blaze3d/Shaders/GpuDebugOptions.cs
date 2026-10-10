namespace NetCraft.Client.Blaze3d.Shaders;

//GpuDebugOptions GPU debug/validation options, aligns with vanilla com.mojang.blaze3d.shaders.GpuDebugOptions
public sealed record GpuDebugOptions(int LogLevel, bool SynchronousLogs, bool UseLabels, bool UseValidationLayers)
{
    public static GpuDebugOptions None { get; } = new(0, false, false, false);
}
