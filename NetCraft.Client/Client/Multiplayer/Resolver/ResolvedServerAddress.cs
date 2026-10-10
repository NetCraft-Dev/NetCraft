using System.Net;

namespace NetCraft.Client.Multiplayer.Resolver;

//ResolvedServerAddress a resolved server endpoint, maps to vanilla net.minecraft.client.multiplayer.resolver.ResolvedServerAddress
//Vanilla exposes it as an interface built from an anonymous implementation; the endpoint is the only carried state
public interface ResolvedServerAddress
{
    //HostName resolved host name, falling back to the IP text when the reverse lookup yields nothing
    string HostName { get; }

    //HostIp IP text
    string HostIp { get; }

    //Port port
    int Port { get; }

    //AsInetSocketAddress the underlying endpoint the socket is opened on
    IPEndPoint AsInetSocketAddress();

    //From wraps an endpoint, mirrors vanilla ResolvedServerAddress.from
    static ResolvedServerAddress From(IPEndPoint address) => new Default(address);

    private sealed class Default(IPEndPoint address) : ResolvedServerAddress
    {
        private string? _hostName;

        public string HostName
        {
            get
            {
                //Vanilla InetAddress.getHostName falls back to the IP text when reverse lookup finds nothing; cache it the same way
                if (_hostName is not null) return _hostName;
                try
                {
                    _hostName = Dns.GetHostEntry(address.Address).HostName;
                }
                catch (Exception)
                {
                    _hostName = address.Address.ToString();
                }
                return _hostName;
            }
        }

        public string HostIp => address.Address.ToString();

        public int Port => address.Port;

        public IPEndPoint AsInetSocketAddress() => address;
    }
}
