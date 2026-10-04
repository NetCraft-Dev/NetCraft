using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Logging;
using NetCraft.TPGA.Db;

namespace NetCraft.TPGA.Web;

//PlayerEndpoints 主端口玩家自服务 API 端点
//login 按 username 或 email 查玩家 PBKDF2 校验密码 创建 session 下发 cookie
//logout 清除 session 与 cookie
//session 返回当前玩家档案概览 前端刷新时拿状态
//session cookie 名 player_sid 与 admin 的 sid 独立 避免互相覆盖
//登录失败统一 invalid_credentials 不泄露账户存在性
public static class PlayerEndpoints
{
    public const string SessionCookie = "player_sid";

    //MapPlayerEndpoints 注册 /api/player/* 路由 在主端口分支调用
    //login/logout/session 公开端点 profile/password 受 PlayerAuthMiddleware 保护
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/player/login", LoginHandler);
        app.MapPost("/api/player/logout", LogoutHandler);
        app.MapGet("/api/player/session", SessionHandler);
        app.MapGet("/api/player/profile", ProfileHandler);
        app.MapPut("/api/player/profile", UpdateProfileHandler);
        app.MapPut("/api/player/password", ChangePasswordHandler);
        app.MapPut("/api/player/preferences", UpdatePreferencesHandler);
        return app;
    }

    //LoginHandler username/password 校验 创建 session 下发 cookie
    //登录失败统一 invalid_credentials 不泄露账户存在 不加 IP 限流 暂留 等阶段 6 安全加固
    private static async Task LoginHandler(HttpContext ctx, UserRepository users, PlayerSessionStore sessions)
    {
        var req = await ctx.Request.ReadFromJsonAsync<LoginRequest>();
        if (req == null || string.IsNullOrEmpty(req.Username) || string.IsNullOrEmpty(req.Password))
        {
            await WriteJsonAsync(ctx, 400, "fields_required", "username and password required");
            return;
        }
        var user = users.FindByLogin(req.Username);
        //不存在/禁用/密码错 统一 invalid_credentials 不泄露账户存在
        if (user == null || !user.Enabled || !PasswordHasher.VerifyGameAccount(req.Password, user.PasswordHash))
        {
            Log.Info("Player", $"login fail user={req.Username} ip={ctx.Connection.RemoteIpAddress}");
            await WriteJsonAsync(ctx, 401, "invalid_credentials", "invalid username or password");
            return;
        }
        SignInPlayer(ctx, user, sessions);
        Log.Info("Player", $"login ok user={user.Username} ip={ctx.Connection.RemoteIpAddress}");
        await WriteJsonAsync(ctx, 200, new { ok = true, username = user.Username, uuid = user.Uuid });
    }

    //LogoutHandler 清除 session 与 cookie
    private static async Task LogoutHandler(HttpContext ctx, PlayerSessionStore sessions)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        if (!string.IsNullOrEmpty(sid)) sessions.Remove(sid);
        ctx.Response.Cookies.Delete(SessionCookie);
        await WriteJsonAsync(ctx, 200, new { ok = true });
    }

    //SessionHandler 返回当前登录玩家档案概览 前端刷新时拿状态
    //未登录返回 ok=false 不报错 前端按 ok 字段判断跳转
    private static async Task SessionHandler(HttpContext ctx, PlayerSessionStore sessions, UserRepository users)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        var session = sessions.Find(sid);
        if (session == null)
        {
            await WriteJsonAsync(ctx, 200, new { ok = false });
            return;
        }
        var user = users.FindById(session.UserId);
        if (user == null || !user.Enabled)
        {
            //玩家被删或禁 session 失效
            sessions.Remove(sid!);
            ctx.Response.Cookies.Delete(SessionCookie);
            await WriteJsonAsync(ctx, 200, new { ok = false });
            return;
        }
        await WriteJsonAsync(ctx, 200, new
        {
            ok = true,
            username = user.Username,
            uuid = user.Uuid,
            email = user.Email,
            emailVerified = user.EmailVerified,
            language = user.Language,
            theme = user.Theme
        });
    }

    //WriteSessionCookie 下发 cookie HttpOnly+Secure+SameSite=Lax
    //Secure 要求 HTTPS 主端口已 HTTPS 自签证书
    private static void WriteSessionCookie(HttpContext ctx, string sid)
        => ctx.Response.Cookies.Append(SessionCookie, sid, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

    //SignInPlayer 创建 session 并下发 cookie 供 LoginHandler 与 OAuth 回调复用
    internal static void SignInPlayer(HttpContext ctx, UserAccount user, PlayerSessionStore sessions)
    {
        var session = sessions.Create(user.Id, user.Username, user.Uuid);
        WriteSessionCookie(ctx, session.SessionId);
    }

    internal static async Task WriteJsonAsync(HttpContext ctx, int status, object data)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(data);
    }

    internal static async Task WriteJsonAsync(HttpContext ctx, int status, string error, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync($"{{\"ok\":false,\"error\":\"{error}\",\"message\":\"{message}\"}}");
    }

    public sealed class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    //ProfileHandler 返回当前玩家完整档案 中间件已校验 session 有效 user 必非空
    //含皮肤元数据 skinHash/skinModel/capeHash 前端 3D 预览用
    private static async Task ProfileHandler(HttpContext ctx, PlayerSessionStore sessions, UserRepository users, ProfileRepository profiles)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        var session = sessions.Find(sid);
        var user = users.FindById(session!.UserId)!;
        var meta = profiles.GetTextureMeta(user.Id);
        await WriteJsonAsync(ctx, 200, new
        {
            ok = true,
            id = user.Id,
            uuid = user.Uuid,
            username = user.Username,
            email = user.Email,
            emailVerified = user.EmailVerified,
            oauthProvider = user.OauthProvider,
            createdAt = user.CreatedAt,
            skinHash = meta.SkinHash,
            skinModel = meta.SkinModel,
            capeHash = meta.CapeHash
        });
    }

    //UpdateProfileHandler PUT 修改 username/email
    //username 非空且不同 查重后 UpdateUsername session 中 username 同步更新
    //email null 不变 空串清空 非空查重 与 AdminEndpoints 一致
    private static async Task UpdateProfileHandler(HttpContext ctx, PlayerSessionStore sessions, UserRepository users)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        var session = sessions.Find(sid);
        var user = users.FindById(session!.UserId)!;
        var req = await ctx.Request.ReadFromJsonAsync<UpdateProfileRequest>();
        if (req == null)
        {
            await WriteJsonAsync(ctx, 400, "fields_required", "request body required");
            return;
        }
        //username 修改
        if (!string.IsNullOrEmpty(req.Username) && req.Username != user.Username)
        {
            if (users.FindByUsername(req.Username) != null)
            {
                await WriteJsonAsync(ctx, 409, "username_exists", "username already exists");
                return;
            }
            users.UpdateUsername(user.Id, req.Username);
            session.Username = req.Username;
        }
        //email null 不变 空串清空 非空查重
        if (req.Email != null && req.Email != (user.Email ?? ""))
        {
            if (!string.IsNullOrEmpty(req.Email) && users.FindByEmail(req.Email) != null)
            {
                await WriteJsonAsync(ctx, 409, "email_exists", "email already exists");
                return;
            }
            users.UpdateEmail(user.Id, string.IsNullOrEmpty(req.Email) ? null : req.Email);
        }
        Log.Info("Player", $"profile updated id={user.Id} user={user.Username}");
        await WriteJsonAsync(ctx, 200, new { ok = true });
    }

    //ChangePasswordHandler PUT 修改密码 旧密码校验 新密码至少 4 位
    private static async Task ChangePasswordHandler(HttpContext ctx, PlayerSessionStore sessions, UserRepository users)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        var session = sessions.Find(sid);
        var user = users.FindById(session!.UserId)!;
        var req = await ctx.Request.ReadFromJsonAsync<ChangePasswordRequest>();
        if (req == null || string.IsNullOrEmpty(req.OldPassword) || string.IsNullOrEmpty(req.NewPassword))
        {
            await WriteJsonAsync(ctx, 400, "fields_required", "old and new password required");
            return;
        }
        if (!PasswordHasher.VerifyGameAccount(req.OldPassword, user.PasswordHash))
        {
            await WriteJsonAsync(ctx, 401, "old_password_mismatch", "old password incorrect");
            return;
        }
        if (req.NewPassword.Length < 4)
        {
            await WriteJsonAsync(ctx, 400, "password_too_short", "password must be at least 4 characters");
            return;
        }
        users.UpdatePassword(user.Id, PasswordHasher.HashGameAccount(req.NewPassword));
        Log.Info("Player", $"password changed id={user.Id} user={user.Username}");
        await WriteJsonAsync(ctx, 200, new { ok = true });
    }

    public sealed class UpdateProfileRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
    }

    public sealed class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    //UpdatePreferencesHandler PUT 修改语言/主题偏好
    //language 非空时须是已知语言代码 theme 非空时须是 light/dark
    //传 null 不改 传空串清空(回退到浏览器/系统默认)
    private static async Task UpdatePreferencesHandler(HttpContext ctx, PlayerSessionStore sessions, UserRepository users)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        var session = sessions.Find(sid);
        var user = users.FindById(session!.UserId)!;
        var req = await ctx.Request.ReadFromJsonAsync<UpdatePreferencesRequest>();
        if (req == null)
        {
            await WriteJsonAsync(ctx, 400, "fields_required", "request body required");
            return;
        }
        //language 校验 非空时须是 zh_CN 或 en_US
        if (req.Language != null && req.Language != "" && req.Language != "zh_CN" && req.Language != "en_US")
        {
            await WriteJsonAsync(ctx, 400, "invalid_language", "language must be zh_CN or en_US");
            return;
        }
        //theme 校验 非空时须是 light 或 dark
        if (req.Theme != null && req.Theme != "" && req.Theme != "light" && req.Theme != "dark")
        {
            await WriteJsonAsync(ctx, 400, "invalid_theme", "theme must be light or dark");
            return;
        }
        var lang = string.IsNullOrEmpty(req.Language) ? null : req.Language;
        var theme = string.IsNullOrEmpty(req.Theme) ? null : req.Theme;
        users.UpdatePreferences(user.Id, lang, theme);
        Log.Info("Player", $"preferences updated id={user.Id} user={user.Username} lang={lang} theme={theme}");
        await WriteJsonAsync(ctx, 200, new { ok = true });
    }

    public sealed class UpdatePreferencesRequest
    {
        public string? Language { get; set; }
        public string? Theme { get; set; }
    }
}
