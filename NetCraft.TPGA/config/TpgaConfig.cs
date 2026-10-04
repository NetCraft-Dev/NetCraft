namespace NetCraft.TPGA.Config;

using YamlDotNet.Serialization;

//TpgaConfig 配置 POCO 对应根目录 tpga.yaml
//字段范围越界由 ConfigManager.Load 校验回默认值
//字段顺序即 yaml 写出顺序 由 YamlCommentedWriter 固定
//YamlMember 显式指定 yaml key 名 避免 CamelCaseNamingConvention 对连续大写(OA)映射错误
public sealed class TpgaConfig
{
    //默认值常量 供 ConfigManager 修复和首启生成引用
    public const string DefaultLogDirectory = "logs";
    public const string DefaultLanguage = "zh_CN";
    public const string DefaultDatabase = "sqlite:tpga.db";
    public const string DefaultPlayerDatabase = "sqlite:tpga_players.db";
    public const int DefaultMainPort = 25565;
    public const int DefaultApiPort = 25566;
    public const string DefaultCertificatePath = "";
    public const string DefaultCertificatePassword = "";
    public const string DefaultServerName = "NetCraft.TPGA";
    public const string DefaultSkinDomains = "127.0.0.1,localhost";
    //OAuth 提供商默认空列表 留空即禁用 OAuth 登录 仅密码登录
    public static readonly List<OAuthProviderConfig> DefaultOAuthProviders = new();

    //日志目录 相对程序工作目录
    public string LogDirectory { get; set; } = DefaultLogDirectory;
    //界面语言 en_US / zh_CN
    public string Language { get; set; } = DefaultLanguage;
    //数据库 sqlite:文件路径 或其他前缀连接字符串 postgres:Host=...
    public string Database { get; set; } = DefaultDatabase;
    //玩家数据库 与主数据库隔离 存游戏账户令牌档案与服务器加入记录
    public string PlayerDatabase { get; set; } = DefaultPlayerDatabase;
    //主服务端口 Yggdrasil 验证 API
    public int MainPort { get; set; } = DefaultMainPort;
    //API 服务端口 wss + 管理后台
    public int ApiPort { get; set; } = DefaultApiPort;
    //HTTPS/WSS 证书路径 留空自动生成自签名证书
    public string CertificatePath { get; set; } = DefaultCertificatePath;
    //证书密码 PFX 私钥保护 自动生成时随机生成并写回
    public string CertificatePassword { get; set; } = DefaultCertificatePassword;
    //服务器名称 metadata 与日志展示用
    public string ServerName { get; set; } = DefaultServerName;
    //皮肤域名白名单 逗号分隔 metadata skinDomains 客户端按此加载贴图
    public string SkinDomains { get; set; } = DefaultSkinDomains;
    //OAuth 提供商列表 配置了几个前端就显示几个登录按钮 留空禁用 OAuth
    //type=github 走预置包 type=generic 填 authorizationEndpoint/tokenEndpoint/userInformationEndpoint
    //YamlMember 显式映射 CamelCase 对连续大写(OA)映射为 oAuth 非 oauth 需手动对齐
    [YamlMember(Alias = "oauthProviders")]
    public List<OAuthProviderConfig> OAuthProviders { get; set; } = new();
}
