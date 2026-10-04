using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace NetCraft.TPGA.Auth;

//PasswordHasher 密码哈希
//游戏账户 PBKDF2-SHA256 对齐 Mojang 风格
//管理员账户 Argon2id 更强安全
//存储格式 base64(salt):base64(hash)
public static class PasswordHasher
{
    private const int Pbkdf2Iterations = 100000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    //HashGameAccount PBKDF2-SHA256
    public static string HashGameAccount(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);
        return ToStored(salt, hash);
    }

    //VerifyGameAccount 校验 PBKDF2 stored 为 null/空(OAuth 账户)直接返回 false
    public static bool VerifyGameAccount(string password, string? stored)
    {
        if (!TryParseStored(stored, out var salt, out var expected)) return false;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);
        return CryptographicOperations.FixedTimeEquals(hash, expected);
    }

    //HashAdmin Argon2id
    public static string HashAdmin(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Argon2id(password, salt);
        return ToStored(salt, hash);
    }

    //VerifyAdmin 校验 Argon2id
    public static bool VerifyAdmin(string password, string stored)
    {
        if (!TryParseStored(stored, out var salt, out var expected)) return false;
        var hash = Argon2id(password, salt);
        return CryptographicOperations.FixedTimeEquals(hash, expected);
    }

    //Argon2id 计算 Argon2id 哈希 参数保守
    private static byte[] Argon2id(string password, byte[] salt)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon.Salt = salt;
        argon.DegreeOfParallelism = 4;
        argon.MemorySize = 65536;
        argon.Iterations = 3;
        return argon.GetBytes(HashSize);
    }

    //ToStored 拼接 base64(salt):base64(hash)
    private static string ToStored(byte[] salt, byte[] hash)
        => Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);

    //TryParseStored 解析存储格式 null/空串直接失败 OAuth 账户 password_hash NULL 走此分支
    private static bool TryParseStored(string? stored, out byte[] salt, out byte[] hash)
    {
        salt = Array.Empty<byte>();
        hash = Array.Empty<byte>();
        if (string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split(':');
        if (parts.Length != 2) return false;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            hash = Convert.FromBase64String(parts[1]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
