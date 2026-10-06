namespace NetCraft.Registry;

//PermissionSet 权限集合对应原版 net.minecraft.server.permissions.PermissionSet
//判入是集合的事 Union 把两个集合并起来 扁平化交给 PermissionSetUnion
//原版 NO_PERMISSIONS/ALL_PERMISSIONS 是两个 lambda 这里是私有子类
public abstract class PermissionSet
{
    public static readonly PermissionSet NoPermissions = new NoPermissionsSet();
    public static readonly PermissionSet AllPermissions = new AllPermissionsSet();

    //HasPermission 是否持有一项权限
    public abstract bool HasPermission(Permission permission);

    //Union 取并集对端已是并集时转交它扁平化 对应原版 default union
    public virtual PermissionSet Union(PermissionSet other)
        => other is PermissionSetUnion union ? union.Union(this) : new PermissionSetUnion(this, other);

    private sealed class NoPermissionsSet : PermissionSet
    {
        public override bool HasPermission(Permission permission) => false;
        public override string ToString() => "NoPermissions";
    }

    private sealed class AllPermissionsSet : PermissionSet
    {
        public override bool HasPermission(Permission permission) => true;
        public override string ToString() => "AllPermissions";
    }
}

//PermissionSetSupplier 持有权限集合的东西对应原版 PermissionSetSupplier
public interface PermissionSetSupplier
{
    PermissionSet Permissions { get; }
}
