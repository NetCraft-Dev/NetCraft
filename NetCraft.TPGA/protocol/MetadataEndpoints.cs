using NetCraft.TPGA.Config;
using NetCraft.TPGA.Crypto;

namespace NetCraft.TPGA.Protocol;

//MetadataBuilder 构造 authlib-injector metadata 响应
//GET / Accept application/json 时返回 含签名公钥与皮肤域名白名单
//authlib-injector 注入时首查此端点 拿公钥验签 textures 属性
public static class MetadataBuilder
{
    //Build 构造 metadata 对象 serverName 与 skinDomains 从配置取
    public static object Build(ProfileKeyStore keys, TpgaConfig config)
    {
        var domains = config.SkinDomains
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (domains.Length == 0) domains = new[] { "127.0.0.1", "localhost" };
        return new
        {
            meta = new
            {
                serverName = string.IsNullOrWhiteSpace(config.ServerName) ? TpgaConfig.DefaultServerName : config.ServerName,
                implementationName = "NetCraft.TPGA",
                implementationVersion = "0.1.0",
                links = new { homepage = $"https://127.0.0.1:{config.MainPort}" }
            },
            signaturePublickey = keys.PublicKeyPem,
            skinDomains = domains
        };
    }
}
