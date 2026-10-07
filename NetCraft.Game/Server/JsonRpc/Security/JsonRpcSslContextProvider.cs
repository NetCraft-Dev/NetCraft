using System.Security.Cryptography.X509Certificates;
using NetCraft.Logging;

namespace NetCraft.Game.Server.JsonRpc.Security;

//JsonRpcSslContextProvider manages service TLS certificate loading, maps to vanilla net.minecraft.server.jsonrpc.security.JsonRpcSslContextProvider
//Vanilla uses netty SslContext; this uses X509Certificate2 loaded directly from a PKCS12 keystore
public static class JsonRpcSslContextProvider
{
    private const string PasswordEnvVariableKey = "MINECRAFT_MANAGEMENT_TLS_KEYSTORE_PASSWORD";
    private const string PasswordAppContextKey = "management.tls.keystore.password";

    //CreateFrom loads the server certificate from a PKCS12 keystore
    //An empty path or missing file throws an argument exception; password priority: environment variable > AppContext > server.properties
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

    //GetKeystorePassword takes the keystore password by priority; falls back to the server.properties value when none is set
    private static string? GetKeystorePassword(string keystorePasswordFromServerProperties)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(PasswordEnvVariableKey);
        if (fromEnvironment is not null) return fromEnvironment;
        if (AppContext.GetData(PasswordAppContextKey) is string fromAppContext) return fromAppContext;
        return keystorePasswordFromServerProperties;
    }

    //PrintInstructions prints the steps needed to enable TLS
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
