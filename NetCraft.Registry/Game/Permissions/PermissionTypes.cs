using NetCraft.Codec;

namespace NetCraft.Registry;

//PermissionTypes 权限类型登记对应原版 net.minecraft.server.permissions.PermissionTypes
//把两种 Permission 的 MapCodec 注册进 PERMISSION_TYPE 注册表
public static class PermissionTypes
{
    //Bootstrap 登记全部内置权限类型 返回最后注册的与原版一致
    public static MapCodec<Permission> Bootstrap(Registry<MapCodec<Permission>> registry)
    {
        Registry<MapCodec<Permission>>.Register(
            registry, Identifier.WithDefaultNamespace("atom"), Permission.Atom.MapCodec);
        return Registry<MapCodec<Permission>>.Register(
            registry, Identifier.WithDefaultNamespace("command_level"), Permission.HasCommandLevel.MapCodec);
    }
}

//PermissionCheckTypes 权限判定类型登记对应原版 net.minecraft.server.permissions.PermissionCheckTypes
public static class PermissionCheckTypes
{
    //Bootstrap 登记全部内置判定类型 返回最后注册的与原版一致
    public static MapCodec<PermissionCheck> Bootstrap(Registry<MapCodec<PermissionCheck>> registry)
    {
        Registry<MapCodec<PermissionCheck>>.Register(
            registry, Identifier.WithDefaultNamespace("always_pass"), PermissionCheck.AlwaysPass.MapCodec);
        return Registry<MapCodec<PermissionCheck>>.Register(
            registry, Identifier.WithDefaultNamespace("require"), PermissionCheck.Require.MapCodec);
    }
}
