namespace NetCraft.Registry;

//注册元信息记录来源资源包与生命周期
public sealed record RegistrationInfo(KnownPack? KnownPackInfo, Lifecycle Lifecycle)
{
    //内置注册项元信息无来源包stable
    public static readonly RegistrationInfo BuiltIn = new(null, Lifecycle.Stable);
}
