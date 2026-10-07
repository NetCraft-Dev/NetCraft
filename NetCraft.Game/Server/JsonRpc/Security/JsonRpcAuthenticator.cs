using System.Security.Cryptography;
using System.Text;

namespace NetCraft.Game.Server.JsonRpc.Security;

//SecurityCheckResult authentication check result, maps to the internal class of vanilla AuthenticationHandler
//TokenSentInSecWebsocketProtocol true means the key is carried in the websocket subprotocol; the adapter layer writes the negotiated header accordingly
public sealed record SecurityCheckResult(bool Allowed, string? Reason, bool TokenSentInSecWebsocketProtocol)
{
    public static SecurityCheckResult Ok(bool tokenSentInSecWebsocketProtocol = false) => new(true, null, tokenSentInSecWebsocketProtocol);

    public static SecurityCheckResult Denied(string reason) => new(false, reason, false);
}

//JsonRpcAuthenticator manages the service API key authentication, maps to vanilla net.minecraft.server.jsonrpc.security.AuthenticationHandler
//Vanilla extends netty ChannelDuplexHandler for pipeline interception; this strips the transport layer and keeps only the authentication decision
//The caller provides the request headers and writes back the 401 response and websocket subprotocol header
public sealed class JsonRpcAuthenticator
{
    //BearerPrefix the Bearer scheme prefix of the Authorization header
    public const string BearerPrefix = "Bearer ";
    //SubProtocolValue the websocket subprotocol name written back on a successful handshake
    public const string SubProtocolValue = "minecraft-v1";
    private const string SubProtocolHeaderPrefix = "minecraft-v1,";

    private readonly SecurityConfig _securityConfig;
    private readonly HashSet<string> _allowedOrigins;

    //JsonRpcAuthenticator constructor; allowed origins are split by comma into a whitelist
    public JsonRpcAuthenticator(SecurityConfig securityConfig, string allowedOrigins)
    {
        _securityConfig = securityConfig;
        _allowedOrigins = new HashSet<string>(allowedOrigins.Split(','), StringComparer.Ordinal);
    }

    //Check checks the request headers and returns whether to allow and the rejection reason
    //The header dictionary must be case-insensitive; Authorization and Sec-WebSocket-Protocol are alternatives, the latter needing the Origin whitelist first
    public SecurityCheckResult Check(IReadOnlyDictionary<string, string?> headers)
    {
        var token = ParseTokenInAuthorizationHeader(headers);
        if (token is not null)
        {
            return IsValidApiKey(token) ? SecurityCheckResult.Ok() : SecurityCheckResult.Denied("Invalid API key");
        }
        token = ParseTokenInSecWebsocketProtocolHeader(headers);
        if (token is not null)
        {
            if (!IsAllowedOriginHeader(headers)) return SecurityCheckResult.Denied("Origin Not Allowed");
            return IsValidApiKey(token) ? SecurityCheckResult.Ok(true) : SecurityCheckResult.Denied("Invalid API key");
        }
        return SecurityCheckResult.Denied("Missing API key");
    }

    //IsValidApiKey compares the key in constant time; passes only when equal length and byte-identical
    public bool IsValidApiKey(string suppliedKey)
    {
        if (suppliedKey.Length == 0) return false;
        var supplied = Encoding.UTF8.GetBytes(suppliedKey);
        var configured = Encoding.UTF8.GetBytes(_securityConfig.SecretKey);
        return CryptographicOperations.FixedTimeEquals(supplied, configured);
    }

    //ParseTokenInAuthorizationHeader takes the key after the Bearer scheme; non-Bearer returns empty
    private static string? ParseTokenInAuthorizationHeader(IReadOnlyDictionary<string, string?> headers)
    {
        if (!headers.TryGetValue("Authorization", out var value) || value is null) return null;
        if (!value.StartsWith(BearerPrefix, StringComparison.Ordinal)) return null;
        return value[BearerPrefix.Length..].Trim();
    }

    //ParseTokenInSecWebsocketProtocolHeader takes the key after "minecraft-v1," in the websocket subprotocol
    private static string? ParseTokenInSecWebsocketProtocolHeader(IReadOnlyDictionary<string, string?> headers)
    {
        if (!headers.TryGetValue("Sec-WebSocket-Protocol", out var value) || value is null) return null;
        if (!value.StartsWith(SubProtocolHeaderPrefix, StringComparison.Ordinal)) return null;
        return value[SubProtocolHeaderPrefix.Length..].Trim();
    }

    //IsAllowedOriginHeader an empty Origin header or one not on the whitelist counts as not allowed
    private bool IsAllowedOriginHeader(IReadOnlyDictionary<string, string?> headers)
    {
        if (!headers.TryGetValue("Origin", out var origin) || string.IsNullOrEmpty(origin)) return false;
        return _allowedOrigins.Contains(origin);
    }
}
