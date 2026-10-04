using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using NetCraft.TPGA.Config;
using NetCraft.TPGA.Crypto;
using NetCraft.TPGA.Logging;
using NetCraft.TPGA.Protocol;

namespace NetCraft.TPGA.Web;

//WebUIEndpoints webui 构建产物服务
//player.html / admin.html 多入口 共享 assets 与 fonts
//构建产物嵌入 NetCraft.TPGA.dll 由 EmbeddedFileProvider 提供服务
//资源名格式 NetCraft.TPGA.wwwroot.路径.文件名 base_namespace=NetCraft.TPGA.wwwroot
public static class WebUIEndpoints
{
    //UseWebUIStaticFiles 注册静态资源中间件 服务 /assets/* /fonts/*
    //不设 RequestPath URL 路径直接映射到嵌入资源路径
    //ServeUnknownFileTypes 允许 .woff .ttf 等非标准类型
    public static IApplicationBuilder UseWebUIStaticFiles(this IApplicationBuilder app)
    {
        var provider = CreateProvider();
        //Web SDK 内嵌 wwwroot 后 StaticFileMiddleware 不服务嵌入资源 改手动从 EmbeddedFileProvider 读取流返回
        //仅 /assets/ /fonts/ 走手动服务 其余请求放行到认证与路由
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? "";
            if (!path.StartsWith("/assets/") && !path.StartsWith("/fonts/")) { await next(); return; }
            var fi = provider.GetFileInfo(path);
            if (!fi.Exists || fi.IsDirectory) { await next(); return; }
            ctx.Response.ContentType = GuessContentType(path);
            ctx.Response.ContentLength = fi.Length;
            await using var stream = fi.CreateReadStream();
            await stream.CopyToAsync(ctx.Response.Body);
        });
        return app;
    }

    //GuessContentType 按扩展名推断 MIME StaticFileMiddleware 不可用时的替代
    private static string GuessContentType(string path)
    {
        if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) return "application/javascript";
        if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)) return "text/css";
        if (path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)) return "font/woff";
        if (path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase)) return "font/woff2";
        if (path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) return "font/ttf";
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) return "text/html; charset=utf-8";
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return "application/json";
        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return "image/png";
        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return "image/svg+xml";
        return "application/octet-stream";
    }

    //MapWebUI 注册前端入口与 SPA fallback defaultEntry 指定该端口默认入口
    //enableMetadata 主端口传 true GET / 按 Accept 分流 text/html 返回页面 其余返回 metadata
    //浏览器请求页面带 Accept text/html authlib-injector/PCL-CE 拉 metadata 不带 text/html 默认 JSON
    //Yggdrasil /ws /login /api 等具体路由优先匹配 catch-all 仅兜底未注册 GET
    public static IEndpointRouteBuilder MapWebUI(this IEndpointRouteBuilder app, string defaultEntry, bool enableMetadata = false)
    {
        app.MapGet("/", (HttpContext ctx, ProfileKeyStore? keys, TpgaConfig? config) =>
        {
            var accept = ctx.Request.Headers.Accept.ToString();
            //浏览器带 text/html 返回页面 其余含 PCL-CE 的 */* 无 Accept application/json 一律返回 metadata
            if (enableMetadata && keys != null && config != null
                && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(MetadataBuilder.Build(keys, config));
            }
            return ServeHtml(defaultEntry);
        });
        app.MapGet("/{**rest}", () => ServeHtml(defaultEntry));
        return app;
    }

    //ServeHtml 从嵌入资源读取 HTML 返回 未找到提示先跑 build.ps1
    private static IResult ServeHtml(string name)
    {
        var provider = CreateProvider();
        var file = provider.GetFileInfo(name);
        if (!file.Exists)
            return Results.Text($"webui entry {name} not found, run build.ps1 to build frontend first", "text/plain");
        return Results.File(file.CreateReadStream(), "text/html; charset=utf-8");
    }

    //CreateProvider 构造 EmbeddedFileProvider baseNamespace 对应 csproj EmbeddedResource 命名空间
    private static EmbeddedFileProvider CreateProvider()
        => new(typeof(Boot).Assembly, "NetCraft.TPGA.wwwroot");
}
