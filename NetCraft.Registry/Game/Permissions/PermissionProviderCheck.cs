namespace NetCraft.Registry;

//PermissionProviderCheck 权限判定的谓词包装对应原版 net.minecraft.server.permissions.PermissionProviderCheck
//把 PermissionCheck 适配到任意 PermissionSetSupplier 上
public sealed record PermissionProviderCheck<T>(PermissionCheck Check) where T : PermissionSetSupplier
{
    //Test 对供给方做判定
    public bool Test(T supplier) => Check.Check(supplier.Permissions);
}
