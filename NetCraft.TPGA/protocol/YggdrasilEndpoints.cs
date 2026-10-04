using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Models;

namespace NetCraft.TPGA.Protocol;

//YggdrasilEndpoints 主端口 Yggdrasil 路由注册
//认证端点挂两套路径 无前缀供 authlib-injector 注入的 Minecraft 客户端用 /authserver 前缀供 PCL-CE 等启动器直连用
//join hasJoined profile 仅无前缀 客户端走注入路径 启动器登录后也会拉 profile
//profile 路径挂两个 sessionserver/session/minecraft/profile 新版标准 profiles/minecraft 旧版兼容
//authenticate signout 加 IP 限流 防暴力破解
//YggdrasilException 由 endpoint filter 统一转 ErrorResponse 返回对应状态码
public static class YggdrasilEndpoints
{
    //Map 注册所有 Yggdrasil 端点并附加异常过滤
    public static void MapYggdrasilEndpoints(this IEndpointRouteBuilder app)
    {
        //无前缀组 authlib-injector 注入后的 Minecraft 客户端走这套
        var root = app.MapGroup("/").AddEndpointFilter<YggdrasilExceptionFilter>();
        MapAuthRoutes(root);
        root.MapPost("/profiles/minecraft", BatchProfilesHandler);
        root.MapGet("/blockedservers", () => Results.Text("", "text/plain"));
        root.MapGet("/player/attributes", () => Results.Json(new { }));
        root.MapPost("/sessionserver/session/minecraft/join", JoinHandler);
        root.MapGet("/sessionserver/session/minecraft/hasJoined", HasJoinedHandler);
        //profile 标准路径 sessionserver/session/minecraft/profile 旧路径 profiles/minecraft 均注册
        root.MapGet("/sessionserver/session/minecraft/profile/{uuid}", ProfileHandler);
        root.MapGet("/profiles/minecraft/{uuid}", ProfileHandler);

        //authserver 前缀组 PCL-CE 等启动器对接皮肤站走这套 仅认证端点
        var auth = app.MapGroup("/authserver").AddEndpointFilter<YggdrasilExceptionFilter>();
        MapAuthRoutes(auth);
    }

    //MapAuthRoutes 注册 5 个认证端点 无前缀和 authserver 前缀共用
    private static void MapAuthRoutes(IEndpointRouteBuilder group)
    {
        group.MapPost("/authenticate", AuthenticateHandler);
        group.MapPost("/refresh", RefreshHandler);
        group.MapPost("/validate", ValidateHandler);
        group.MapPost("/invalidate", InvalidateHandler);
        group.MapPost("/signout", SignOutHandler);
    }

