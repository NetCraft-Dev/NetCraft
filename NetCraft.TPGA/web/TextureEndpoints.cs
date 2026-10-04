using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetCraft.TPGA.Crypto;
using NetCraft.TPGA.Storage;

namespace NetCraft.TPGA.Web;

//TextureEndpoints 贴图分发与公钥端点 主端口开放
//GET /textures/{hash} 返回 PNG 贴图 hash 仅 hex 防路径穿越
//GET /yggdrasil/public-key 返回 RSA 公钥 PEM 客户端验签用
public static class TextureEndpoints
{
    //MapTextureEndpoints 注册贴图分发与公钥路由
    public static IEndpointRouteBuilder MapTextureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/textures/{hash}", ServeTexture);
        app.MapGet("/yggdrasil/public-key", ServePublicKey);
        return app;
    }

    //ServeTexture 返回贴图文件 hash 非法或不存在 404
    private static IResult ServeTexture(string hash, TextureStorage storage)
    {
        hash = hash.ToLowerInvariant();
        if (!IsValidHash(hash)) return Results.NotFound();
        var data = storage.Load(hash);
        if (data == null) return Results.NotFound();
        return Results.File(data, "image/png");
    }

    //ServePublicKey 返回 RSA 公钥 PEM
    private static IResult ServePublicKey(ProfileKeyStore keys)
        => Results.Text(keys.PublicKeyPem, "text/plain; charset=utf-8");

    //IsValidHash 校验 64 位小写 hex 防路径穿越
    private static bool IsValidHash(string hash)
        => hash.Length == 64 && hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
}
