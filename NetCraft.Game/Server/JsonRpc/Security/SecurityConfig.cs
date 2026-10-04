using System.Security.Cryptography;

namespace NetCraft.Game.Server.JsonRpc.Security;

//SecurityConfig 管理服务 API key 配置 对应原版 net.minecraft.server.jsonrpc.security.SecurityConfig
//密钥固定 40 位字母数字 缺省由 GenerateSecretKey 生成 也可由 server.properties 指定
public sealed record SecurityConfig(string SecretKey)
{
    private const string SecretKeyChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int SecretKeyLength = 40;

    //IsValid 校验密钥格式 非 40 位纯字母数字一律非法
    public static bool IsValid(string secretKey)
    {
        if (secretKey.Length != SecretKeyLength) return false;
        foreach (var c in secretKey)
        {
            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9') continue;
            return false;
        }
        return true;
    }

    //GenerateSecretKey 用加密随机源生成 40 位密钥
    public static string GenerateSecretKey()
    {
        var chars = new char[SecretKeyLength];
        for (var i = 0; i < SecretKeyLength; i++)
        {
            chars[i] = SecretKeyChars[RandomNumberGenerator.GetInt32(SecretKeyChars.Length)];
        }
        return new string(chars);
    }
}
