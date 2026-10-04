using System.Security.Cryptography.X509Certificates;
using NetCraft.Logging;

namespace NetCraft.Game.Server.JsonRpc.Security;

//JsonRpcSslContextProvider 管理服务 TLS 证书加载 对应原版 net.minecraft.server.jsonrpc.security.JsonRpcSslContextProvider
//原版基于 netty SslContext 这里用 X509Certificate2 直接从 PKCS12 密钥库加载
public static class JsonRpcSslContextProvider
{
    private const string PasswordEnvVariableKey = "MINECRAFT_MANAGEMENT_TLS_KEYSTORE_PASSWORD";
    private const string PasswordAppContextKey = "management.tls.keystore.password";

    //CreateFrom 从 PKCS12 密钥库加载服务端证书
    //路径为空或文件不存在直接抛参数异常 密码优先级 环境变量 > AppContext > server.properties
    public static X509Certificate2 CreateFrom(string keystorePath, string keystorePasswordFromServerProperties)
    {
        if (string.IsNullOrEmpty(keystorePath))
        {
            throw new ArgumentException("TLS is enabled but keystore is not configured");
        }
        if (!File.Exists(keystorePath) || Directory.Exists(keystorePath))
        {
            throw new ArgumentException($"Supplied keystore is not a file or does not exist: '{keystorePath}'");
        }
        var password = GetKeystorePassword(keystorePasswordFromServerProperties);
        return X509CertificateLoader.LoadPkcs12FromFile(keystorePath, password, X509KeyStorageFlags.Exportable);
    }

    //GetKeystorePassword 按优先级取密钥库口令 都没配则回退 server.properties 的值
    private static string? GetKeystorePassword(string keystorePasswordFromServerProperties)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(PasswordEnvVariableKey);
        if (fromEnvironment is not null) return fromEnvironment;
        if (AppContext.GetData(PasswordAppContextKey) is string fromAppContext) return fromAppContext;
        return keystorePasswordFromServerProperties;
    }

    //PrintInstructions 打印启用 TLS 所需步骤
    public static void PrintInstructions()
    {
        Log.Info("To use TLS for the management server, please follow these steps:");
        Log.Info("1. Set the server property 'management-server-tls-enabled' to 'true' to enable TLS");
        Log.Info("2. Create a keystore file of type PKCS12 containing your server certificate and private key");
        Log.Info("3. Set the server property 'management-server-tls-keystore' to the path of your keystore file");
        Log.Info("4. Set the keystore password via the environment variable 'MINECRAFT_MANAGEMENT_TLS_KEYSTORE_PASSWORD', or system property 'management.tls.keystore.password', or server property 'management-server-tls-keystore-password'");
        Log.Info("5. Restart the server to apply the changes.");
    }
}
