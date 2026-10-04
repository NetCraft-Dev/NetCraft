using System.Text.Json.Serialization;

namespace NetCraft.TPGA.Models;

//Yggdrasil 协议 DTO 集合
//字段命名用 camelCase 序列化兼容原版启动器
//profileId accessToken 均为去连字符 32 位 hex

//Agent 客户端代理信息 authenticate 携带
public sealed class AgentDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Minecraft";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
}

//Profile 简要档案 authenticate/refresh 响应里的 availableProfiles 与 selectedProfile
public sealed class ProfileDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

//ProfileProperty 档案属性 hasJoined/profile 响应的 properties 数组元素
//value base64 的 textures JSON signature 签名 base64 MVP 不签名为空
public sealed class ProfilePropertyDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
}

//Profile 完整档案 hasJoined/profile 响应含 properties
public sealed class FullProfileDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("properties")]
    public List<ProfilePropertyDto> Properties { get; set; } = new();
}

//AuthenticateRequest authenticate 请求
//username 游戏账户用户名 password 明文 clientToken 可选缺省服务端生成
public sealed class AuthenticateRequest
{
    [JsonPropertyName("agent")]
    public AgentDto? Agent { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("clientToken")]
    public string? ClientToken { get; set; }

    [JsonPropertyName("requestUser")]
    public bool RequestUser { get; set; }
}

//AuthenticateResponse authenticate 响应
//availableProfiles 当前账户的档案列表 单账户即一个 selectedProfile
public sealed class AuthenticateResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("clientToken")]
    public string ClientToken { get; set; } = "";

    [JsonPropertyName("availableProfiles")]
    public List<ProfileDto> AvailableProfiles { get; set; } = new();

    [JsonPropertyName("selectedProfile")]
    public ProfileDto? SelectedProfile { get; set; }

    [JsonPropertyName("user")]
    public UserInfoDto? User { get; set; }
}

//UserInfoDto requestUser=true 时返回的账户信息
public sealed class UserInfoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("properties")]
    public List<ProfilePropertyDto> Properties { get; set; } = new();
}

//RefreshRequest refresh 请求
//accessToken 旧令牌 clientToken 必须与签发时一致 selectedProfile 可选
public sealed class RefreshRequest
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("clientToken")]
    public string? ClientToken { get; set; }

    [JsonPropertyName("selectedProfile")]
    public ProfileDto? SelectedProfile { get; set; }

    [JsonPropertyName("requestUser")]
    public bool RequestUser { get; set; }
}

//RefreshResponse refresh 响应
public sealed class RefreshResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("clientToken")]
    public string ClientToken { get; set; } = "";

    [JsonPropertyName("selectedProfile")]
    public ProfileDto? SelectedProfile { get; set; }

    [JsonPropertyName("user")]
    public UserInfoDto? User { get; set; }
}

//TokenRequest validate/invalidate 请求
//clientToken 可选 validate 不带则以 accessToken 为准
public sealed class TokenRequest
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("clientToken")]
    public string? ClientToken { get; set; }
}

//SignOutRequest signout 请求 用用户名密码登出所有令牌
public sealed class SignOutRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}

//JoinRequest sessionserver/session/minecraft/join 请求
//selectedProfile 为 profileId 32 hex serverId 服务端生成
public sealed class JoinRequest
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("selectedProfile")]
    public string SelectedProfile { get; set; } = "";

    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = "";
}

//ErrorResponse 统一错误响应
//error 短标识 errorMessage 可读消息
public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = "";

    //Cause 仅部分错误如 IllegalArgumentException 携带
    [JsonPropertyName("cause")]
    public string? Cause { get; set; }
}

//YggdrasilException 业务异常 Endpoints 捕获转 ErrorResponse
//Error 对应 Yggdrasil 错误标识如 ForbiddenOperationException
public sealed class YggdrasilException : Exception
{
    public string Error { get; }

    public YggdrasilException(string error, string message) : base(message)
    {
        Error = error;
    }
}