    //AuthenticateHandler 用户名密码换 accessToken 限流防暴力破解
    //仅 ForbiddenOperationException 计数 成功清除
    private static async Task<IResult> AuthenticateHandler(HttpContext ctx, YggdrasilService svc, AuthRateLimiter limiter)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (limiter.IsLimited(ip))
            return Error(429, "RateLimitedException", "Too many attempts. Try later.");
        try
        {
            var result = svc.Authenticate(await ctx.ReadJsonAsync<AuthenticateRequest>());
            limiter.Reset(ip);
            return Results.Ok(result);
        }
        catch (YggdrasilException ex) when (ex.Error == "ForbiddenOperationException")
        {
            limiter.RecordFailure(ip);
            throw;
        }
    }

    //RefreshHandler 用旧 token 换新 token
    private static async Task<IResult> RefreshHandler(HttpContext ctx, YggdrasilService svc)
        => Results.Ok(svc.Refresh(await ctx.ReadJsonAsync<RefreshRequest>()));

    //ValidateHandler 校验 token 有效性不失效
    private static async Task<IResult> ValidateHandler(HttpContext ctx, YggdrasilService svc)
        => svc.Validate(await ctx.ReadJsonAsync<TokenRequest>()) ? Results.NoContent() : Error(403, "ForbiddenOperationException", "Invalid token.");

    //InvalidateHandler 失效单个 token
    private static async Task<IResult> InvalidateHandler(HttpContext ctx, YggdrasilService svc)
    {
        svc.Invalidate(await ctx.ReadJsonAsync<TokenRequest>());
        return Results.NoContent();
    }

    //SignOutHandler 用户名密码登出 失效该用户所有 token 限流防暴力破解
    private static async Task<IResult> SignOutHandler(HttpContext ctx, YggdrasilService svc, AuthRateLimiter limiter)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (limiter.IsLimited(ip))
            return Error(429, "RateLimitedException", "Too many attempts. Try later.");
        try
        {
            svc.SignOut(await ctx.ReadJsonAsync<SignOutRequest>());
            limiter.Reset(ip);
            return Results.NoContent();
        }
        catch (YggdrasilException ex) when (ex.Error == "ForbiddenOperationException")
        {
            limiter.RecordFailure(ip);
            throw;
        }
    }

    //JoinHandler 服务端加入会话 提交 serverId 供 hasJoined 校验
    private static async Task<IResult> JoinHandler(HttpContext ctx, YggdrasilService svc)
    {
        svc.Join(await ctx.ReadJsonAsync<JoinRequest>());
        return Results.NoContent();
    }

    //HasJoinedHandler 服务端校验玩家是否加入 返回档案或 204
    //unsigned=true 时 textures 不签名 客户端免签加载
    private static IResult HasJoinedHandler(HttpContext ctx, YggdrasilService svc)
    {
        var username = ctx.Request.Query["username"].ToString();
        var serverId = ctx.Request.Query["serverId"].ToString();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(serverId))
            return Error(400, "IllegalArgumentException", "username and serverId are required.");
        var unsigned = ctx.Request.Query["unsigned"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
        //host 含端口 用于拼接 textures URL ToString 非空避免可空告警
        var profile = svc.HasJoined(username, serverId, ctx.Request.Host.ToString(), unsigned);
        return profile == null ? Results.NoContent() : Results.Ok(profile);
    }

    //ProfileHandler 按 uuid 查玩家档案 含皮肤属性签名 unsigned=true 时不签名
    private static IResult ProfileHandler(HttpContext ctx, string uuid, YggdrasilService svc)
    {
        if (string.IsNullOrEmpty(uuid))
            return Error(400, "IllegalArgumentException", "uuid is required.");
        var unsigned = ctx.Request.Query["unsigned"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
        return Results.Ok(svc.GetProfile(uuid, ctx.Request.Host.ToString(), unsigned));
    }

    //BatchProfilesHandler 批量按用户名查档案 body 用户名数组 返回存在的 profile
    private static async Task<IResult> BatchProfilesHandler(HttpContext ctx, YggdrasilService svc)
    {
        var names = await ctx.ReadJsonAsync<string[]>();
        if (names == null || names.Length == 0)
            return Results.Ok(Array.Empty<ProfileDto>());
        if (names.Length > 100)
            throw new YggdrasilException("IllegalArgumentException", "Too many names.");
        return Results.Ok(svc.FindProfilesByNames(names));
    }

    //Error 构造错误响应
    private static IResult Error(int status, string error, string message)
        => Results.Json(new ErrorResponse { Error = error, ErrorMessage = message }, statusCode: status);

    //ReadJsonAsync 异步读取请求体反序列化 空体或格式错误抛 IllegalArgumentException
    //限制 8KB 防超大 body 攻击 同步读取被 ASP.NET Core 默认禁用故必须 async
    private static async Task<T> ReadJsonAsync<T>(this HttpContext ctx)
    {
        try
        {
            //ContentLength 可能为 null chunked 读取后兜底检查
            if (ctx.Request.ContentLength is > 8192)
                throw new YggdrasilException("IllegalArgumentException", "Request body too large.");
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            if (body.Length > 8192)
                throw new YggdrasilException("IllegalArgumentException", "Request body too large.");
            if (string.IsNullOrEmpty(body))
                throw new YggdrasilException("IllegalArgumentException", "Request body is empty.");
            return System.Text.Json.JsonSerializer.Deserialize<T>(body, _jsonOptions)
                ?? throw new YggdrasilException("IllegalArgumentException", "Request body is invalid.");
        }
        catch (YggdrasilException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new YggdrasilException("IllegalArgumentException", $"Request body parse error: {ex.Message}");
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

//YggdrasilExceptionFilter endpoint 过滤器捕获业务异常转 ErrorResponse
//next 返回 ValueTask 异常在 await 时抛出 必须 await 才能进入 catch
public sealed class YggdrasilExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (YggdrasilException ex)
        {
            var status = ex.Error switch
            {
                "ForbiddenOperationException" => 403,
                "IllegalArgumentException" => 400,
                "GameProfileNotFoundException" => 404,
                "RateLimitedException" => 429,
                _ => 400
            };
            var body = new ErrorResponse { Error = ex.Error, ErrorMessage = ex.Message };
            return Results.Json(body, statusCode: status);
        }
    }
}
