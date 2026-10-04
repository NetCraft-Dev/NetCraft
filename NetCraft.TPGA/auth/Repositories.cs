using Dapper;
using NetCraft.TPGA.Db;

namespace NetCraft.TPGA.Auth;

//AdminAccount 管理员账户实体 映射 admin_accounts 表
public sealed class AdminAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    //sqlite INTEGER 0/1 Dapper 映射到 bool
    public bool MustChangePassword { get; set; }
    //语言偏好 NULL 表示未设置 前端按浏览器语言请求 切换后固化
    public string? Language { get; set; }
}

//UserAccount 游戏账户实体 映射 users 表
public sealed class UserAccount
{
    public int Id { get; set; }
    public string Uuid { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    //sqlite INTEGER 0/1 Dapper 映射 bool 禁用后 Yggdrasil 登录拒绝
    public bool Enabled { get; set; } = true;
    //登录账号 PCL-CE 邮箱框填此值 authenticate 按 email 或 username 查 老账户 NULL 用 username 兜底
    public string? Email { get; set; }
    //邮箱验证标志 0未验证 1已验证 阶段6邮箱验证启用 不阻塞登录
    public bool EmailVerified { get; set; }
    //oauth_provider/oauth_subject OAuth 绑定 NULL 密码账户 非空 OAuth 账户
    //一个本地账户最多绑一个 OAuth 身份 一一对应
    public string? OauthProvider { get; set; }
    public string? OauthSubject { get; set; }
    //注册时间 Home 账户安全卡展示
    public string CreatedAt { get; set; } = "";
    //language/theme 玩家个性化偏好 NULL 未设置 前端按浏览器/系统回退
    public string? Language { get; set; }
    public string? Theme { get; set; }
}

//AdminRepository admin_accounts 仓储
public sealed class AdminRepository
{
    private readonly IDbConnectionFactory _factory;

    public AdminRepository(IDbConnectionFactory factory) => _factory = factory;

    //FindByUsername 按用户名查管理员
    public AdminAccount? FindByUsername(string username)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<AdminAccount>(
            "SELECT id, username, password_hash AS PasswordHash, must_change_password AS MustChangePassword, language AS Language FROM admin_accounts WHERE username=@u",
            new { u = username });
    }

    //FindById 按 id 查管理员
    public AdminAccount? FindById(int id)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<AdminAccount>(
            "SELECT id, username, password_hash AS PasswordHash, must_change_password AS MustChangePassword, language AS Language FROM admin_accounts WHERE id=@i",
            new { i = id });
    }

    //Insert 插入管理员
    public void Insert(string username, string passwordHash, bool mustChange)
    {
        using var conn = _factory.Create();
        conn.Execute(
            "INSERT INTO admin_accounts (username, password_hash, must_change_password) VALUES (@u, @p, @m)",
            new { u = username, p = passwordHash, m = mustChange ? 1 : 0 });
    }

    //UpdatePassword 更新密码并清除改密标志
    public void UpdatePassword(int id, string passwordHash)
    {
        using var conn = _factory.Create();
        conn.Execute(
            "UPDATE admin_accounts SET password_hash=@p, must_change_password=0 WHERE id=@i",
            new { p = passwordHash, i = id });
    }

    //UpdateLanguage 固化管理员语言偏好 前端切换语言后调用
    public void UpdateLanguage(int id, string language)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE admin_accounts SET language=@l WHERE id=@i", new { l = language, i = id });
    }

    //Count 管理员总数
    public int Count()
    {
        using var conn = _factory.Create();
        return conn.ExecuteScalar<int>("SELECT COUNT(*) FROM admin_accounts");
    }

    //ListAll 列出所有管理员 不含密码
    public IEnumerable<AdminAccount> ListAll()
    {
        using var conn = _factory.Create();
        return conn.Query<AdminAccount>(
            "SELECT id, username, password_hash AS PasswordHash, must_change_password AS MustChangePassword, language AS Language FROM admin_accounts ORDER BY id");
    }

    //Delete 删除管理员
    public void Delete(int id)
    {
        using var conn = _factory.Create();
        conn.Execute("DELETE FROM admin_accounts WHERE id=@i", new { i = id });
    }
}

//UserRepository users 仓储 游戏账户
public sealed class UserRepository
{
    private readonly IDbConnectionFactory _factory;

    public UserRepository(IDbConnectionFactory factory) => _factory = factory;

