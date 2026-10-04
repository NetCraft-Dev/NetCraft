namespace NetCraft.ModLoader;

//ModDependencies 清单里依赖版本约束的判定
//depends 没列到的依赖不查版本 依赖的模组不在当前端时也跳过 那种情况由程序集解析去报错
internal static class ModDependencies
{
    //Filter 挑出依赖版本不满足的模组 返回通过的那批
    //skipped 收被剔掉的模组 id errors 收对应的原因
    public static List<ModManifest> Filter(
        IReadOnlyList<ModManifest> manifests,
        ICollection<string> skipped,
        ICollection<string> errors)
    {
        var versions = new Dictionary<string, ModManifest>(StringComparer.Ordinal);
        foreach (var manifest in manifests)
            versions[manifest.Id] = manifest;

        var kept = new List<ModManifest>(manifests.Count);
        foreach (var manifest in manifests)
        {
            var reason = UnmetReason(manifest, versions);
            if (reason is null)
            {
                kept.Add(manifest);
                continue;
            }
            skipped.Add(manifest.Id);
            errors.Add(reason);
        }
        return kept;
    }

    //UnmetReason 返回第一个不满足的依赖描述 全部满足返回 null
    private static string? UnmetReason(ModManifest manifest, Dictionary<string, ModManifest> versions)
    {
        foreach (var (name, constraint) in manifest.Depends)
        {
            if (!versions.TryGetValue(name, out var dependency))
                continue;
            if (VersionConstraint.Matches(constraint, dependency.Version))
                continue;

            var actual = dependency.Version.Length > 0 ? dependency.Version : "未声明版本";
            return $"模组 {manifest.Id} 要求依赖 {name} {constraint} 实际为 {actual}";
        }
        return null;
    }
}
