using System.Security.Cryptography;
using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Crypto;

//ProfileKeyStore textures 签名用持久化 RSA 密钥对
//重启后签名仍有效 profile_key.pem 存程序根目录 私钥勿外泄
public sealed class ProfileKeyStore
{
    private const string KeyFileName = "profile_key.pem";
    private readonly RSA _rsa;

    public ProfileKeyStore()
    {
        var path = Path.Combine(AppContext.BaseDirectory, KeyFileName);
        var rsa = RSA.Create();
        if (File.Exists(path))
        {
            try
            {
                rsa.ImportFromPem(File.ReadAllText(path));
                Log.Info("ProfileKey", "loaded persisted profile signing key");
                _rsa = rsa;
                return;
            }
            catch (Exception e)
            {
                Log.Warning("ProfileKey", $"load failed regenerating: {e.Message}");
            }
        }
        rsa = RSA.Create(2048);
        File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem());
        Log.Info("ProfileKey", "generated and saved profile signing key");
        _rsa = rsa;
    }

    //Sign 对 data 做 SHA256withRSA 签名 返回字节数组
    public byte[] Sign(byte[] data)
        => _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    //PublicKeyPem 导出公钥 PEM 客户端验签用
    public string PublicKeyPem
    {
        get
        {
            var pub = _rsa.ExportSubjectPublicKeyInfo();
            return new string(PemEncoding.WriteString("PUBLIC KEY", pub));
        }
    }
}
