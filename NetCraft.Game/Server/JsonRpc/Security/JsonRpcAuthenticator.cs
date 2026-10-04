using System.Security.Cryptography;
using System.Text;

namespace NetCraft.Game.Server.JsonRpc.Security;

//SecurityCheckResult 认证检查结果 对应原版 AuthenticationHandler 内部类
//TokenSentInSecWebsocketProtocol 为真表示密钥走 websocket 子协议携带 适配层据此回写协商头
public sealed record SecurityCheckResult(bool Allowed, string? Reason, bool TokenSentInSecWebsocketProtocol)
{
    public static SecurityCheckResult Ok(bool tokenSentInSecWebsocketProtocol = false) => new(true, null, tokenSentInSecWebsocketProtocol);

    public static SecurityCheckResult Denied(string reason) => new(false, reason, false);
}

//JsonRpcAuthenticator 管理服务 API key 认证 对应原版 net.minecraft.server.jsonrpc.security.AuthenticationHandler
//原版继承 netty ChannelDuplexHandler 做链路拦截 这里剥离传输层只保留认证决策
//调用方负责提供请求头与回写 401 响应及 websocket 子协议头
public sealed class JsonRpcAuthenticator
{
    //BearerPrefix Authorization 头的 Bearer 方案前缀
    public const string BearerPrefix = "Bearer ";
    //SubProtocolValue 握手成功时回写的 websocket 子协议名
    public const string SubProtocolValue = "minecraft-v1";
    private const string SubProtocolHeaderPrefix = "minecraft-v1,";

    private readonly SecurityConfig _securityConfig;
    private readonly HashSet<string> _allowedOrigins;

    //JsonRpcAuthenticator 构造 允许来源按逗号分割成白名单
    public JsonRpcAuthenticator(SecurityConfig securityConfig, string allowedOrigins)
    {
        _securityConfig = securityConfig;
        _allowedOrigins = new HashSet<string>(allowedOrigins.Split(','), StringComparer.Ordinal);
    }

    //Check 检查请求头 返回是否放行与拒绝原因
    //头字典需忽略大小写 Authorization 与 Sec-WebSocket-Protocol 二选一 后者需先过 Origin 白名单
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

    //IsValidApiKey 常量时间比较密钥 等长且逐字节一致才通过
    public bool IsValidApiKey(string suppliedKey)
    {
        if (suppliedKey.Length == 0) return false;
        var supplied = Encoding.UTF8.GetBytes(suppliedKey);
        var configured = Encoding.UTF8.GetBytes(_securityConfig.SecretKey);
        return CryptographicOperations.FixedTimeEquals(supplied, configured);
    }

    //ParseTokenInAuthorizationHeader 取 Bearer 方案后的密钥 非 Bearer 返回空
    private static string? ParseTokenInAuthorizationHeader(IReadOnlyDictionary<string, string?> headers)
    {
        if (!headers.TryGetValue("Authorization", out var value) || value is null) return null;
        if (!value.StartsWith(BearerPrefix, StringComparison.Ordinal)) return null;
        return value[BearerPrefix.Length..].Trim();
    }

    //ParseTokenInSecWebsocketProtocolHeader 取 websocket 子协议里 "minecraft-v1," 后的密钥
    private static string? ParseTokenInSecWebsocketProtocolHeader(IReadOnlyDictionary<string, string?> headers)
    {
        if (!headers.TryGetValue("Sec-WebSocket-Protocol", out var value) || value is null) return null;
        if (!value.StartsWith(SubProtocolHeaderPrefix, StringComparison.Ordinal)) return null;
        return value[SubProtocolHeaderPrefix.Length..].Trim();
    }

    //IsAllowedOriginHeader Origin 头为空或不在白名单都算不允许
    private bool IsAllowedOriginHeader(IReadOnlyDictionary<string, string?> headers)
    {
        if (!headers.TryGetValue("Origin", out var origin) || string.IsNullOrEmpty(origin)) return false;
        return _allowedOrigins.Contains(origin);
    }
}
