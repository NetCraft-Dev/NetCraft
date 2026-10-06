using System.Security.Cryptography;

namespace NetCraft.Network;

//CryptoHelper packet encryption helper, maps to vanilla net.minecraft.network.CipherEncoder/CipherDecoder
//Vanilla fully implements AES/CFB8; here an AES-CFB placeholder aligns round-trip
//Key and IV are identical, aligning with the vanilla Minecraft convention
public static class CryptoHelper
{
    //Encrypt encrypts data with AES-CFB
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

    //Decrypt decrypts data with AES-CFB
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
