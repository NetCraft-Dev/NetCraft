using System.Data;

namespace NetCraft.TPGA.Db;

//SchemaInitializer 数据库表结构初始化
//管理库 admin_accounts(管理员 web 登录) + api_accounts(wss 握手用)
//玩家库 users(游戏账户 Yggdrasil) + access_tokens + profiles + server_joins
//首次启动建表 使用 IF NOT EXISTS 幂等可重复执行
//玩家库与主库物理隔离 由两个 IDbConnectionFactory 分别初始化
public static class SchemaInitializer
{
    //InitializeAdmin 主库建表 管理员账户与 api 账户
    //建表后跑迁移补 enabled language 列 老库 IF NOT EXISTS 跳过建表 依赖迁移补列
    public static void InitializeAdmin(IDbConnectionFactory factory)
    {
        using var conn = factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = _adminSchemas;
        cmd.ExecuteNonQuery();
        Migrate(conn, "api_accounts", "enabled", "INTEGER NOT NULL DEFAULT 1");
        //管理员语言偏好 NULL 表示未设置 前端按浏览器语言请求
        Migrate(conn, "admin_accounts", "language", "TEXT");
    }

    //InitializePlayer 玩家库建表 游戏账户令牌档案与服务器加入记录
    public static void InitializePlayer(IDbConnectionFactory factory)
    {
        using var conn = factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = _playerSchemas;
        cmd.ExecuteNonQuery();
        Migrate(conn, "users", "enabled", "INTEGER NOT NULL DEFAULT 1");
        //email 登录账号 PCL-CE 邮箱框填此字段 authenticate 按 email 或 username 查 老库 ALTER 加列无 UNIQUE 靠应用层查重
        Migrate(conn, "users", "email", "TEXT");
        //email_verified 邮箱验证标志 阶段6邮箱验证启用 老库默认0未验证 不阻塞登录
        Migrate(conn, "users", "email_verified", "INTEGER NOT NULL DEFAULT 0");
        //oauth_provider/oauth_subject OAuth 第三方绑定 NULL 表示密码账户 非空表示 OAuth 账户
        //(provider, subject) 唯一 一个 OAuth 身份只能绑一个本地账户
        Migrate(conn, "users", "oauth_provider", "TEXT");
        Migrate(conn, "users", "oauth_subject", "TEXT");
        //language/theme 玩家个性化偏好 NULL 未设置 前端按浏览器/系统回退
        Migrate(conn, "users", "language", "TEXT");
        Migrate(conn, "users", "theme", "TEXT");
        MigrateIndex(conn, "idx_users_oauth", "users", "oauth_provider, oauth_subject");
        //profiles 扩展贴图元数据 皮肤 hash 模型 slim/classic 披风 hash
        Migrate(conn, "profiles", "skin_hash", "TEXT");
        Migrate(conn, "profiles", "skin_model", "TEXT");
        Migrate(conn, "profiles", "cape_hash", "TEXT");
    }

    //Migrate 幂等加列 列已存在时 SQLite 抛 duplicate column 异常 忽略
    private static void Migrate(IDbConnection conn, string table, string column, string definition)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            //列已存在 忽略
        }
    }

    //MigrateIndex 幂等建索引 CREATE INDEX IF NOT EXISTS 已存在时跳过
    private static void MigrateIndex(IDbConnection conn, string name, string table, string columns)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS {name} ON {table}({columns}) WHERE oauth_provider IS NOT NULL";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            //索引已存在或语法不支持 忽略
        }
    }

    //_adminSchemas 管理库表
    //admin_accounts 单个管理员 web 后台登录 Argon2id must_change_password 首登改密 language 语言偏好
    //api_accounts wss 握手用 Argon2id 由管理员后台添加
    private static readonly string _adminSchemas = """
        CREATE TABLE IF NOT EXISTS admin_accounts (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT NOT NULL UNIQUE,
            password_hash TEXT NOT NULL,
            must_change_password INTEGER NOT NULL DEFAULT 0,
            language TEXT,
            created_at TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE TABLE IF NOT EXISTS api_accounts (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT NOT NULL UNIQUE,
            password_hash TEXT NOT NULL,
            enabled INTEGER NOT NULL DEFAULT 1,
            created_at TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    //_playerSchemas 玩家库表
    //server_joins 不设 TTL 字段 MVP 由应用层清理或后续加触发器
    private static readonly string _playerSchemas = """
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            uuid TEXT NOT NULL UNIQUE,
            username TEXT NOT NULL UNIQUE,
            password_hash TEXT,
            enabled INTEGER NOT NULL DEFAULT 1,
            email TEXT UNIQUE,
            oauth_provider TEXT,
            oauth_subject TEXT,
            created_at TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_users_oauth ON users(oauth_provider, oauth_subject) WHERE oauth_provider IS NOT NULL;

        CREATE TABLE IF NOT EXISTS access_tokens (
            token TEXT PRIMARY KEY,
            user_id INTEGER NOT NULL,
            client_token TEXT,
            created_at TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS profiles (
            user_id INTEGER PRIMARY KEY,
            properties TEXT NOT NULL DEFAULT '[]',
            skin_hash TEXT,
            skin_model TEXT,
            cape_hash TEXT,
            FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS server_joins (
            username TEXT NOT NULL,
            server_id TEXT NOT NULL,
            joined_at TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (username, server_id)
        );

        CREATE INDEX IF NOT EXISTS idx_access_tokens_user ON access_tokens(user_id);
        CREATE INDEX IF NOT EXISTS idx_server_joins_server ON server_joins(server_id);
        """;
}
