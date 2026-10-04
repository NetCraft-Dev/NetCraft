using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Web;

//OAuthEndpoints 玩家 OAuth 登录与补全注册端点 主端口注册
//providers 返回已配置 OAuth 列表 前端按列表渲染按钮
//login 发起 Challenge 重定向到 provider authorize 回调由 OnTicketReceived 拦截
//register GET 取暂存身份预填 POST 建账户绑定 OAuth 签 session
//所有端点公开 不需先登录 由 PlayerAuthMiddleware 放行
public static class OAuthEndpoints
{
    //MapOAuthEndpoints 注册 /api/player/oauth/* 与 /api/player/register 路由
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/player/oauth/providers", ProvidersHandler);
        app.MapGet("/api/player/oauth/{key}/login", LoginHandler);
        app.MapGet("/api/player/register", RegisterGetHandler);
        app.MapPost("/api/player/register", RegisterPostHandler);
        return app;
    }

    //ProvidersHandler 返回已注册 OAuth 提供商列表 前端按此渲染按钮
    private static async Task ProvidersHandler(HttpContext ctx, OAuthProviderRegistry registry)
    {
        var list = registry.Providers.Select(p => new { key = p.key, name = p.name });
        await PlayerEndpoints.WriteJsonAsync(ctx, 200, new { ok = true, providers = list });
    }

    //LoginHandler 发起 OAuth Challenge 重定向到 provider authorize url
    //key 路由参数标识 provider 实际 scheme 由 registry 查 回调后 OnTicketReceived 拦截
    private static async Task LoginHandler(HttpContext ctx, string key, OAuthProviderRegistry registry)
    {
        var entry = registry.Find(key);
        if (entry == null)
        {
            await PlayerEndpoints.WriteJsonAsync(ctx, 404, "provider_not_found", $"oauth provider {key} not registered");
            return;
        }
        var props = new AuthenticationProperties { RedirectUri = "/" };
        await ctx.ChallengeAsync(entry.Value.scheme, props);
    }

    //RegisterGetHandler 取暂存 OAuth 身份 预填补全页 username/email
    //token 无效或过期返回 400 前端跳回登录
    private static async Task RegisterGetHandler(HttpContext ctx, PendingOAuthStore pending, string? token)
    {
        var p = pending.Find(token);
        if (p == null)
        {
            await PlayerEndpoints.WriteJsonAsync(ctx, 400, "invalid_token", "pending oauth token invalid or expired");
            return;
        }
        await PlayerEndpoints.WriteJsonAsync(ctx, 200, new
        {
            ok = true,
            provider = p.Provider,
            username = p.Username,
            email = p.Email
        });
    }

    //RegisterPostHandler 补全注册 建 OAuth 账户绑定 provider+subject 签 session
    //token 关联暂存身份 username 必填可改 email 选填 空串清空
    private static async Task RegisterPostHandler(HttpContext ctx, PendingOAuthStore pending, UserRepository users, PlayerSessionStore sessions)
    {
        var req = await ctx.Request.ReadFromJsonAsync<RegisterRequest>();
        if (req == null || string.IsNullOrEmpty(req.Token) || string.IsNullOrEmpty(req.Username))
        {
            await PlayerEndpoints.WriteJsonAsync(ctx, 400, "fields_required", "token and username required");
            return;
        }
        var p = pending.Find(req.Token);
        if (p == null)
        {
            await PlayerEndpoints.WriteJsonAsync(ctx, 400, "invalid_token", "pending oauth token invalid or expired");
            return;
        }
        //username 查重
        if (users.FindByUsername(req.Username) != null)
        {
            await PlayerEndpoints.WriteJsonAsync(ctx, 409, "username_exists", "username already exists");
            return;
        }
        //email 空串清空(NULL) 非空查重
        string? email = string.IsNullOrEmpty(req.Email) ? null : req.Email;
        if (email != null && users.FindByEmail(email) != null)
        {
            await PlayerEndpoints.WriteJsonAsync(ctx, 409, "email_exists", "email already exists");
            return;
        }
        //建 OAuth 账户 password_hash NULL 绑定 oauth_provider/oauth_subject
        var uuid = Guid.NewGuid().ToString("N");
        var id = users.CreateOAuthUser(uuid, req.Username, email, p.Provider, p.Subject);
        var user = users.FindById(id)!;
        pending.Remove(req.Token);
        PlayerEndpoints.SignInPlayer(ctx, user, sessions);
        Log.Info("OAuth", $"register ok user={user.Username} provider={p.Provider} ip={ctx.Connection.RemoteIpAddress}");
        await PlayerEndpoints.WriteJsonAsync(ctx, 200, new { ok = true });
    }

    //RegisterRequest 补全注册请求 token 关联暂存身份 username 必填 email 选填
    public sealed class RegisterRequest
    {
        public string Token { get; set; } = "";
        public string Username { get; set; } = "";
        public string? Email { get; set; }
    }
}
