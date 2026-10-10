namespace NetCraft.Client.Multiplayer.Resolver;

//ServerNameResolver combines resolution, SRV redirect and address checks, maps to vanilla net.minecraft.client.multiplayer.resolver.ServerNameResolver
public sealed class ServerNameResolver
{
    //Default the resolver used by the multiplayer screen
    public static readonly ServerNameResolver Default = new(
        ServerAddressResolver.System,
        ServerRedirectHandler.CreateDnsSrvRedirectHandler(),
        AddressCheck.CreateFromService());

    private readonly ServerAddressResolver _resolver;
    private readonly ServerRedirectHandler _redirectHandler;
    private readonly AddressCheck _addressCheck;

    internal ServerNameResolver(ServerAddressResolver resolver, ServerRedirectHandler redirectHandler, AddressCheck addressCheck)
    {
        _resolver = resolver;
        _redirectHandler = redirectHandler;
        _addressCheck = addressCheck;
    }

    //ResolveAddress resolves the address, applying the block check and following an SRV redirect when present
    //Mirrors vanilla ServerNameResolver.resolveAddress
    public ResolvedServerAddress? ResolveAddress(ServerAddress address)
    {
        var resolved = _resolver.Resolve(address);
        if ((resolved is not null && !_addressCheck.IsAllowed(resolved)) || !_addressCheck.IsAllowed(address))
        {
            return null;
        }
        var redirected = _redirectHandler.LookupRedirect(address);
        if (redirected is not null)
        {
            resolved = _resolver.Resolve(redirected);
            if (resolved is not null && !_addressCheck.IsAllowed(resolved)) return null;
        }
        return resolved;
    }
}
