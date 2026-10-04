using System.Security.Cryptography;

namespace NetCraft.Network;

//CryptoHelper 包加密辅助对应原版 net.minecraft.network.CipherEncoder/CipherDecoder
//原版用 AES/CFB8 完整实现这里用 AES-CFB 占位对齐 round-trip
//Key 与 IV 相同对齐原版 Minecraft 约定
public static class CryptoHelper
{
    //Encrypt 加密数据用 AES-CFB
    public static byte[] Encrypt(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = key;
        aes.Mode = CipherMode.CFB;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }

    //Decrypt 解密数据用 AES-CFB
    public static byte[] Decrypt(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = key;
        aes.Mode = CipherMode.CFB;
        aes.Padding = PaddingMode.None;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, data.Length);
    }
}
