namespace NetCraft.Registry;

//PermissionProviderCheck predicate wrapper for permission checks, maps to vanilla net.minecraft.server.permissions.PermissionProviderCheck
//Adapts PermissionCheck to any PermissionSetSupplier
public sealed record PermissionProviderCheck<T>(PermissionCheck Check) where T : PermissionSetSupplier
{
    //Test makes the decision against the supplier
    public bool Test(T supplier) => Check.Check(supplier.Permissions);
}
