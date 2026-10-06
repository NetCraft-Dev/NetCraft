namespace NetCraft.Registry;

//PermissionSet permission set, maps to vanilla net.minecraft.server.permissions.PermissionSet
//Membership is the set's job and Union combines two sets, delegating flattening to PermissionSetUnion
//Vanilla NO_PERMISSIONS/ALL_PERMISSIONS are two lambdas, here they are private subclasses
public abstract class PermissionSet
{
    public static readonly PermissionSet NoPermissions = new NoPermissionsSet();
    public static readonly PermissionSet AllPermissions = new AllPermissionsSet();

    //HasPermission whether it holds a permission
    public abstract bool HasPermission(Permission permission);

    //Union takes the union; if the other is already a union it delegates flattening to it, maps to vanilla default union
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

//PermissionSetSupplier something that holds a permission set, maps to vanilla PermissionSetSupplier
public interface PermissionSetSupplier
{
    PermissionSet Permissions { get; }
}
