using NetCraft.TPGA.Db;
using NetCraft.TPGA.Logging;
using NetCraft.TPGA.Models;

namespace NetCraft.TPGA.Auth;

//YggdrasilService Yggdrasil 验证协议业务逻辑
//8 端点 authenticate refresh validate invalidate signout join hasJoined profile
//accessToken/clientToken/profileId 均为去连字符 32 位 hex
//单账户单档案 MVP 一个 user 一个 profile availableProfiles 仅含自身
//hasJoined/profile 响应附加 textures 属性 含 base64 value 与 RSA 签名
//unsigned 参数透传 ProfileService true 时不签名
//authenticate 禁用账户统一返回 invalid credentials 不泄露账户存在性
public sealed class YggdrasilService
{
    private readonly UserRepository _users;
    private readonly TokenRepository _tokens;
    private readonly ServerJoinRepository _joins;
    private readonly ProfileService _profileService;

    public YggdrasilService(UserRepository users, TokenRepository tokens, ServerJoinRepository joins, ProfileService profileService)
    {
        _users = users;
        _tokens = tokens;
        _joins = joins;
        _profileService = profileService;
    }

    //Authenticate 校验密码签发令牌
    //clientToken 缺省服务端生成 同用户旧令牌清除保持单会话
    //账户不存在/禁用/密码错误统一返回 invalid credentials 防止枚举账户
    public AuthenticateResponse Authenticate(AuthenticateRequest req)
    {
        var user = _users.FindByLogin(req.Username);
        if (user == null || !user.Enabled || !PasswordHasher.VerifyGameAccount(req.Password, user.PasswordHash))
            throw new YggdrasilException("ForbiddenOperationException", "Invalid credentials. Invalid username or password.");
        var clientToken = string.IsNullOrWhiteSpace(req.ClientToken) ? NewToken() : req.ClientToken;
        _tokens.DeleteByUser(user.Id);
        var accessToken = NewToken();
        _tokens.Insert(accessToken, user.Id, clientToken);
        Log.Info("Yggdrasil", $"authenticate user={user.Username} token={accessToken}");
        return new AuthenticateResponse
        {
            AccessToken = accessToken,
            ClientToken = clientToken,
            AvailableProfiles = { BuildProfile(user) },
            SelectedProfile = BuildProfile(user),
            User = req.RequestUser ? BuildUserInfo(user) : null
        };
    }

    //Refresh 校验旧令牌换发新令牌 clientToken 必须匹配
    public RefreshResponse Refresh(RefreshRequest req)
    {
        var record = _tokens.Find(req.AccessToken);
        if (record == null)
            throw new YggdrasilException("ForbiddenOperationException", "Invalid token.");
        if (!string.IsNullOrEmpty(req.ClientToken) && !string.Equals(req.ClientToken, record.ClientToken, StringComparison.Ordinal))
            throw new YggdrasilException("ForbiddenOperationException", "Invalid token.");
        var user = _users.FindById(record.UserId) ?? throw new YggdrasilException("ForbiddenOperationException", "Invalid token.");
        _tokens.Delete(req.AccessToken);
        var accessToken = NewToken();
        var clientToken = req.ClientToken ?? record.ClientToken ?? NewToken();
        _tokens.Insert(accessToken, user.Id, clientToken);
        Log.Info("Yggdrasil", $"refresh user={user.Username} token={accessToken}");
        return new RefreshResponse
        {
            AccessToken = accessToken,
            ClientToken = clientToken,
            SelectedProfile = BuildProfile(user),
            User = req.RequestUser ? BuildUserInfo(user) : null
        };
    }

    //Validate 校验令牌有效返回 bool 由端点决定 204 或 403
    public bool Validate(TokenRequest req)
    {
        var record = _tokens.Find(req.AccessToken);
        if (record == null) return false;
        if (!string.IsNullOrEmpty(req.ClientToken) && !string.Equals(req.ClientToken, record.ClientToken, StringComparison.Ordinal))
            return false;
        return true;
    }

