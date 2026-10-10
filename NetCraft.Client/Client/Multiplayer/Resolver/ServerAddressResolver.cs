using System.Net;
using NetCraft.Logging;

namespace NetCraft.Client.Multiplayer.Resolver;

//ServerAddressResolver resolves a ServerAddress into a concrete endpoint, maps to vanilla net.minecraft.client.multiplayer.resolver.ServerAddressResolver
public interface ServerAddressResolver
{
    //System the default resolver backed by the OS resolver, mirrors vanilla SYSTEM
    static readonly ServerAddressResolver System = new SystemResolver();

    //Resolve resolves the address, returning null when the host cannot be resolved
    ResolvedServerAddress? Resolve(ServerAddress address);

    private sealed class SystemResolver : ServerAddressResolver
    {
        public ResolvedServerAddress? Resolve(ServerAddress address)
        {
            try
            {
                var addresses = Dns.GetHostAddresses(address.Host);
                if (addresses.Length == 0) return null;
                //Vanilla InetAddress.getByName takes the first resolved address
                return ResolvedServerAddress.From(new IPEndPoint(addresses[0], address.Port));
            }
            catch (Exception e)
            {
                Log.Debug($"Couldn't resolve server {address.Host} address {e.Message}");
                return null;
            }
        }
    }
}
