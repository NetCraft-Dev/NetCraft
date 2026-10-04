using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Config;
using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Web;

//OAuthSetup OAuth handler 动态注册与回调桥接
//启动时遍历 config.OAuthProviders 按 type 注册 GitHub 预置或 generic 通用 handler
//OnTicketReceived 拦截 OAuth 完成事件 拿 userInfo 查本地绑定 已绑签 session 未绑暂存跳补全页
//不依赖 ASP.NET Core 标准 SignIn 签发外部 cookie 改用现有 PlayerSessionStore 桥接
//RemoteAuthenticationHandler.HandleAuthenticateAsync 调 AuthenticateAsync(SignInScheme) 
//SignInScheme 默认取 DefaultScheme 若是 OAuth handler 自身则无限递归 Stack overflow
//注册占位 cookie scheme 作 SignInScheme 打断递归 OnTicketReceived 里 HandleResponse 不签 cookie
public static class OAuthSetup
{
    //占位 cookie scheme 名 OAuth handler 的 SignInScheme 指向此 防止递归
    private const string OAuthCookieScheme = "OAuthCookies";

    //RegisterHandlers 遍历 config.OAuthProviders 动态注册 OAuth handler 填 registry
    //github 走预置 AddGitHub generic 走通用 AddOAuth 填端点 其他 type 日志告警跳过
    public static void RegisterHandlers(AuthenticationBuilder authBuilder, TpgaConfig config, OAuthProviderRegistry registry)
    {
        //占位 cookie scheme 不实际使用 仅作 OAuth handler 的 SignInScheme 防递归
        authBuilder.AddCookie(OAuthCookieScheme);

        int genericIdx = 0;
        foreach (var p in config.OAuthProviders)
        {
            if (!p.IsConfigured()) continue;
            var type = (p.Type ?? "").Trim().ToLowerInvariant();
            //key 前端按钮标识 github 唯一 generic 用 generic0/generic1 区分 重启配置顺序不变则稳定
            string key = type switch
            {
                "github" => "github",
                "generic" => "generic" + genericIdx++,
                _ => ""
            };
            if (string.IsNullOrEmpty(key))
            {
                Log.Warning("Boot", $"oauth type={p.Type} name={p.Name} not supported, skipped");
                continue;
            }
            var scheme = key;
            var captured = p;
            switch (type)
            {
                case "github":
                    authBuilder.AddGitHub(scheme, o =>
                    {
                        o.ClientId = captured.ClientId;
                        o.ClientSecret = captured.ClientSecret;
                        o.SignInScheme = OAuthCookieScheme;
                        //回调路径 /signin-{key} OAuth handler 自动监听
                        o.CallbackPath = $"/signin-{key}";
                        o.Scope.Clear();
                        if (!string.IsNullOrEmpty(captured.Scopes))
                            foreach (var s in captured.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                                o.Scope.Add(s.Trim());
                        else
                            o.Scope.Add("user:email");
                        o.Events = new OAuthEvents { OnTicketReceived = ctx => OnTicketReceived(ctx, key) };
                    });
                    break;
                case "generic":
                    authBuilder.AddOAuth(scheme, o =>
                    {
                        o.ClientId = captured.ClientId;
                        o.ClientSecret = captured.ClientSecret;
                        o.SignInScheme = OAuthCookieScheme;
                        o.AuthorizationEndpoint = captured.AuthorizationEndpoint;
                        o.TokenEndpoint = captured.TokenEndpoint;
                        o.UserInformationEndpoint = captured.UserInformationEndpoint;
                        o.CallbackPath = $"/signin-{key}";
                        o.Scope.Clear();
                        if (!string.IsNullOrEmpty(captured.Scopes))
                            foreach (var s in captured.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                                o.Scope.Add(s.Trim());
                        //generic userInfo 字段映射 兼容 sub/id/email/name/preferred_username/login
                        o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                        o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                        o.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                        o.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                        o.ClaimActions.MapJsonKey(ClaimTypes.Name, "preferred_username");
                        o.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
                        o.Events = new OAuthEvents { OnTicketReceived = ctx => OnTicketReceived(ctx, key) };
                    });
                    break;
            }
            registry.Register(key, scheme, p);
            Log.Info("Boot", $"oauth registered key={key} type={type} name={p.Name}");
        }
    }

    //OnTicketReceived OAuth 完成事件拦截 拿 userInfo claims 查绑定签 session 或暂存跳补全页
    //ctx.Principal 由 handler 填充 含 NameIdentifier(subject)/Email/Name(username)
    //HandleResponse 阻止默认 SignIn 签发外部 cookie 改用 PlayerSessionStore 桥接
    private static async Task OnTicketReceived(TicketReceivedContext ctx, string providerKey)
    {
        var principal = ctx.Principal!;
        var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        var username = principal.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            Log.Warning("OAuth", $"callback no subject provider={providerKey}");
            ctx.Response.Redirect("/login?error=oauth_no_subject");
            ctx.HandleResponse();
            return;
        }
        var httpCtx = ctx.HttpContext;
        var users = httpCtx.RequestServices.GetRequiredService<UserRepository>();
        var sessions = httpCtx.RequestServices.GetRequiredService<PlayerSessionStore>();
        var pending = httpCtx.RequestServices.GetRequiredService<PendingOAuthStore>();
        var user = users.FindByOAuthSubject(providerKey, subject);
        if (user != null)
        {
            //已绑定 直接签 session 跳首页 账户禁用跳登录
            if (!user.Enabled)
            {
                Log.Info("OAuth", $"login disabled user={user.Username} provider={providerKey}");
                ctx.Response.Redirect("/login?error=account_disabled");
                ctx.HandleResponse();
                await Task.CompletedTask;
                return;
            }
            PlayerEndpoints.SignInPlayer(httpCtx, user, sessions);
            Log.Info("OAuth", $"login ok user={user.Username} provider={providerKey}");
            ctx.Response.Redirect("/");
            ctx.HandleResponse();
            await Task.CompletedTask;
            return;
        }
        //未绑定 暂存 OAuth 身份 跳补全注册页 前端带 token 建账户
        var token = Guid.NewGuid().ToString("N");
        pending.Add(new PendingOAuth
        {
            Token = token,
            Provider = providerKey,
            Subject = subject,
            Username = username ?? "",
            Email = email
        });
        Log.Info("OAuth", $"pending register provider={providerKey} subject={subject} user={username}");
        ctx.Response.Redirect("/register?token=" + token);
        ctx.HandleResponse();
        await Task.CompletedTask;
    }
}
