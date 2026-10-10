namespace NetCraft.Client.Blaze3d.Systems;

//DeviceInfo physical device description, aligns with vanilla com.mojang.blaze3d.systems.DeviceInfo
public sealed record DeviceInfo(
    string Name,
    string VendorName,
    string DriverInfo,
    bool IsZZeroToOne,
    string BackendName,
    float TimestampPeriod,
    DeviceLimits Limits,
    DeviceFeatures Features,
    IReadOnlySet<string> UnderlyingExtensions,
    HintsAndWorkarounds HintsAndWorkarounds,
    DeviceType Type);
