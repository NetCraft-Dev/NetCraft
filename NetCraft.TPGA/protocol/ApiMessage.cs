using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetCraft.TPGA.Protocol;

//ApiMessage wss 应用层消息 DTO
//type 字段区分消息类型 服务端发 pubkey/auth_ok/auth_fail/api_result 客户端发 auth/api_call
//其余字段按消息类型复用 可空字段缺省不序列化
public sealed class ApiMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    //token auth_ok 时为颁发 UUID api_call 时为客户端回传校验
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    //pem pubkey 消息携带 RSA 公钥 PEM
    [JsonPropertyName("pem")]
    public string? Pem { get; set; }

    //payload auth 消息携带 base64(RSA加密的身份包)
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    //method api_call 携带要调用的方法名
    [JsonPropertyName("method")]
    public string? Method { get; set; }

    //args api_call 携带方法参数 动态 JSON
    [JsonPropertyName("args")]
    public JsonElement? Args { get; set; }

    //ok api_result 是否成功
    [JsonPropertyName("ok")]
    public bool? Ok { get; set; }

    //data api_result 成功返回数据
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    //error auth_fail/error/api_result 失败携带 错误码或简短描述
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    //errorMessage api_result 失败时可读消息 客户端可展示 error 为结构化错误码时用此字段
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

//CreatePlayerArgs player.create 参数 username/password 必填 email 可空
public sealed class CreatePlayerArgs
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Email { get; set; }
}

//DeletePlayerArgs player.delete 参数 uuid 必填
public sealed class DeletePlayerArgs
{
    public string Uuid { get; set; } = "";
}

//FreezePlayerArgs player.freeze 参数 uuid 必填 enabled true 解冻 false 冻结
public sealed class FreezePlayerArgs
{
    public string Uuid { get; set; } = "";
    public bool Enabled { get; set; }
}

//UpdatePlayerArgs player.update 参数 uuid 必填 其余字段非空则更新
//email null 不变 空串清空 非空查重后更新 与 AdminEndpoints 一致
public sealed class UpdatePlayerArgs
{
    public string Uuid { get; set; } = "";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Email { get; set; }
}

//WssApiException wss api 业务异常 携带错误码与可读消息 转 ApiMessage api_result ok=false
public sealed class WssApiException : Exception
{
    public string Code { get; }
    public WssApiException(string code, string message) : base(message) => Code = code;
}

//AuthPayload 身份包 RSA 加密前/解密后的明文结构
//timestamp Unix 秒 nonce 防重放由客户端生成
public sealed class AuthPayload
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

//WssException wss 握手业务异常 消息回传客户端不暴露堆栈
public sealed class WssException : Exception
{
    public WssException(string message) : base(message) { }
}
