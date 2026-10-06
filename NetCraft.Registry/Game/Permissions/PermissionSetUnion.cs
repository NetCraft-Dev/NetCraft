namespace NetCraft.Registry;

//PermissionSetUnion 权限集合并集对应原版 net.minecraft.server.permissions.PermissionSetUnion
//成员按引用去重保持插入顺序等价原版的 ReferenceArraySet
//并集内不许再嵌并集构造时直接炸 对应原版 ensureNoUnionsWithinUnions
public class PermissionSetUnion : PermissionSet
{
    private readonly List<PermissionSet> _permissions = [];

    internal PermissionSetUnion(PermissionSet first, PermissionSet second)
    {
        Add(first);
        Add(second);
        EnsureNoUnionsWithinUnions();
    }

    private PermissionSetUnion(IEnumerable<PermissionSet> oldPermissions, PermissionSet other)
    {
        _permissions.AddRange(oldPermissions);
        Add(other);
        EnsureNoUnionsWithinUnions();
    }

    private PermissionSetUnion(IEnumerable<PermissionSet> oldPermissions, IEnumerable<PermissionSet> other)
    {
        _permissions.AddRange(oldPermissions);
        foreach (var permission in other) Add(permission);
        EnsureNoUnionsWithinUnions();
    }

    public override bool HasPermission(Permission permission)
    {
        foreach (var set in _permissions)
            if (set.HasPermission(permission)) return true;
        return false;
    }

    public override PermissionSet Union(PermissionSet other)
        => other is PermissionSetUnion otherUnion
            ? new PermissionSetUnion(_permissions, otherUnion._permissions)
            : new PermissionSetUnion(_permissions, other);

    //GetPermissions 成员快照供测试对应原版 getPermissions
    public IReadOnlyList<PermissionSet> GetPermissions() => _permissions.ToList();

    //Add 按引用去重
    private void Add(PermissionSet set)
    {
        foreach (var existing in _permissions)
            if (ReferenceEquals(existing, set)) return;
        _permissions.Add(set);
    }

    private void EnsureNoUnionsWithinUnions()
    {
        foreach (var set in _permissions)
            if (set is PermissionSetUnion)
                throw new ArgumentException("Cannot have PermissionSetUnion within another PermissionSetUnion");
    }
}