    //FindByUsername 按用户名查游戏账户 档案查询与重名检查用
    public UserAccount? FindByUsername(string username)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<UserAccount>(
            "SELECT id, uuid, username, password_hash AS PasswordHash, enabled AS Enabled, email AS Email FROM users WHERE username=@u",
            new { u = username });
    }

    //FindByLogin 按登录账号查 authenticate/signout 用 PCL-CE 邮箱框填值先匹 email 再匹 username 兼容老账户
    public UserAccount? FindByLogin(string login)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<UserAccount>(
            "SELECT id, uuid, username, password_hash AS PasswordHash, enabled AS Enabled, email AS Email, email_verified AS EmailVerified FROM users WHERE email=@l OR username=@l",
            new { l = login });
    }

    //FindByEmail 按邮箱查 AddPlayer 邮箱查重用
    public UserAccount? FindByEmail(string email)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<UserAccount>(
            "SELECT id, uuid, username, password_hash AS PasswordHash, enabled AS Enabled, email AS Email FROM users WHERE email=@l",
            new { l = email });
    }

    //FindByUuid 按 uuid 查游戏账户
    public UserAccount? FindByUuid(string uuid)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<UserAccount>(
            "SELECT id, uuid, username, password_hash AS PasswordHash, enabled AS Enabled, email AS Email, email_verified AS EmailVerified FROM users WHERE uuid=@u",
            new { u = uuid });
    }

    //FindById 按 id 查游戏账户 refresh/join 用 含 oauth/language/theme 字段供偏好读取
    public UserAccount? FindById(int id)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<UserAccount>(
            "SELECT id, uuid, username, password_hash AS PasswordHash, enabled AS Enabled, email AS Email, email_verified AS EmailVerified, oauth_provider AS OauthProvider, oauth_subject AS OauthSubject, created_at AS CreatedAt, language AS Language, theme AS Theme FROM users WHERE id=@i",
            new { i = id });
    }

    //Insert 插入游戏账户 返回自增 id email 登录账号可空兼容老流程
    public int Insert(string uuid, string username, string passwordHash, string? email)
    {
        using var conn = _factory.Create();
        return conn.ExecuteScalar<int>(
            "INSERT INTO users (uuid, username, password_hash, email) VALUES (@u, @n, @p, @e); SELECT last_insert_rowid();",
            new { u = uuid, n = username, p = passwordHash, e = email });
    }

    //Delete 删除游戏账户
    public void Delete(int id)
    {
        using var conn = _factory.Create();
        conn.Execute("DELETE FROM users WHERE id=@i", new { i = id });
    }

    //UpdateEnabled 切换启用状态 禁用后 Yggdrasil authenticate 拒绝
    public void UpdateEnabled(int id, bool enabled)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE users SET enabled=@e WHERE id=@i", new { e = enabled ? 1 : 0, i = id });
    }

    //UpdateUuid 修改 uuid 关联表按 user_id 不受影响 textures value 动态读取自动更新
    public void UpdateUuid(int id, string uuid)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE users SET uuid=@u WHERE id=@i", new { u = uuid, i = id });
    }

    //UpdateUsername 修改用户名 唯一约束冲突由 SQLite 抛异常
    public void UpdateUsername(int id, string username)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE users SET username=@n WHERE id=@i", new { n = username, i = id });
    }

    //UpdatePassword 重置密码 管理员后台用 PBKDF2 哈希
    public void UpdatePassword(int id, string passwordHash)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE users SET password_hash=@p WHERE id=@i", new { p = passwordHash, i = id });
    }

    //ListAll 列出所有游戏账户
    public IEnumerable<UserAccount> ListAll()
    {
        using var conn = _factory.Create();
        return conn.Query<UserAccount>(
            "SELECT id, uuid, username, password_hash AS PasswordHash, enabled AS Enabled, email AS Email, email_verified AS EmailVerified FROM users ORDER BY id");
    }

    //UpdateEmail 修改登录邮箱 NULL 清空 唯一性由调用方 FindByEmail 预检
    public void UpdateEmail(int id, string? email)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE users SET email=@e WHERE id=@i", new { e = email, i = id });
    }

    //FindByOAuthSubject 按 OAuth 提供商与 subject 查 回调时判断是否已绑定本地账户
    public UserAccount? FindByOAuthSubject(string provider, string subject)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<UserAccount>(
            "SELECT id, uuid, username, password_hash AS PasswordHash, enabled AS Enabled, email AS Email, email_verified AS EmailVerified, oauth_provider AS OauthProvider, oauth_subject AS OauthSubject FROM users WHERE oauth_provider=@p AND oauth_subject=@s",
            new { p = provider, s = subject });
    }

    //CreateOAuthUser 插入 OAuth 账户 password_hash NULL 仅 OAuth 可登录 返回自增 id
    //username/email 由补全注册页确认后传入 oauth_provider/oauth_subject 绑定
    public int CreateOAuthUser(string uuid, string username, string? email, string oauthProvider, string oauthSubject)
    {
        using var conn = _factory.Create();
        return conn.ExecuteScalar<int>(
            "INSERT INTO users (uuid, username, password_hash, email, oauth_provider, oauth_subject) VALUES (@u, @n, NULL, @e, @op, @os); SELECT last_insert_rowid();",
            new { u = uuid, n = username, e = email, op = oauthProvider, os = oauthSubject });
    }

    //UpdatePreferences 更新玩家个性化偏好 language/theme 传 null 不改
    public void UpdatePreferences(int id, string? language, string? theme)
    {
        using var conn = _factory.Create();
        var sets = new List<string>();
        var args = new DynamicParameters();
        args.Add("i", id);
        if (language != null) { sets.Add("language=@l"); args.Add("l", language); }
        if (theme != null) { sets.Add("theme=@t"); args.Add("t", theme); }
        if (sets.Count == 0) return;
        conn.Execute($"UPDATE users SET {string.Join(", ", sets)} WHERE id=@i", args);
    }
}

