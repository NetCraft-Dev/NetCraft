using NetCraft.TPGA.Config;

namespace NetCraft.TPGA.Web;

//OAuthProviderRegistry OAuth 提供商注册表
//启动时遍历 config.OAuthProviders 按 type 生成 key 注册 handler 与 config 映射
//key 前端用 scheme 后端 OAuth handler 用 name 显示名
//github 唯一 key=github 多个 generic 用 generic0/generic1 区分
public sealed class OAuthProviderRegistry
{
    private readonly Dictionary<string, (string scheme, OAuthProviderConfig config)> _byKey = new();
    private readonly List<(string key, string name)> _list = new();

    //Register 注册一个 provider key 前端按钮标识 scheme OAuth handler 标识
    public void Register(string key, string scheme, OAuthProviderConfig config)
    {
        _byKey[key] = (scheme, config);
        _list.Add((key, string.IsNullOrEmpty(config.Name) ? key : config.Name));
    }

    //Find 按 key 查 scheme 与 config 未注册返回 null
    public (string scheme, OAuthProviderConfig config)? Find(string? key)
        => !string.IsNullOrEmpty(key) && _byKey.TryGetValue(key, out var v) ? v : null;

    //List 已注册列表 供 GET /api/player/oauth/providers 返回前端渲染按钮
    public IReadOnlyList<(string key, string name)> Providers => _list;
}
