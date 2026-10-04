using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NetCraft.TPGA.Auth;

namespace NetCraft.TPGA.Web;

//AdminAuthMiddleware web 后台 session cookie 认证
//切换 React 前端后只拦 /api/* 数据请求 前端入口 / /login /password 放行由 React 路由处理
//未登录访问 /api/* 返回 401 前端收到跳 /login must_change 状态访问 /api/* 返回 403 跳 /password
//logout 与 password POST 不在此拦 由 API 内部 Resolve 校验 session
public static class AdminAuthMiddleware
{
    //UseAdminAuth 注册中间件 在路由前调用 IApplicationBuilder 适配 MapWhen 分支与顶层 WebApplication
    public static void UseAdminAuth(this IApplicationBuilder app)
    {
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? "";
            //只拦 /api/* 数据请求 前端入口与 Yggdrasil /ws /login /logout /password POST 放行
            if (!IsApi(path)) { await next(); return; }

            var sessions = ctx.RequestServices.GetRequiredService<AdminSessionStore>();
            var admins = ctx.RequestServices.GetRequiredService<AdminRepository>();
            var sid = ctx.Request.Cookies[AdminEndpoints.SessionCookie];
            var session = sessions.Find(sid);
            //session 不存在或对应管理员被删 视为未登录
            if (session == null || admins.FindById(session.AdminId) == null)
            {
                await WriteJsonAsync(ctx, 401, "unauthorized", "not logged in");
                return;
            }
            //首登强制改密 /api/* 一律拒绝 前端收到 403 跳 /password
            //session 检查端点放行 否则刷新后前端拿不到 mustChangePassword 状态
            if (session.MustChangePassword && !path.Equals("/api/session", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(ctx, 403, "must_change_password", "change password first");
                return;
            }
            await next();
        });
    }

    //IsApi 判断是否 /api/* 路径 仅数据请求需 session
    private static bool IsApi(string path) => path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

    //WriteJsonAsync 写简单 JSON 错误响应
    private static async Task WriteJsonAsync(HttpContext ctx, int status, string error, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync($"{{\"ok\":false,\"error\":\"{error}\",\"message\":\"{message}\"}}");
    }
}
