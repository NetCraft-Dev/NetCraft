using DnsClient;
using DnsClient.Protocol;
using NetCraft.Logging;

namespace NetCraft.Client.Multiplayer.Resolver;

//ServerRedirectHandler rewrites an address through a DNS SRV record, maps to vanilla net.minecraft.client.multiplayer.resolver.ServerRedirectHandler
//Vanilla queries "_minecraft._tcp.<host>" through JNDI; this version uses DnsClient, which is the .NET equivalent
public interface ServerRedirectHandler
{
    //Empty the handler that never redirects, mirrors vanilla EMPTY
    static readonly ServerRedirectHandler Empty = new NoRedirect();

    //LookupRedirect returns the redirected address when the host advertises one, or null otherwise
    ServerAddress? LookupRedirect(ServerAddress address);

    //CreateDnsSrvRedirectHandler builds the SRV-backed handler, mirrors vanilla createDnsSrvRedirectHandler
    //Falls back to Empty when the DNS client cannot be created, matching vanilla's fallback when JNDI is unavailable
    static ServerRedirectHandler CreateDnsSrvRedirectHandler()
    {
        try
        {
            return new DnsSrvRedirectHandler();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to initialize the SRV redirect resolver, some servers might not work {e.Message}");
            return Empty;
        }
    }

    private sealed class NoRedirect : ServerRedirectHandler
    {
        public ServerAddress? LookupRedirect(ServerAddress address) => null;
    }

    //DnsSrvRedirectHandler looks up the "_minecraft._tcp" SRV record and returns its first target
    private sealed class DnsSrvRedirectHandler : ServerRedirectHandler
    {
        //DefaultPort SRV is only consulted for the default port, mirrors vanilla
        private const int DefaultPort = 25565;

        //_client shared DNS client; LookupClient keeps its own connection pool, so one instance is enough
        private readonly LookupClient _client = new();

        public ServerAddress? LookupRedirect(ServerAddress address)
        {
            if (address.Port != DefaultPort) return null;
            try
            {
                var answer = _client.Query("_minecraft._tcp." + address.Host, QueryType.SRV);
                foreach (var record in answer.Answers.OfType<SrvRecord>())
                {
                    //The SRV target is a fully qualified name with a trailing dot; drop it so the resolver gets a plain host
                    var target = record.Target.Value.TrimEnd('.');
                    if (target.Length == 0) continue;
                    return new ServerAddress(target, record.Port);
                }
            }
            catch (Exception e)
            {
                Log.Debug($"SRV lookup for {address.Host} failed {e.Message}");
            }
            return null;
        }
    }
}
