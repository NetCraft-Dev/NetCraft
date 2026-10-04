using NetCraft.TPGA.Logging;

namespace NetCraft.TPGA.Auth;

//AdminBootstrap 管理员账户引导
//admin_accounts 表为空时插入默认 admin/admin must_change_password=1
//首登后强制改密 由后台 UpdatePassword 清除标志
public static class AdminBootstrap
{
    //EnsureDefaultAdmin 表为空则创建默认管理员
    public static void EnsureDefaultAdmin(AdminRepository repo)
    {
        if (repo.Count() > 0) return;
        var hash = PasswordHasher.HashAdmin("admin");
        repo.Insert("admin", hash, mustChange: true);
        Log.Info("Admin", "default admin/admin created, change on first login");
    }
}
