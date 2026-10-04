using System.Security.Cryptography;

namespace NetCraft.TPGA.Crypto;

//RsaKeyProvider 应用层加密 RSA 密钥对
//wss 握手时下发给客户端公钥 客户端加密身份 服务端用私钥解密
//MVP 内存生成 启动即生成不持久化 重启后公钥变化客户端需重新协商
public sealed class RsaKeyProvider
{
    private readonly RSA _rsa;

    public RsaKeyProvider()
    {
        _rsa = RSA.Create(2048);
    }

    //PublicKeyPem 导出公钥 PEM 字符串 下发给客户端
    public string PublicKeyPem
    {
        get
        {
            var pub = _rsa.ExportSubjectPublicKeyInfo();
            return new string(PemEncoding.WriteString("PUBLIC KEY", pub));
        }
    }

    //Decrypt 解密客户端用公钥加密的身份包
    public byte[] Decrypt(byte[] ciphertext)
        => _rsa.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256);
}
