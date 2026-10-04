using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetCraft.TPGA.Auth;
using NetCraft.TPGA.Models;
using NetCraft.TPGA.Storage;

namespace NetCraft.TPGA.Web;

//SkinEndpoints 皮肤披风上传删除端点 主端口
//PUT /user/profile/{uuid}/skin 上传皮肤 Bearer accessToken 鉴权 body PNG
//DELETE /user/profile/{uuid}/skin 删除皮肤
//披风同构 PUT/DELETE /user/profile/{uuid}/cape
//错误响应与 Yggdrasil 一致 ErrorResponse
public static class SkinEndpoints
{
    //MapSkinEndpoints 注册皮肤披风上传删除路由
    public static IEndpointRouteBuilder MapSkinEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/user/profile/{uuid}/skin", UploadSkin);
        app.MapDelete("/user/profile/{uuid}/skin", DeleteSkin);
        app.MapPut("/user/profile/{uuid}/cape", UploadCape);
        app.MapDelete("/user/profile/{uuid}/cape", DeleteCape);
        return app;
    }

    //UploadSkin 上传皮肤 验证 token 属主后存 PNG 更新 skin_hash
    private static async Task<IResult> UploadSkin(string uuid, HttpContext ctx,
        UserRepository users, TokenRepository tokens, ProfileRepository profiles, TextureStorage storage)
    {
        var (user, err) = await AuthenticateAsync(uuid, ctx, users, tokens);
        if (err != null) return err;
        var data = await ReadBodyAsync(ctx);
        if (!IsValidPng(data)) return Error(400, "IllegalArgumentException", "invalid PNG");
        var hash = ComputeHash(data);
        storage.Save(hash, data);
        var model = "slim".Equals(ctx.Request.Query["model"].ToString(), StringComparison.OrdinalIgnoreCase) ? "slim" : "default";
        profiles.SetSkin(user!.Id, hash, model);
        return Results.NoContent();
    }

    //DeleteSkin 清除皮肤元数据 贴图文件保留供其他引用
    private static IResult DeleteSkin(string uuid, HttpContext ctx,
        UserRepository users, TokenRepository tokens, ProfileRepository profiles)
    {
        var (user, err) = Authenticate(uuid, ctx, users, tokens);
        if (err != null) return err;
        profiles.ClearSkin(user!.Id);
        return Results.NoContent();
    }

    //UploadCape 上传披风 逻辑同皮肤无 model
    private static async Task<IResult> UploadCape(string uuid, HttpContext ctx,
        UserRepository users, TokenRepository tokens, ProfileRepository profiles, TextureStorage storage)
    {
        var (user, err) = await AuthenticateAsync(uuid, ctx, users, tokens);
        if (err != null) return err;
        var data = await ReadBodyAsync(ctx);
        if (!IsValidPng(data)) return Error(400, "IllegalArgumentException", "invalid PNG");
        var hash = ComputeHash(data);
        storage.Save(hash, data);
        profiles.SetCape(user!.Id, hash);
        return Results.NoContent();
    }

    //DeleteCape 清除披风元数据
    private static IResult DeleteCape(string uuid, HttpContext ctx,
        UserRepository users, TokenRepository tokens, ProfileRepository profiles)
    {
        var (user, err) = Authenticate(uuid, ctx, users, tokens);
        if (err != null) return err;
        profiles.ClearCape(user!.Id);
        return Results.NoContent();
    }

    //Authenticate 同步鉴权 Bearer token 验证 uuid 属主
    private static (UserAccount?, IResult?) Authenticate(string uuid, HttpContext ctx,
        UserRepository users, TokenRepository tokens)
    {
        var token = ExtractToken(ctx);
        if (token == null) return (null, Error(401, "ForbiddenOperationException", "missing access token"));
        var record = tokens.Find(token);
        if (record == null) return (null, Error(403, "ForbiddenOperationException", "invalid access token"));
        var user = users.FindByUuid(uuid);
        if (user == null || user.Id != record.UserId)
            return (null, Error(403, "ForbiddenOperationException", "profile not owned"));
        return (user, null);
    }

    //AuthenticateAsync 异步鉴权 同步版本用于上传后读取 body
    private static async Task<(UserAccount?, IResult?)> AuthenticateAsync(string uuid, HttpContext ctx,
        UserRepository users, TokenRepository tokens)
    {
        //token 与 user 查询无 IO 异步 但保持签名一致便于上传 handler 调用
        await Task.CompletedTask;
        return Authenticate(uuid, ctx, users, tokens);
    }

    //ExtractToken 从 Authorization Bearer 取 token
    private static string? ExtractToken(HttpContext ctx)
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(auth)) return null;
        var parts = auth.Split(' ', 2);
        if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase)) return null;
        return parts[1];
    }

    //ReadBodyAsync 读请求体为字节数组 PNG 二进制
    private static async Task<byte[]> ReadBodyAsync(HttpContext ctx)
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }

    //IsValidPng 校验 PNG 魔数 89 50 4E 47 0D 0A 1A 0A
    private static bool IsValidPng(byte[] data)
    {
        if (data.Length < 8) return false;
        return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;
    }

    //ComputeHash SHA256 hex 小写
    private static string ComputeHash(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    //Error 构造 ErrorResponse 与 Yggdrasil 协议一致
    private static IResult Error(int status, string error, string message)
        => Results.Json(new ErrorResponse { Error = error, ErrorMessage = message }, statusCode: status);
}
