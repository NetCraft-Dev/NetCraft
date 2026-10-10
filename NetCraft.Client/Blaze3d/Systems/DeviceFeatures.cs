namespace NetCraft.Client.Blaze3d.Systems;

//DeviceFeatures device capability flags, aligns with vanilla com.mojang.blaze3d.systems.DeviceFeatures
public sealed record DeviceFeatures(
    bool ShaderDrawParameters,
    bool MultiDrawDirectInterleaved,
    bool MultiDrawDirectSeparate,
    bool MultiDrawIndirect,
    bool DrawIndirect,
    bool NonZeroFirstInstance,
    bool PersistentMapping);
