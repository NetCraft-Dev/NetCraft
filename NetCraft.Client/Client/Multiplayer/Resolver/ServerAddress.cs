using System.Globalization;
using NetCraft.Logging;

namespace NetCraft.Client.Multiplayer.Resolver;

//ServerAddress a server host and port, maps to vanilla net.minecraft.client.multiplayer.resolver.ServerAddress
//Parses and validates the text entered on the multiplayer screen
//Vanilla relies on Guava HostAndPort; this version implements the same parse rules directly
public sealed class ServerAddress
{
    //DefaultPort port used when the text carries none
    private const int DefaultPort = 25565;

    //Invalid fallback returned when parsing fails, mirrors vanilla INVALID
    public static readonly ServerAddress Invalid = new("server.invalid", DefaultPort);

    private readonly string _host;
    private readonly int _port;

    public ServerAddress(string host, int port)
    {
        _host = host;
        _port = port;
    }

    //Host the host without the port, converted with IDN so non-ASCII names become punycode; empty when the conversion fails
    public string Host
    {
        get
        {
            try
            {
                return new IdnMapping().GetAscii(_host);
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }
    }

    //Port the port
    public int Port => _port;

    //ParseString parses "host", "host:port" or "[ipv6]:port"; returns Invalid when the text is unusable
    public static ServerAddress ParseString(string? input)
    {
        if (input is null) return Invalid;
        if (!TryParseHostAndPort(input, DefaultPort, out var host, out var port) || host.Length == 0)
        {
            Log.Info($"Failed to parse server address {input}");
            return Invalid;
        }
        return new ServerAddress(host, port);
    }

    //IsValidAddress whether the text looks like a usable address, mirrors vanilla ServerAddress.isValidAddress
    public static bool IsValidAddress(string input)
    {
        if (!TryParseHostAndPort(input, -1, out var host, out _) || host.Length == 0) return false;
        try
        {
            new IdnMapping().GetAscii(host);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    //ParsePort parses a port text, falling back to the default port, mirrors vanilla ServerAddress.parsePort
    public static int ParsePort(string str)
        => int.TryParse(str.Trim(), out var value) ? value : DefaultPort;

    //TryParseHostAndPort splits text into host and port following Guava HostAndPort.fromString
    //A defaultPort below zero means the port is only validated, not filled in
    //Bracketless text with more than one colon is rejected, so IPv6 hosts must be bracketed as in vanilla
    private static bool TryParseHostAndPort(string input, int defaultPort, out string host, out int port)
    {
        host = string.Empty;
        port = -1;
        if (input.Length == 0) return false;
        string? portText = null;
        if (input[0] == '[')
        {
            var end = input.IndexOf(']');
            if (end < 0) return false;
            host = input[1..end];
            var rest = input[(end + 1)..];
            if (rest.Length > 0)
            {
                if (rest[0] != ':') return false;
                portText = rest[1..];
            }
        }
        else
        {
            var firstColon = input.IndexOf(':');
            if (firstColon < 0)
            {
                host = input;
            }
            else if (input.IndexOf(':', firstColon + 1) >= 0)
            {
                return false;
            }
            else
            {
                host = input[..firstColon];
                portText = input[(firstColon + 1)..];
            }
        }
        if (portText is null || portText.Length == 0)
        {
            if (defaultPort < 0) return true;
            port = defaultPort;
            return true;
        }
        if (!int.TryParse(portText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out port)) return false;
        return port is >= 0 and <= 65535;
    }

    public override bool Equals(object? obj)
        => obj is ServerAddress other && _host == other._host && _port == other._port;

    public override int GetHashCode() => HashCode.Combine(_host, _port);

    public override string ToString() => $"{_host}:{_port}";
}
