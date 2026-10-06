namespace NetCraft.Registry;

//Registration metadata records the source resource pack and lifecycle
public sealed record RegistrationInfo(KnownPack? KnownPackInfo, Lifecycle Lifecycle)
{
    //Built-in entry metadata has no source pack and is stable
    public static readonly RegistrationInfo BuiltIn = new(null, Lifecycle.Stable);
}
