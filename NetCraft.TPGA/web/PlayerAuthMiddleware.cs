using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NetCraft.TPGA.Auth;

namespace NetCraft.TPGA.Web;

//PlayerAuthMiddleware 主端口玩家 session cookie 认证
//拦 /api/player/* 数据请求 前端入口 /login /auth/* /user/closet 等放行由 React 路由处理
//login logout session 三个公开端点放行 由 PlayerEndpoints 内部校验 session
//未登录返回 401 session 失效或玩家被禁/删同样 401 前端收到跳 /login
public static class PlayerAuthMiddleware
{
    //UsePlayerAuth 注册中间件 在路由前调用
    public static void UsePlayerAuth(this IApplicationBuilder app)
    {
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? "";
            //只拦 /api/player/* 数据请求 公开端点放行
            if (!IsPlayerApi(path) || IsPublicPlayerApi(path)) { await next(); return; }

            var sessions = ctx.RequestServices.GetRequiredService<PlayerSessionStore>();
            var users = ctx.RequestServices.GetRequiredService<UserRepository>();
            var sid = ctx.Request.Cookies[PlayerEndpoints.SessionCookie];
            var session = sessions.Find(sid);
            //session 不存在或对应玩家被删/禁 视为未登录
            if (session == null)
            {
                await WriteJsonAsync(ctx, 401, "unauthorized", "not logged in");
                return;
            }
            var user = users.FindById(session.UserId);
            if (user == null || !user.Enabled)
            {
                await WriteJsonAsync(ctx, 401, "unauthorized", "account unavailable");
                return;
            }
            await next();
        });
    }

    //IsPlayerApi 是否 /api/player/* 路径
    private static bool IsPlayerApi(string path) => path.StartsWith("/api/player/", StringComparison.OrdinalIgnoreCase);

    //IsPublicPlayerApi login/logout/session 公开端点 不需先登录
    //oauth/* 发起挑战与 providers 列表 register 补全注册 都未登录可用
    private static bool IsPublicPlayerApi(string path)
    {
        return path.Equals("/api/player/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/player/logout", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/player/session", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/player/oauth/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/player/register", StringComparison.OrdinalIgnoreCase);
    }

    //WriteJsonAsync 写简单 JSON 错误响应
    private static async Task WriteJsonAsync(HttpContext ctx, int status, string error, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync($"{{\"ok\":false,\"error\":\"{error}\",\"message\":\"{message}\"}}");
    }
}
