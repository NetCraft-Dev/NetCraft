using System.Text;
using NetCraft.TPGA.Localization;

namespace NetCraft.TPGA.Config;

//YamlCommentedWriter 手工 yaml emitter
//扁平结构按字段顺序写 key: value 每字段前写 I18n 注释
//不用 YamlDotNet 序列化 因其不支持动态语言注释切换
public static class YamlCommentedWriter
{
    //Write 输出 TpgaConfig 为带 I18n 注释的 yaml 文本
    public static string Write(TpgaConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# " + I18n.Get("config.header"));
        sb.AppendLine();
        Field(sb, "config.logDirectory", "logDirectory", Quote(config.LogDirectory));
        Field(sb, "config.language", "language", config.Language);
        Field(sb, "config.database", "database", Quote(config.Database));
        Field(sb, "config.playerDatabase", "playerDatabase", Quote(config.PlayerDatabase));
        Field(sb, "config.mainPort", "mainPort", config.MainPort.ToString());
        Field(sb, "config.apiPort", "apiPort", config.ApiPort.ToString());
        Field(sb, "config.certificatePath", "certificatePath", Quote(config.CertificatePath));
        Field(sb, "config.certificatePassword", "certificatePassword", Quote(config.CertificatePassword));
        Field(sb, "config.serverName", "serverName", Quote(config.ServerName));
        Field(sb, "config.skinDomains", "skinDomains", Quote(config.SkinDomains));
        WriteOAuthProviders(sb, config.OAuthProviders);
        return sb.ToString();
    }

    //Field 写一个字段 注释行 + key: value
    private static void Field(StringBuilder sb, string commentKey, string key, string value)
    {
        sb.AppendLine("# " + I18n.Get(commentKey));
        sb.AppendLine($"{key}: {value}");
    }

    //WriteOAuthProviders 写 OAuth 提供商数组 空写 [] 非空逐项写出 camelCase key 匹配反序列化
    private static void WriteOAuthProviders(StringBuilder sb, List<OAuthProviderConfig>? providers)
    {
        sb.AppendLine("# " + I18n.Get("config.oauthProviders"));
        if (providers == null || providers.Count == 0)
        {
            sb.AppendLine("oauthProviders: []");
            return;
        }
        sb.AppendLine("oauthProviders:");
        foreach (var p in providers)
        {
            sb.AppendLine($"  - type: {p.Type}");
            sb.AppendLine($"    name: {Quote(p.Name)}");
            sb.AppendLine($"    clientId: {Quote(p.ClientId)}");
            sb.AppendLine($"    clientSecret: {Quote(p.ClientSecret)}");
            sb.AppendLine($"    scopes: {Quote(p.Scopes)}");
            //generic type 写端点 预置 type 留空
            sb.AppendLine($"    authorizationEndpoint: {Quote(p.AuthorizationEndpoint)}");
            sb.AppendLine($"    tokenEndpoint: {Quote(p.TokenEndpoint)}");
            sb.AppendLine($"    userInformationEndpoint: {Quote(p.UserInformationEndpoint)}");
        }
    }

    //Quote 字符串值加双引号 转义反斜杠和双引号 防止 Windows 路径破坏 yaml
    private static string Quote(string? value)
    {
        var v = (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "\"" + v + "\"";
    }
}