    //Invalidate 删除单个令牌 无论存在与否返回成功
    public void Invalidate(TokenRequest req)
    {
        _tokens.Delete(req.AccessToken);
        Log.Info("Yggdrasil", $"invalidate token={req.AccessToken}");
    }

    //SignOut 用用户名密码登出该用户所有令牌
    public void SignOut(SignOutRequest req)
    {
        var user = _users.FindByLogin(req.Username);
        if (user == null || !PasswordHasher.VerifyGameAccount(req.Password, user.PasswordHash))
            throw new YggdrasilException("ForbiddenOperationException", "Invalid credentials. Invalid username or password.");
        _tokens.DeleteByUser(user.Id);
        Log.Info("Yggdrasil", $"signout user={user.Username}");
    }

    //Join 校验令牌与 profileId 后记录 server join 30 秒内可被 hasJoined 查到
    public void Join(JoinRequest req)
    {
        var record = _tokens.Find(req.AccessToken);
        if (record == null)
            throw new YggdrasilException("ForbiddenOperationException", "Invalid token.");
        var user = _users.FindById(record.UserId) ?? throw new YggdrasilException("ForbiddenOperationException", "Invalid token.");
        if (!string.Equals(req.SelectedProfile, user.Uuid, StringComparison.OrdinalIgnoreCase))
            throw new YggdrasilException("IllegalArgumentException", "Invalid profile.");
        _joins.Upsert(user.Username, req.ServerId);
        Log.Info("Yggdrasil", $"join user={user.Username} server={req.ServerId}");
    }

    //HasJoined 查 30 秒内 join 记录 命中后删除避免重复返回 profile
    //unsigned true 时 textures 属性不带签名
    public FullProfileDto? HasJoined(string username, string serverId, string host, bool unsigned = false)
    {
        _joins.CleanupExpired();
        var join = _joins.FindRecent(username, serverId);
        if (join == null) return null;
        var user = _users.FindByUsername(username);
        if (user == null) return null;
        _joins.Delete(username, serverId);
        return BuildFullProfile(user, host, unsigned);
    }

    //GetProfile 按 uuid 查用户返回完整档案 unsigned true 时不签名
    public FullProfileDto GetProfile(string uuid, string host, bool unsigned = false)
    {
        var user = _users.FindByUuid(uuid);
        if (user == null)
            throw new YggdrasilException("GameProfileNotFoundException", "Profile not found.");
        return BuildFullProfile(user, host, unsigned);
    }

    //FindProfilesByNames 批量按用户名查档案 返回存在的 profile 列表
    public List<ProfileDto> FindProfilesByNames(string[] names)
    {
        var result = new List<ProfileDto>();
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var user = _users.FindByUsername(name);
            if (user != null)
                result.Add(BuildProfile(user));
        }
        return result;
    }

    //NewToken 生成无连字符 32 位 hex UUID
    private static string NewToken() => Guid.NewGuid().ToString("N");

    //BuildProfile 构造简要档案
    private static ProfileDto BuildProfile(UserAccount user) => new() { Id = user.Uuid, Name = user.Username };

    //BuildUserInfo 构造 requestUser=true 时的 user 信息
    private static UserInfoDto BuildUserInfo(UserAccount user) => new() { Id = user.Uuid };

    //BuildFullProfile 构造完整档案 附加 textures 属性 base64 value 含签名
    private FullProfileDto BuildFullProfile(UserAccount user, string host, bool unsigned = false)
    {
        var profile = new FullProfileDto { Id = user.Uuid, Name = user.Username };
        //textures 属性 由 ProfileService 构建 含贴图 URL base64 与 RSA 签名
        var textures = _profileService.BuildTexturesProperty(user, host, unsigned);
        if (textures != null) profile.Properties.Add(textures);
        return profile;
    }
}
