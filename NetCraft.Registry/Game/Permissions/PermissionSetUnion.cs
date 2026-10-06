namespace NetCraft.Registry;

//PermissionSetUnion permission set union, maps to vanilla net.minecraft.server.permissions.PermissionSetUnion
//Members are deduplicated by reference while preserving insertion order, equivalent to vanilla's ReferenceArraySet
//A union must not contain another union and it throws at construction, maps to vanilla ensureNoUnionsWithinUnions
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

    //GetPermissions member snapshot for tests, maps to vanilla getPermissions
    public IReadOnlyList<PermissionSet> GetPermissions() => _permissions.ToList();

    //Add deduplicates by reference
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
