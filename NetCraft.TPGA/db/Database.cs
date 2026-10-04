using Microsoft.Data.Sqlite;
using System.Data;

namespace NetCraft.TPGA.Db;

//IDbConnectionFactory 数据库连接工厂抽象
//便于后续切换 provider 不改业务代码
public interface IDbConnectionFactory
{
    //Create 创建并打开的连接
    IDbConnection Create();
}

//SqliteConnectionFactory SQLite 实现
//连接串 sqlite:filepath 解析后转 Microsoft.Data.Sqlite 格式
//每次打开启用 WAL 与外键约束
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = ParseConnectionString(connectionString);
    }

    public IDbConnection Create()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    //ParseConnectionString 解析 sqlite: 前缀 转 Data Source 连接串
    //相对路径基于程序目录 AppContext.BaseDirectory 不基于 cwd
    //确保 tpga.db 与 tpga.yaml 同目录
    private static string ParseConnectionString(string connectionString)
    {
        const string prefix = "sqlite:";
        if (connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var path = connectionString[prefix.Length..];
            if (!Path.IsPathRooted(path))
                path = Path.Combine(AppContext.BaseDirectory, path);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return $"Data Source={path}";
        }
        //非前缀视为原始连接串
        return connectionString;
    }
}

//Database 静态工厂 按 connection string 前缀分发 provider
public static class Database
{
    //CreateFactory 按配置创建连接工厂 MVP 仅 sqlite
    public static IDbConnectionFactory CreateFactory(string connectionString)
    {
        if (connectionString.StartsWith("sqlite:", StringComparison.OrdinalIgnoreCase))
            return new SqliteConnectionFactory(connectionString);
        throw new NotSupportedException($"unsupported database provider: {connectionString}");
    }
}
