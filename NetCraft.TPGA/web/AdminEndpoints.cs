using Microsoft.AspNetCore.Http;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Logging;
using NetCraft.TPGA.Storage;

namespace NetCraft.TPGA.Web;

//AdminEndpoints 管理后台 API 路由 挂根路径
//HTML 页面由 webui React 提供 WebUIEndpoints 返回 admin.html 此处仅保留数据 API
//session cookie 认证 /api/* 由 AdminAuthMiddleware 拦截 /login /logout /password POST 内部校验
//错误统一返回 error 错误码 前端按当前语言翻译 webui.error.{code} message 为英文兜底
//API 账户支持改密与禁用 玩家支持禁用 增删查 管理员语言偏好持久化
public static class AdminEndpoints
{
    //session cookie 名
    public const string SessionCookie = "tpga_session";

    //MapAdminEndpoints 注册后台 API 路由 挂根路径
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        //登录与改密 POST 不经 middleware 内部校验
        app.MapPost("/login", LoginAsync);
        app.MapPost("/logout", Logout);
        app.MapPost("/password", ChangePasswordAsync);
        //session 检查 must_change 状态下 middleware 放行此端点 前端刷新后据此跳 /password
        app.MapGet("/api/session", GetSession);
        //管理员语言偏好 持久化到 admin_accounts.language
        app.MapPost("/api/language", SetLanguageAsync);
        //api 账户管理 /api/* 由 middleware 校验 session
        app.MapGet("/api/accounts", ListApiAccounts);
        app.MapPost("/api/accounts", AddApiAccount);
        app.MapDelete("/api/accounts/{id}", DeleteApiAccount);
        app.MapPost("/api/accounts/{id}/password", ChangeApiPassword);
        app.MapPost("/api/accounts/{id}/enabled", ToggleApiEnabled);
        //游戏用户管理
        app.MapGet("/api/players", ListPlayers);
        app.MapPost("/api/players", AddPlayer);
        app.MapDelete("/api/players/{id}", DeletePlayer);
        app.MapPost("/api/players/{id}/enabled", TogglePlayerEnabled);
        //修改玩家 username/uuid 重置密码
        app.MapPut("/api/players/{id}", UpdatePlayerAsync);
        app.MapPut("/api/players/{id}/password", ChangePlayerPasswordAsync);
        //皮肤管理 预览 上传 改类型 删除
        app.MapGet("/api/players/{id}/skin", GetPlayerSkin);
        app.MapPut("/api/players/{id}/skin", UploadPlayerSkinAsync);
        app.MapPatch("/api/players/{id}/skin-model", ChangePlayerSkinModelAsync);
        app.MapDelete("/api/players/{id}/skin", DeletePlayerSkin);
    }

    //LoginAsync 校验管理员凭证 创建 session 下发 cookie
    private static async Task<IResult> LoginAsync(HttpContext ctx, AdminRepository admins, AdminSessionStore sessions)
    {
        var req = await ctx.ReadBodyAsync<LoginRequest>();
        var admin = admins.FindByUsername(req.Username);
        if (admin == null || !PasswordHasher.VerifyAdmin(req.Password, admin.PasswordHash))
        {
            Log.Warning("Admin", $"login fail user={req.Username}");
            return Fail(403, "invalid_credentials", "invalid username or password");
        }
        var session = sessions.Create(admin.Id, admin.Username, admin.MustChangePassword);
        ctx.Response.Cookies.Append(SessionCookie, session.SessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });
        Log.Info("Admin", $"login ok user={admin.Username} mustChange={admin.MustChangePassword}");
        return Results.Json(new { ok = true, mustChangePassword = admin.MustChangePassword });
    }

    //Logout 校验 session 后清 session 清 cookie 未登录返回 401
    private static IResult Logout(HttpContext ctx, AdminSessionStore sessions)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        var session = sessions.Find(sid);
        if (session == null) return Fail(401, "unauthorized", "not logged in");
        //session 非空则 SessionId 必有值 用它避免 sid 可空告警
        sessions.Remove(session.SessionId);
        ctx.Response.Cookies.Delete(SessionCookie, new CookieOptions { Path = "/" });
        return Results.Json(new { ok = true });
    }

    //ChangePasswordAsync 校验旧密码 更新 清 must_change 标志
    private static async Task<IResult> ChangePasswordAsync(HttpContext ctx, AdminRepository admins, AdminSessionStore sessions)
    {
        var req = await ctx.ReadBodyAsync<ChangePasswordRequest>();
        var (session, admin) = Resolve(ctx, sessions, admins);
        if (session == null || admin == null) return Fail(401, "unauthorized", "session expired");
        if (!PasswordHasher.VerifyAdmin(req.OldPassword, admin.PasswordHash))
            return Fail(403, "old_password_mismatch", "old password mismatch");
        if (string.IsNullOrEmpty(req.NewPassword) || req.NewPassword.Length < 4)
            return Fail(400, "password_too_short", "password must be at least 4 characters");
        admins.UpdatePassword(admin.Id, PasswordHasher.HashAdmin(req.NewPassword));
        sessions.UpdateMustChange(session.SessionId, false);
        Log.Info("Admin", $"password changed user={admin.Username}");
        return Results.Json(new { ok = true });
    }

    //ListApiAccounts 列出所有 api 账户 含 enabled 状态
    private static IResult ListApiAccounts(ApiAccountRepository apis)
    {
        var list = apis.ListAll().Select(a => new { id = a.Id, username = a.Username, enabled = a.Enabled, createdAt = a.CreatedAt });
        return Results.Json(new { ok = true, accounts = list });
    }

    //AddApiAccount 新增 api 账户 wss 握手用 Argon2id
    private static async Task<IResult> AddApiAccount(HttpContext ctx, ApiAccountRepository apis)
    {
        var req = await ctx.ReadBodyAsync<AddAccountRequest>();
        if (string.IsNullOrEmpty(req.Username) || string.IsNullOrEmpty(req.Password))
            return Fail(400, "fields_required", "username and password required");
        if (apis.FindByUsername(req.Username) != null)
            return Fail(409, "username_exists", "username already exists");
        var id = apis.Insert(req.Username, PasswordHasher.HashAdmin(req.Password));
        Log.Info("Admin", $"api account added id={id} user={req.Username}");
        return Results.Json(new { ok = true, id });
    }

    //DeleteApiAccount 删除 api 账户
    private static IResult DeleteApiAccount(int id, ApiAccountRepository apis)
    {
        apis.Delete(id);
        return Results.Json(new { ok = true });
    }

    //ChangeApiPassword 管理员后台改 api 账户密码
    private static async Task<IResult> ChangeApiPassword(int id, HttpContext ctx, ApiAccountRepository apis)
    {
        var req = await ctx.ReadBodyAsync<NewPasswordRequest>();
        if (string.IsNullOrEmpty(req.NewPassword) || req.NewPassword.Length < 4)
            return Fail(400, "password_too_short", "password must be at least 4 characters");
        var api = apis.FindById(id);
        if (api == null) return Fail(404, "account_not_found", "account not found");
        apis.UpdatePassword(id, PasswordHasher.HashAdmin(req.NewPassword));
        Log.Info("Admin", $"api password changed id={id} user={api.Username}");
        return Results.Json(new { ok = true });
    }

    //ToggleApiEnabled 切换 api 账户启用状态 禁用后 wss 握手拒绝
    private static async Task<IResult> ToggleApiEnabled(int id, HttpContext ctx, ApiAccountRepository apis)
    {
        var req = await ctx.ReadBodyAsync<ToggleEnabledRequest>();
        var api = apis.FindById(id);
        if (api == null) return Fail(404, "account_not_found", "account not found");
        apis.UpdateEnabled(id, req.Enabled);
        Log.Info("Admin", $"api enabled={req.Enabled} id={id} user={api.Username}");
        return Results.Json(new { ok = true, enabled = req.Enabled });
    }

    //ListPlayers 列出所有游戏用户 含 enabled 状态
    private static IResult ListPlayers(UserRepository users)
    {
        var list = users.ListAll().Select(u => new { id = u.Id, uuid = u.Uuid, username = u.Username, enabled = u.Enabled, email = u.Email });
        return Results.Json(new { ok = true, players = list });
    }

    //AddPlayer 新增游戏用户 生成 uuid PBKDF2 哈希 email 登录账号可空 PCL-CE 邮箱框填此值登录
    private static async Task<IResult> AddPlayer(HttpContext ctx, UserRepository users)
    {
        var req = await ctx.ReadBodyAsync<AddPlayerRequest>();
        if (string.IsNullOrEmpty(req.Username) || string.IsNullOrEmpty(req.Password))
            return Fail(400, "fields_required", "username and password required");
        if (users.FindByUsername(req.Username) != null)
            return Fail(409, "username_exists", "username already exists");
        if (!string.IsNullOrEmpty(req.Email) && users.FindByEmail(req.Email) != null)
            return Fail(409, "email_exists", "email already exists");
        var uuid = Guid.NewGuid().ToString("N");
        var id = users.Insert(uuid, req.Username, PasswordHasher.HashGameAccount(req.Password), req.Email);
        Log.Info("Admin", $"player added id={id} user={req.Username} email={req.Email ?? "-"} uuid={uuid}");
        return Results.Json(new { ok = true, id, uuid });
    }

    //DeletePlayer 删除游戏用户
    private static IResult DeletePlayer(int id, UserRepository users)
    {
        users.Delete(id);
        return Results.Json(new { ok = true });
    }

    //TogglePlayerEnabled 切换玩家启用状态 禁用后 Yggdrasil authenticate 拒绝
    private static async Task<IResult> TogglePlayerEnabled(int id, HttpContext ctx, UserRepository users)
    {
        var req = await ctx.ReadBodyAsync<ToggleEnabledRequest>();
        var user = users.FindById(id);
        if (user == null) return Fail(404, "player_not_found", "player not found");
        users.UpdateEnabled(id, req.Enabled);
        Log.Info("Admin", $"player enabled={req.Enabled} id={id} user={user.Username}");
        return Results.Json(new { ok = true, enabled = req.Enabled });
    }

    //UpdatePlayerAsync 修改玩家 username 与 uuid 唯一性校验
    private static async Task<IResult> UpdatePlayerAsync(int id, HttpContext ctx, UserRepository users)
    {
        var req = await ctx.ReadBodyAsync<UpdatePlayerRequest>();
        var user = users.FindById(id);
        if (user == null) return Fail(404, "player_not_found", "player not found");
        if (!string.IsNullOrEmpty(req.Username) && req.Username != user.Username)
        {
            if (users.FindByUsername(req.Username) != null)
                return Fail(409, "username_exists", "username already exists");
            users.UpdateUsername(id, req.Username);
        }
        if (!string.IsNullOrEmpty(req.Uuid) && req.Uuid != user.Uuid)
        {
            if (users.FindByUuid(req.Uuid) != null)
                return Fail(409, "uuid_exists", "uuid already exists");
            users.UpdateUuid(id, req.Uuid);
        }
        //email 传空串清空 NULL 兜底 非空时查重 空串与 NULL 视为一致避免反复触发更新
        var currentEmail = user.Email ?? "";
        if (req.Email != null && req.Email != currentEmail)
        {
            if (!string.IsNullOrEmpty(req.Email) && users.FindByEmail(req.Email) != null)
                return Fail(409, "email_exists", "email already exists");
            users.UpdateEmail(id, string.IsNullOrEmpty(req.Email) ? null : req.Email);
        }
        Log.Info("Admin", $"player updated id={id}");
        return Results.Json(new { ok = true });
    }

    //ChangePlayerPasswordAsync 管理员重置玩家密码 PBKDF2 哈希
    private static async Task<IResult> ChangePlayerPasswordAsync(int id, HttpContext ctx, UserRepository users)
    {
        var req = await ctx.ReadBodyAsync<NewPasswordRequest>();
        if (string.IsNullOrEmpty(req.NewPassword) || req.NewPassword.Length < 4)
            return Fail(400, "password_too_short", "password must be at least 4 characters");
        var user = users.FindById(id);
        if (user == null) return Fail(404, "player_not_found", "player not found");
        users.UpdatePassword(id, PasswordHasher.HashGameAccount(req.NewPassword));
        Log.Info("Admin", $"player password reset id={id} user={user.Username}");
        return Results.Json(new { ok = true });
    }

    //GetPlayerSkin 返回玩家皮肤 PNG 无皮肤 404
    private static IResult GetPlayerSkin(int id, UserRepository users, ProfileRepository profiles, TextureStorage storage)
    {
        var user = users.FindById(id);
        if (user == null) return Fail(404, "player_not_found", "player not found");
        var meta = profiles.GetTextureMeta(id);
        if (meta.SkinHash == null) return Fail(404, "skin_not_found", "no skin uploaded");
        var data = storage.Load(meta.SkinHash);
        if (data == null) return Fail(404, "skin_not_found", "skin file missing");
        return Results.File(data, "image/png");
    }

    //UploadPlayerSkinAsync 管理员上传玩家皮肤 body PNG query model slim/default
    private static async Task<IResult> UploadPlayerSkinAsync(int id, HttpContext ctx,
        UserRepository users, ProfileRepository profiles, TextureStorage storage)
    {
        var user = users.FindById(id);
        if (user == null) return Fail(404, "player_not_found", "player not found");
        var data = await ReadBodyBytesAsync(ctx);
        if (!IsValidPng(data)) return Fail(400, "invalid_png", "invalid PNG");
        var hash = ComputeHash(data);
        storage.Save(hash, data);
        var model = "slim".Equals(ctx.Request.Query["model"].ToString(), StringComparison.OrdinalIgnoreCase) ? "slim" : "default";
        profiles.SetSkin(id, hash, model);
        Log.Info("Admin", $"player skin uploaded id={id} hash={hash} model={model}");
        return Results.Json(new { ok = true });
    }

    //ChangePlayerSkinModelAsync 改皮肤类型 保留原 hash 只更新 model
    private static async Task<IResult> ChangePlayerSkinModelAsync(int id, HttpContext ctx,
        UserRepository users, ProfileRepository profiles)
    {
        var user = users.FindById(id);
        if (user == null) return Fail(404, "player_not_found", "player not found");
        var req = await ctx.ReadBodyAsync<ChangeModelRequest>();
        var model = "slim".Equals(req.Model, StringComparison.OrdinalIgnoreCase) ? "slim" : "default";
        var meta = profiles.GetTextureMeta(id);
        if (meta.SkinHash == null) return Fail(400, "skin_not_found", "no skin to change model");
        profiles.SetSkin(id, meta.SkinHash, model);
        Log.Info("Admin", $"player skin model changed id={id} model={model}");
        return Results.Json(new { ok = true, model });
    }

    //DeletePlayerSkin 清除玩家皮肤元数据 贴图文件保留
    private static IResult DeletePlayerSkin(int id, UserRepository users, ProfileRepository profiles)
    {
        var user = users.FindById(id);
        if (user == null) return Fail(404, "player_not_found", "player not found");
        profiles.ClearSkin(id);
        Log.Info("Admin", $"player skin cleared id={id}");
        return Results.Json(new { ok = true });
    }

    //ReadBodyBytesAsync 读请求体为字节数组 PNG 上传用 同步读被禁用
    private static async Task<byte[]> ReadBodyBytesAsync(HttpContext ctx)
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    //IsValidPng 校验 PNG 魔数
    private static bool IsValidPng(byte[] data)
    {
        if (data.Length < 8) return false;
        return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
    }

    //ComputeHash SHA256 hex 小写 贴图文件名
    private static string ComputeHash(byte[] data)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();

    //Resolve 从 cookie 解析当前会话与管理员实体
    private static (AdminSession? session, AdminAccount? admin) Resolve(HttpContext ctx, AdminSessionStore sessions, AdminRepository admins)
    {
        var sid = ctx.Request.Cookies[SessionCookie];
        var session = sessions.Find(sid);
        if (session == null) return (null, null);
        var admin = admins.FindById(session.AdminId);
        return (session, admin);
    }

    //GetSession 返回当前 session 用户名 mustChangePassword 与 language 前端据此路由与选语言
    private static IResult GetSession(HttpContext ctx, AdminSessionStore sessions, AdminRepository admins)
    {
        var (session, admin) = Resolve(ctx, sessions, admins);
        if (session == null || admin == null) return Results.Json(new { ok = false }, statusCode: 401);
        return Results.Json(new { ok = true, username = admin.Username, mustChangePassword = session.MustChangePassword, language = admin.Language });
    }

    //SetLanguageAsync 固化管理员语言偏好 前端切换语言后调用
    private static async Task<IResult> SetLanguageAsync(HttpContext ctx, AdminRepository admins, AdminSessionStore sessions)
    {
        var req = await ctx.ReadBodyAsync<SetLanguageRequest>();
        if (string.IsNullOrWhiteSpace(req.Language))
            return Fail(400, "fields_required", "language required");
        var (session, admin) = Resolve(ctx, sessions, admins);
        if (session == null || admin == null) return Fail(401, "unauthorized", "session expired");
        admins.UpdateLanguage(admin.Id, req.Language);
        Log.Info("Admin", $"language set user={admin.Username} lang={req.Language}");
        return Results.Json(new { ok = true, language = req.Language });
    }

    //Fail 构造统一错误响应 error 错误码 message 英文兜底
    private static IResult Fail(int status, string error, string message)
        => Results.Json(new { ok = false, error, message }, statusCode: status);

    //ReadBodyAsync 异步读取 JSON 请求体 同步读被 ASP.NET Core 默认禁用
    private static async Task<T> ReadBodyAsync<T>(this HttpContext ctx)
    {
        var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        return System.Text.Json.JsonSerializer.Deserialize<T>(body, _json)
            ?? throw new InvalidOperationException("invalid body");
    }

    private static readonly System.Text.Json.JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    //请求 DTO 内聚
    private sealed class LoginRequest { public string Username { get; set; } = ""; public string Password { get; set; } = ""; }
    private sealed class ChangePasswordRequest { public string OldPassword { get; set; } = ""; public string NewPassword { get; set; } = ""; }
    private sealed class AddAccountRequest { public string Username { get; set; } = ""; public string Password { get; set; } = ""; }
    //AddPlayerRequest 游戏玩家新增独有 email 登录账号可空 AddAccountRequest 仅 api 账户用不含 email
    private sealed class AddPlayerRequest { public string Username { get; set; } = ""; public string Password { get; set; } = ""; public string? Email { get; set; } }
    private sealed class NewPasswordRequest { public string NewPassword { get; set; } = ""; }
    private sealed class ToggleEnabledRequest { public bool Enabled { get; set; } }
    private sealed class SetLanguageRequest { public string Language { get; set; } = ""; }
    private sealed class UpdatePlayerRequest { public string? Username { get; set; } public string? Uuid { get; set; } public string? Email { get; set; } }
    private sealed class ChangeModelRequest { public string Model { get; set; } = ""; }
}
