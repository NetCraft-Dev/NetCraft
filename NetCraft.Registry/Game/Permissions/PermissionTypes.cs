using NetCraft.Codec;

namespace NetCraft.Registry;

//PermissionTypes permission type registration, maps to vanilla net.minecraft.server.permissions.PermissionTypes
//Registers the MapCodecs of both Permission kinds into the PERMISSION_TYPE registry
public static class PermissionTypes
{
    //Bootstrap registers all built-in permission types, returning the last registered as in vanilla
    public static MapCodec<Permission> Bootstrap(Registry<MapCodec<Permission>> registry)
    {
        Registry<MapCodec<Permission>>.Register(
            registry, Identifier.WithDefaultNamespace("atom"), Permission.Atom.MapCodec);
        return Registry<MapCodec<Permission>>.Register(
            registry, Identifier.WithDefaultNamespace("command_level"), Permission.HasCommandLevel.MapCodec);
    }
}

//PermissionCheckTypes permission check type registration, maps to vanilla net.minecraft.server.permissions.PermissionCheckTypes
public static class PermissionCheckTypes
{
    //Bootstrap registers all built-in check types, returning the last registered as in vanilla
    public static MapCodec<PermissionCheck> Bootstrap(Registry<MapCodec<PermissionCheck>> registry)
    {
        Registry<MapCodec<PermissionCheck>>.Register(
            registry, Identifier.WithDefaultNamespace("always_pass"), PermissionCheck.AlwaysPass.MapCodec);
        return Registry<MapCodec<PermissionCheck>>.Register(
            registry, Identifier.WithDefaultNamespace("require"), PermissionCheck.Require.MapCodec);
    }
}
