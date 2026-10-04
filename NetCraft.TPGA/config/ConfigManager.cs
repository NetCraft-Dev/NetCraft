using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Config;

//ConfigManager 配置加载管理 两阶段
//阶段1 PreLoadLanguage 仅读 language 容忍 yaml 错误 默认 zh_CN
//阶段2 I18n 初始化后调 Load 完整反序列化+范围校验+触发修复重写
//修复写回用 YamlCommentedWriter 注释来自当前语言 I18n
//yaml key 用 camelCase 与 YamlCommentedWriter 输出一致 配 CamelCaseNamingConvention
public static class ConfigManager
{
    //PreLoadLanguage 阶段1 仅读 language 字段
    //yaml 不存在或格式错误返回默认语言
    public static string PreLoadLanguage(string path)
    {
        if (!File.Exists(path)) return TpgaConfig.DefaultLanguage;
        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var dict = deserializer.Deserialize<Dictionary<string, string>>(yaml);
            var lang = dict != null && dict.TryGetValue("language", out var v) ? v : null;
            return string.IsNullOrWhiteSpace(lang) ? TpgaConfig.DefaultLanguage : lang!;
        }
        catch
        {
            return TpgaConfig.DefaultLanguage;
        }
    }

    //Load 阶段2 完整反序列化 + 范围校验 + 触发修复重写
    //yaml 缺失字段 YamlDotNet 赋 C# 默认值不算越界 但用户看不到新字段
    //对比原始 yaml 文本检查关键 key 是否缺失 缺失即标记 repaired 触发写回
    public static TpgaConfig Load(string path)
    {
        var config = new TpgaConfig();
        if (!File.Exists(path))
        {
            Save(path, config);
            Log.Info("Config", $"config file missing, default generated: {path}");
            return config;
        }

        string? rawYaml = null;
        TpgaConfig? parsed = null;
        try
        {
            rawYaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            parsed = deserializer.Deserialize<TpgaConfig>(rawYaml);
        }
        catch (Exception ex)
        {
            Log.Warning("Config", $"yaml format error, using defaults: {ex.Message}");
        }
        if (parsed != null) config = parsed;

        bool repaired = false;
        //yaml 缺失新字段时 YamlDotNet 赋默认值校验通过但用户看不到新配置
        //检查原始 yaml 文本是否缺少关键 key 缺失则标记修复触发完整写回
        if (rawYaml != null)
        {
            foreach (var key in RequiredKeys)
            {
                if (!rawYaml.Contains(key + ":"))
                {
                    Log.Info("Config", $"config missing key '{key}', will write back");
                    repaired = true;
                    break;
                }
            }
        }
        if (config.MainPort is < 1 or > 65535)
        {
            Log.Warning("Config", $"mainPort={config.MainPort} out of range, reset to {TpgaConfig.DefaultMainPort}");
            config.MainPort = TpgaConfig.DefaultMainPort;
            repaired = true;
        }
        if (config.ApiPort is < 1 or > 65535)
        {
            Log.Warning("Config", $"apiPort={config.ApiPort} out of range, reset to {TpgaConfig.DefaultApiPort}");
            config.ApiPort = TpgaConfig.DefaultApiPort;
            repaired = true;
        }
        if (config.MainPort == config.ApiPort)
        {
            Log.Warning("Config", $"apiPort equals mainPort, reset to {TpgaConfig.DefaultApiPort}");
            config.ApiPort = TpgaConfig.DefaultApiPort;
            repaired = true;
        }
        if (string.IsNullOrWhiteSpace(config.Language)) { config.Language = TpgaConfig.DefaultLanguage; repaired = true; }
        if (string.IsNullOrWhiteSpace(config.LogDirectory)) { config.LogDirectory = TpgaConfig.DefaultLogDirectory; repaired = true; }
        if (string.IsNullOrWhiteSpace(config.Database)) { config.Database = TpgaConfig.DefaultDatabase; repaired = true; }
        if (string.IsNullOrWhiteSpace(config.PlayerDatabase)) { config.PlayerDatabase = TpgaConfig.DefaultPlayerDatabase; repaired = true; }
        if (string.Equals(config.Database, config.PlayerDatabase, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Config", $"playerDatabase equals database, reset to {TpgaConfig.DefaultPlayerDatabase}");
            config.PlayerDatabase = TpgaConfig.DefaultPlayerDatabase;
            repaired = true;
        }
        if (config.CertificatePath == null) { config.CertificatePath = ""; repaired = true; }
        if (config.CertificatePassword == null) { config.CertificatePassword = ""; repaired = true; }
        //OAuth 提供商列表 null 转空列表 过滤掉 clientId/clientSecret 未填全的项 日志告警
        if (config.OAuthProviders == null) { config.OAuthProviders = new(); repaired = true; }
        else
        {
            var valid = config.OAuthProviders.Where(p => p.IsConfigured()).ToList();
            var dropped = config.OAuthProviders.Count - valid.Count;
            if (dropped > 0)
            {
                Log.Warning("Config", $"oauthProviders dropped {dropped} unconfigured entries (clientId/clientSecret empty)");
                config.OAuthProviders = valid;
                repaired = true;
            }
        }

        if (repaired)
        {
            Save(path, config);
            Log.Info("Config", "config repaired and written back");
        }
        return config;
    }

    //Save 用 YamlCommentedWriter 写带 I18n 注释的 yaml
    public static void Save(string path, TpgaConfig config)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, YamlCommentedWriter.Write(config));
    }

    //RequiredKeys yaml 必须包含的 key 缺失则触发写回补全
    private static readonly string[] RequiredKeys =
    [
        "playerDatabase", "serverName", "skinDomains", "oauthProviders"
    ];
}
