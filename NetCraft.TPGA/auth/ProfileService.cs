using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetCraft.TPGA.Crypto;
using NetCraft.TPGA.Models;

namespace NetCraft.TPGA.Auth;

//ProfileService 构建 textures 属性 含 base64 value 与 RSA 签名
//贴图 URL 用 host 动态拼接 客户端下载后用公钥验签防篡改
public sealed class ProfileService
{
    private readonly ProfileKeyStore _keyStore;
    private readonly ProfileRepository _profiles;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ProfileService(ProfileKeyStore keyStore, ProfileRepository profiles)
    {
        _keyStore = keyStore;
        _profiles = profiles;
    }

    //BuildTexturesProperty 构建 textures 属性 无贴图返回 null
    //host 含端口 如 127.0.0.1:25565 unsigned true 时 payload 不含 signatureRequired 且不签名
    public ProfilePropertyDto? BuildTexturesProperty(UserAccount user, string host, bool unsigned = false)
    {
        var meta = _profiles.GetTextureMeta(user.Id);
        if (meta.SkinHash == null && meta.CapeHash == null) return null;

        var textures = new Dictionary<string, object?>();
        if (meta.SkinHash != null)
        {
            //slim 模型细手臂 其他一律 default 兼容 classic 写法
            var model = "slim".Equals(meta.SkinModel, StringComparison.OrdinalIgnoreCase) ? "slim" : "default";
            textures["SKIN"] = new
            {
                url = $"https://{host}/textures/{meta.SkinHash}",
                metadata = new { model }
            };
        }
        if (meta.CapeHash != null)
        {
            textures["CAPE"] = new { url = $"https://{host}/textures/{meta.CapeHash}" };
        }

        //payload 字段顺序固定 timestamp profileId profileName textures signatureRequired 按需追加
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["profileId"] = user.Uuid,
            ["profileName"] = user.Username,
            ["textures"] = textures
        };
        if (!unsigned) payload["signatureRequired"] = true;

        var json = JsonSerializer.Serialize(payload, _json);
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        //unsigned true 签名为 null 客户端跳过验签
        var signature = unsigned ? null : Convert.ToBase64String(_keyStore.Sign(Encoding.UTF8.GetBytes(value)));
        return new ProfilePropertyDto
        {
            Name = "textures",
            Value = value,
            Signature = signature
        };
    }
}