//AccessTokenRecord access_tokens 实体
public sealed class AccessTokenRecord
{
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public string? ClientToken { get; set; }
}

//TokenRepository access_tokens 仓储
//token 主键 一个用户可有多条 refresh 时删旧建新
public sealed class TokenRepository
{
    private readonly IDbConnectionFactory _factory;

    public TokenRepository(IDbConnectionFactory factory) => _factory = factory;

    //Insert 新增令牌 同 user_id 旧令牌由调用方先 DeleteByUser 清理
    public void Insert(string token, int userId, string? clientToken)
    {
        using var conn = _factory.Create();
        conn.Execute(
            "INSERT INTO access_tokens (token, user_id, client_token) VALUES (@t, @u, @c)",
            new { t = token, u = userId, c = clientToken });
    }

    //Find 按 token 查令牌
    public AccessTokenRecord? Find(string token)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<AccessTokenRecord>(
            "SELECT token AS Token, user_id AS UserId, client_token AS ClientToken FROM access_tokens WHERE token=@t",
            new { t = token });
    }

    //Delete 删除单个令牌
    public void Delete(string token)
    {
        using var conn = _factory.Create();
        conn.Execute("DELETE FROM access_tokens WHERE token=@t", new { t = token });
    }

    //DeleteByUser 删除用户所有令牌 invalidate/refresh 场景
    public void DeleteByUser(int userId)
    {
        using var conn = _factory.Create();
        conn.Execute("DELETE FROM access_tokens WHERE user_id=@u", new { u = userId });
    }
}

//ProfileRepository profiles 仓储
//properties 存历史 JSON 留兼容 skin_hash/skin_model/cape_hash 存贴图元数据
public sealed class ProfileRepository
{
    private readonly IDbConnectionFactory _factory;

    public ProfileRepository(IDbConnectionFactory factory) => _factory = factory;

    //Get 取用户档案 properties JSON 无档案返回 null
    public string? Get(int userId)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<string?>(
            "SELECT properties FROM profiles WHERE user_id=@u", new { u = userId });
    }

    //Upsert 插入或更新档案
    public void Upsert(int userId, string propertiesJson)
    {
        using var conn = _factory.Create();
        conn.Execute(
            "INSERT INTO profiles (user_id, properties) VALUES (@u, @p) ON CONFLICT(user_id) DO UPDATE SET properties=@p",
            new { u = userId, p = propertiesJson });
    }

    //GetTextureMeta 取皮肤披风元数据 无档案返回空对象
    public TextureMeta GetTextureMeta(int userId)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<TextureMeta>(
            "SELECT skin_hash AS SkinHash, skin_model AS SkinModel, cape_hash AS CapeHash FROM profiles WHERE user_id=@u",
            new { u = userId }) ?? new TextureMeta();
    }

    //SetSkin 设皮肤 hash 与模型 无档案则插入
    public void SetSkin(int userId, string hash, string model)
    {
        using var conn = _factory.Create();
        conn.Execute(
            "INSERT INTO profiles (user_id, properties, skin_hash, skin_model) VALUES (@u, '[]', @h, @m) ON CONFLICT(user_id) DO UPDATE SET skin_hash=@h, skin_model=@m",
            new { u = userId, h = hash, m = model });
    }

    //SetCape 设披风 hash 无档案则插入
    public void SetCape(int userId, string hash)
    {
        using var conn = _factory.Create();
        conn.Execute(
            "INSERT INTO profiles (user_id, properties, cape_hash) VALUES (@u, '[]', @h) ON CONFLICT(user_id) DO UPDATE SET cape_hash=@h",
            new { u = userId, h = hash });
    }

    //ClearSkin 清除皮肤 元件置空
    public void ClearSkin(int userId)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE profiles SET skin_hash=NULL, skin_model=NULL WHERE user_id=@u", new { u = userId });
    }

    //ClearCape 清除披风
    public void ClearCape(int userId)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE profiles SET cape_hash=NULL WHERE user_id=@u", new { u = userId });
    }
}

