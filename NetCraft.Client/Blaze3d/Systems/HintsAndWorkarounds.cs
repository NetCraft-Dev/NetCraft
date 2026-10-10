namespace NetCraft.Client.Blaze3d.Systems;

//HintsAndWorkarounds per-backend behaviour hints, aligns with vanilla com.mojang.blaze3d.systems.HintsAndWorkarounds
public sealed record HintsAndWorkarounds(
    bool WriteToBufferIsSlow,
    bool AnisotropyHasKnownIssues);
