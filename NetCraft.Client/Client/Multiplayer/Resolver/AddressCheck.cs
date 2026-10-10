namespace NetCraft.Client.Multiplayer.Resolver;

//AddressCheck decides whether a server address may be used, maps to vanilla net.minecraft.client.multiplayer.resolver.AddressCheck
//Vanilla builds it from a javax ServiceLoader BlockListSupplier (a Mojang-side blocked server list)
//NC has no such service, and vanilla allows everything when no provider is registered, so the default check allows everything
public interface AddressCheck
{
    //IsAllowed whether a resolved endpoint is allowed
    bool IsAllowed(ResolvedServerAddress address);

    //IsAllowed whether a raw address is allowed
    bool IsAllowed(ServerAddress address);

    //CreateFromService builds the check from registered block list providers, mirrors vanilla AddressCheck.createFromService
    static AddressCheck CreateFromService() => AllowAll.Instance;

    private sealed class AllowAll : AddressCheck
    {
        public static readonly AllowAll Instance = new();

        public bool IsAllowed(ResolvedServerAddress address) => true;

        public bool IsAllowed(ServerAddress address) => true;
    }
}
