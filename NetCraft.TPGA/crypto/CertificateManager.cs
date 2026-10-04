using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Crypto;

//CertificateManager 证书加载或自签生成
//configPath 指定则加载 configPath 空则用自动路径 tpga_auto.pfx
//自动路径已存在则加载 不重复生成 配置里 certificatePath 保持空表示自动
//password 为空时生成随机密码并标志 passwordGenerated 供 Boot 回写
public static class CertificateManager
{
    //LoadOrGenerate 加载或生成证书
    //返回 (证书, 实际密码, 是否新生成密码需回写)
    public static (X509Certificate2 cert, string password, bool passwordGenerated) LoadOrGenerate(string configPath, string configPassword)
    {
        //指定路径优先加载
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            var pwd = configPassword ?? "";
            var cert = new X509Certificate2(configPath, pwd, X509KeyStorageFlags.Exportable);
            Log.Info("Cert", $"loaded {configPath}");
            return (cert, pwd, false);
        }

        var autoPath = string.IsNullOrEmpty(configPath)
            ? Path.Combine(AppContext.BaseDirectory, "tpga_auto.pfx")
            : configPath;

        //自动路径已存在且密码非空则加载 避免重复生成
        if (File.Exists(autoPath) && !string.IsNullOrEmpty(configPassword))
        {
            var cert = new X509Certificate2(autoPath, configPassword, X509KeyStorageFlags.Exportable);
            Log.Info("Cert", $"loaded auto {autoPath}");
            return (cert, configPassword, false);
        }

        //生成新证书
        bool pwdGenerated = string.IsNullOrEmpty(configPassword);
        var pwd2 = pwdGenerated ? GeneratePassword() : configPassword;
        var cert2 = GenerateSelfSigned(autoPath, pwd2);
        Log.Info("Cert", $"self-signed generated {autoPath}");
        if (pwdGenerated)
            Log.Info("Cert", "password was empty, random generated");
        return (cert2, pwd2, pwdGenerated);
    }

    //GenerateSelfSigned 生成 RSA 2048 自签名证书写 PFX 返回证书
    private static X509Certificate2 GenerateSelfSigned(string path, string password)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=NetCraft.TPGA, O=NetCraft", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true)); //serverAuth
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        san.AddIpAddress(IPAddress.IPv6Loopback);
        req.CertificateExtensions.Add(san.Build());
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.AddYears(1);
        var cert = req.CreateSelfSigned(notBefore, notAfter);
        var pfx = cert.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(path, pfx);
        return new X509Certificate2(pfx, password, X509KeyStorageFlags.Exportable);
    }

    //GeneratePassword 随机 24 位 base64 密码
    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return Convert.ToBase64String(bytes);
    }
}
