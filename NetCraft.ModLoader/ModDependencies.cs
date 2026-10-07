namespace NetCraft.ModLoader;

//ModDependencies: evaluation of dependency version constraints in the manifest
//Dependencies not listed in depends are not version-checked, and dependencies absent on the current side are skipped too, since assembly resolution reports those
internal static class ModDependencies
{
    //Filter: picks out mods with unmet dependency versions and returns the ones that pass
    //skipped collects the ids of dropped mods, errors collects the corresponding reasons
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

    //UnmetReason: returns the description of the first unmet dependency, or null if all are met
    private static string? UnmetReason(ModManifest manifest, Dictionary<string, ModManifest> versions)
    {
        foreach (var (name, constraint) in manifest.Depends)
        {
            if (!versions.TryGetValue(name, out var dependency))
                continue;
            if (VersionConstraint.Matches(constraint, dependency.Version))
                continue;

            var actual = dependency.Version.Length > 0 ? dependency.Version : "version not declared";
            return $"mod {manifest.Id} requires dependency {name} {constraint} but found {actual}";
        }
        return null;
    }
}
