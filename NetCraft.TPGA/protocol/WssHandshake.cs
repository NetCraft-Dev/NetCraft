using System.Text.Json;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Crypto;

namespace NetCraft.TPGA.Protocol;

//WssHandshake wss 应用层握手
//RSA 解密身份包 校验 timestamp 5 分钟内 查 api_accounts 验证 Argon2id
//成功返回 ApiAccount 由 WssHandler 颁发 UUID 注册会话
public sealed class WssHandshake
{
    private readonly RsaKeyProvider _rsa;
    private readonly ApiAccountRepository _apis;
    //timestamp 容忍 5 分钟 防重放
    private const int TimestampToleranceSeconds = 300;

    public WssHandshake(RsaKeyProvider rsa, ApiAccountRepository apis)
    {
        _rsa = rsa;
        _apis = apis;
    }

    //Authenticate 解密身份包并校验 返回 api 账户失败抛 WssException
    public ApiAccount Authenticate(string encryptedBase64)
    {
        var payload = DecryptPayload(encryptedBase64);
        VerifyTimestamp(payload.Timestamp);
        var api = _apis.FindByUsername(payload.Username)
            ?? throw new WssException("unknown api account");
        if (!api.Enabled)
            throw new WssException("api account disabled");
        if (!PasswordHasher.VerifyAdmin(payload.Password, api.PasswordHash))
            throw new WssException("invalid credentials");
        return api;
    }

    //DecryptPayload base64 解码后 RSA 解密再反序列化身份包
    private AuthPayload DecryptPayload(string encryptedBase64)
    {
        byte[] cipher;
        try { cipher = Convert.FromBase64String(encryptedBase64); }
        catch { throw new WssException("invalid payload encoding"); }

        byte[] plain;
        try { plain = _rsa.Decrypt(cipher); }
        catch { throw new WssException("decrypt failed"); }

        try
        {
            return JsonSerializer.Deserialize<AuthPayload>(plain, _jsonOptions)
                ?? throw new WssException("invalid payload");
        }
        catch (WssException) { throw; }
        catch { throw new WssException("invalid payload json"); }
    }

    //VerifyTimestamp 校验时间戳绝对偏差不超过容忍窗口
    private static void VerifyTimestamp(long timestamp)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > TimestampToleranceSeconds)
            throw new WssException("timestamp expired");
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