//TextureMeta 贴图元数据 皮肤 hash 模型 slim/default 披风 hash
public sealed class TextureMeta
{
    public string? SkinHash { get; set; }
    public string? SkinModel { get; set; }
    public string? CapeHash { get; set; }
}

//ServerJoinRecord server_joins 实体
public sealed class ServerJoinRecord
{
    public string Username { get; set; } = "";
    public string ServerId { get; set; } = "";
    public string JoinedAt { get; set; } = "";
}

//ServerJoinRepository server_joins 仓储
//join 提交 serverId 后 30 秒内 hasJoined 可查 超时由查询条件过滤
public sealed class ServerJoinRepository
{
    private readonly IDbConnectionFactory _factory;

    public ServerJoinRepository(IDbConnectionFactory factory) => _factory = factory;

    //Upsert 插入或更新 server join 记录
    public void Upsert(string username, string serverId)
    {
        using var conn = _factory.Create();
        conn.Execute(
            "INSERT INTO server_joins (username, server_id) VALUES (@u, @s) ON CONFLICT(username, server_id) DO UPDATE SET joined_at=datetime('now')",
            new { u = username, s = serverId });
    }

    //FindRecent 查 30 秒内匹配记录 返回实体否则 null
    public ServerJoinRecord? FindRecent(string username, string serverId)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<ServerJoinRecord>(
            "SELECT username AS Username, server_id AS ServerId, joined_at AS JoinedAt FROM server_joins WHERE username=@u AND server_id=@s AND joined_at > datetime('now','-30 seconds')",
            new { u = username, s = serverId });
    }

    //Delete 清除指定记录 hasJoined 命中后调用避免重复查询
    public void Delete(string username, string serverId)
    {
        using var conn = _factory.Create();
        conn.Execute("DELETE FROM server_joins WHERE username=@u AND server_id=@s",
            new { u = username, s = serverId });
    }

    //CleanupExpired 清理 30 秒前的过期记录
    public void CleanupExpired()
    {
        using var conn = _factory.Create();
        conn.Execute("DELETE FROM server_joins WHERE joined_at <= datetime('now','-30 seconds')");
    }
}

//ApiAccount api_accounts 实体 映射主库
//wss 握手身份校验用 Argon2id 由管理员后台添加
public sealed class ApiAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    //sqlite INTEGER 0/1 Dapper 映射 bool 禁用后 wss 握手拒绝
    public bool Enabled { get; set; } = true;
}

//ApiAccountRepository api_accounts 仓储 主库
//wss 握手 Authenticate 按 username 查校验密码
public sealed class ApiAccountRepository
{
    private readonly IDbConnectionFactory _factory;

    public ApiAccountRepository(IDbConnectionFactory factory) => _factory = factory;

    //FindByUsername 按 username 查 api 账户
    public ApiAccount? FindByUsername(string username)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<ApiAccount>(
            "SELECT id, username, password_hash AS PasswordHash, created_at AS CreatedAt, enabled AS Enabled FROM api_accounts WHERE username=@u",
            new { u = username });
    }

    //FindById 按 id 查 api 账户
    public ApiAccount? FindById(int id)
    {
        using var conn = _factory.Create();
        return conn.QueryFirstOrDefault<ApiAccount>(
            "SELECT id, username, password_hash AS PasswordHash, created_at AS CreatedAt, enabled AS Enabled FROM api_accounts WHERE id=@i",
            new { i = id });
    }

    //Insert 新增 api 账户 返回自增 id
    public int Insert(string username, string passwordHash)
    {
        using var conn = _factory.Create();
        return conn.ExecuteScalar<int>(
            "INSERT INTO api_accounts (username, password_hash) VALUES (@u, @p); SELECT last_insert_rowid();",
            new { u = username, p = passwordHash });
    }

    //UpdatePassword 管理员后台改 api 账户密码 Argon2id
    public void UpdatePassword(int id, string passwordHash)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE api_accounts SET password_hash=@p WHERE id=@i", new { p = passwordHash, i = id });
    }

    //UpdateEnabled 切换启用状态 禁用后 wss 握手拒绝
    public void UpdateEnabled(int id, bool enabled)
    {
        using var conn = _factory.Create();
        conn.Execute("UPDATE api_accounts SET enabled=@e WHERE id=@i", new { e = enabled ? 1 : 0, i = id });
    }

    //Delete 删除 api 账户
    public void Delete(int id)
    {
        using var conn = _factory.Create();
        conn.Execute("DELETE FROM api_accounts WHERE id=@i", new { i = id });
    }

    //ListAll 列出所有 api 账户 不含密码
    public IEnumerable<ApiAccount> ListAll()
    {
        using var conn = _factory.Create();
        return conn.Query<ApiAccount>(
            "SELECT id, username, password_hash AS PasswordHash, created_at AS CreatedAt, enabled AS Enabled FROM api_accounts ORDER BY id");
    }
}
