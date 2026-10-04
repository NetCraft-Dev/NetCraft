namespace NetCraft.TPGA.Config;

//OAuthProviderConfig 单个 OAuth 提供商配置
//type 决定走哪个 handler github 预置 AddGitHub generic 走通用 AddOAuth 填端点
//name 前端按钮显示名 与 type 解耦 方便配多个 generic
//scopes 空则用 provider 默认 github 默认 user:email google 默认 openid profile email
public sealed class OAuthProviderConfig
{
    //provider 类型 github 走预置包 generic 走通用 OAuth2 handler 填端点
    public string Type { get; set; } = "generic";
    //前端按钮显示名 如 GitHub Google 自建Keycloak
    public string Name { get; set; } = "";
    //OAuth 应用 clientId 提供商控制台申请
    public string ClientId { get; set; } = "";
    //OAuth 应用 clientSecret 切勿前端泄露
    public string ClientSecret { get; set; } = "";
    //授权范围 空用默认 逗号分隔 github 默认 user:email
    public string Scopes { get; set; } = "";
    //以下仅 generic 必填 github 等预置 type 忽略
    //授权端点 GET 浏览器跳转拿 code
    public string AuthorizationEndpoint { get; set; } = "";
    //令牌端点 POST code 换 access_token
    public string TokenEndpoint { get; set; } = "";
    //用户信息端点 GET 拿用户身份 subject/email
    public string UserInformationEndpoint { get; set; } = "";

    //IsConfigured clientId 与 clientSecret 都非空才视为启用 启动时过滤未配置项
    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
