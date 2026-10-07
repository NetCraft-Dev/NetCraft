using System.Reflection;
using System.Runtime.Loader;
using Lead.Hook;
using NetCraft.Logging;

namespace NetCraft.ModLoader;

//ModHooks: injection assembly, converts a mod's hook list into Lead.Hook rules
//Assembly must precede the resolution of kernel sub-libraries; once a sub-library comes in there is no chance to rewrite
//The replacement class must not reference kernel types; resolving it also resolves base classes and may pull the kernel up early
public sealed class ModHooks
{
    private readonly HookEngine _engine;
    private readonly HashSet<string> _targets;
    private readonly HashSet<string> _runtimeTargets;
    private readonly Dictionary<string, string> _assemblyPaths;

    private ModHooks(HookEngine engine, HashSet<string> targets, HashSet<string> runtimeTargets,
        Dictionary<string, string> assemblyPaths, List<string> errors, List<string> warnings)
    {
        _engine = engine;
        _targets = targets;
        _runtimeTargets = runtimeTargets;
        _assemblyPaths = assemblyPaths;
        Errors = errors;
        Warnings = warnings;
    }

    //Errors: problems during assembly; a single faulty rule is skipped without affecting the rest
    public List<string> Errors { get; }

    //Warnings: problems that do not cause failure but should be known, currently only multiple mods competing for the same injection point
    //Such rules do not error, only the later-assembled one does not apply, which no one would notice if not recorded
    public List<string> Warnings { get; }

    //TargetAssemblies: assembly names matched by load-time rewriting rules
    public IReadOnlyCollection<string> TargetAssemblies => _targets;

    //RuntimeTargets: assembly names matched by runtime injection rules
    //Such assemblies do not go through pre-load rewriting; ApplyRuntimeInjects submits them to ReJIT after they load
    public IReadOnlyCollection<string> RuntimeTargets => _runtimeTargets;

    //Rules: the assembled rules
    public IReadOnlyList<HookRule> Rules => _engine.Rules;

    //Mixins: the assembled mixin rules
    public IReadOnlyList<MixinRule> Mixins => _engine.Mixins;

    //Build: assembles injection rules from the static scan result
    //kernelAssemblies are kernel assembly names, used to build the type-to-assembly index
    //Mod assemblies enter the index too, so mods can inject into each other
    //The index must come from reading metadata tables; namespace prefixes and assemblies do not correspond one to one and guessing the prefix would apply rules to the wrong assembly
    //No mod assembly is loaded throughout; once a target is loaded early there is no chance to rewrite it
    public static ModHooks Build(IEnumerable<ScannedMod> mods, IEnumerable<string> kernelAssemblies, ModEnvironment environment)
    {
        var modList = mods.ToList();
        var builder = new HookBuilder();
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var runtimeTargets = new HashSet<string>(StringComparer.Ordinal);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();
        var warnings = new List<string>();
        //Injection point to the mod that claimed it first, used to detect multiple mods competing for the same place
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var mod in modList)
            paths[mod.AssemblyName] = mod.AssemblyPath;

        //The kernel is scanned first so a mod's same-named type does not override it
        var typeIndex = KernelTypeIndex.Build(kernelAssemblies, ModBootstrap.ReadKernelAssembly);
        foreach (var mod in modList)
        {
            try
            {
                KernelTypeIndex.Add(typeIndex, mod.AssemblyName, File.ReadAllBytes(mod.AssemblyPath));
            }
            catch (Exception ex)
            {
                errors.Add($"mod {mod.Manifest.Id} failed to read metadata {ex.Message}");
            }
        }

        if (modList.All(m => m.Manifest.Hooks.Count == 0 && m.AnnotatedHooks.Count == 0
                             && m.Manifest.Mixins.Count == 0 && m.AnnotatedMixins.Count == 0))
            return new ModHooks(builder.Build(), targets, runtimeTargets, paths, errors, warnings);

        //Mixin target to the mod that claimed it first, tracked separately from hooks since they are different things
        var mixinOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var mod in modList)
        {
            //Annotations sit next to the replacement method or source class and follow renames, so when both sides declare the same target the annotation wins
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hook in mod.AnnotatedHooks)
                AddRule(builder, typeIndex, targets, runtimeTargets, errors, warnings, owners, mod, hook, environment, declared);

            foreach (var hook in mod.Manifest.Hooks)
                AddRule(builder, typeIndex, targets, runtimeTargets, errors, warnings, owners, mod, hook, environment, declared);

            //One mod can mix two sources into the same target, so the dedup key must include the source
            var declaredMixins = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mixin in mod.AnnotatedMixins)
                AddMixin(builder, typeIndex, targets, errors, warnings, mixinOwners, mod, mixin, declaredMixins);

            foreach (var mixin in mod.Manifest.Mixins)
                AddMixin(builder, typeIndex, targets, errors, warnings, mixinOwners, mod, mixin, declaredMixins);
        }

        var engine = builder.Build();
        //Replacement sources load on demand, pulled in only when a rewrite hits, so the rule table can be built before any mod assembly
        foreach (var name in paths.Keys)
        {
            var path = paths[name];
            engine.RegisterReplacementSource(name, () => ModAssemblies.Load(name, path));
            //Mixing needs to read the source class definition without loading it, so bytes are handed to the engine to parse itself
            engine.RegisterMixinSource(name, () => File.ReadAllBytes(path));
        }

        //When the two application modes land on the same assembly, the method body submitted by runtime injection is rewritten from the original bytes
        //Changes made to the same method by load-time rewriting are entirely overwritten by it; such combinations can only be warned about, since assembly time cannot tell whether two rules hit the same method
        foreach (var name in runtimeTargets)
        {
            if (targets.Contains(name))
                warnings.Add($"assembly {name} has both load-time rewriting and runtime injection rules; on methods rewritten by both, the load-time changes are overwritten by runtime injection");
        }

        return new ModHooks(engine, targets, runtimeTargets, paths, errors, warnings);
    }

    //AddRule: merges one rule into the assembly
    //Annotations and the manifest go through the same entry point; the only difference is that annotations are processed first, so the annotation wins at the same injection point
    //declared records injection points already claimed by this mod; the same target and form count as one point
    private static void AddRule(
        HookBuilder builder,
        Dictionary<string, string> typeIndex,
        HashSet<string> targets,
        HashSet<string> runtimeTargets,
        List<string> errors,
        List<string> warnings,
        Dictionary<string, string> owners,
        ScannedMod mod,
        ModHookRule hook,
        ModEnvironment environment,
        HashSet<string> declared)
    {
        if (!hook.Environment.Matches(environment))
            return;

        if (!Enum.TryParse<HookType>(hook.HookTypeName, ignoreCase: true, out var hookType))
        {
            errors.Add($"unrecognized injection type {hook.HookTypeName} in mod {mod.Manifest.Id}");
            return;
        }

        if (!Enum.TryParse<PatchMode>(hook.PatchModeName, ignoreCase: true, out var patchMode))
        {
            errors.Add($"unrecognized patch mode {hook.PatchModeName} in mod {mod.Manifest.Id}");
            return;
        }

        if (!Enum.TryParse<HookPlacement>(hook.PlacementName, ignoreCase: true, out var placement))
        {
            errors.Add($"unrecognized placement {hook.PlacementName} in mod {mod.Manifest.Id}");
            return;
        }

        if (!declared.Add($"{hook.Target}|{hook.Method}|{hookType}"))
            return;

        if (!typeIndex.TryGetValue(hook.Target, out var target))
        {
            errors.Add($"injection target {hook.Target} of mod {mod.Manifest.Id} is in no known assembly");
            return;
        }

        builder.AddRule(new HookRule(
            hook.Target, hook.Method,
            mod.AssemblyName, hook.ReplaceType, hook.ReplaceMethod,
            hookType, patchMode, null, hook.Label, ordinal: hook.Ordinal,
            localIndex: hook.LocalIndex, constantValue: hook.ConstantValueObject,
            inType: hook.InType, inMethod: hook.InMethod, placement: placement,
            argumentIndex: hook.ArgumentIndex, sliceFrom: hook.SliceFrom, sliceTo: hook.SliceTo));

        //The two application modes are tracked separately: the static one goes through pre-load rewriting, the runtime one is submitted after the target loads
        if (patchMode == PatchMode.RuntimeInject)
            runtimeTargets.Add(target);
        else
            targets.Add(target);

        //When two mods declare the same injection point, only the one assembled first applies
        //Recorded here so the displaced mod gets at least a hint
        var anchor = $"{hook.Target}::{hook.Method}[{hookType}/{patchMode}]";
        if (owners.TryGetValue(anchor, out var owner))
        {
            warnings.Add($"injection {anchor} of mod {mod.Manifest.Id} is already claimed by mod {owner}, this rule will not apply");
        }
        else
        {
            owners[anchor] = mod.Manifest.Id;
        }

        //Logs each rule at debug level so injection troubleshooting shows at a glance which rule comes from which mod
        Log.Debug($"Mod {mod.Manifest.Id} injects {target}!{hook.Target}::{hook.Method} replaced by {hook.ReplaceType}::{hook.ReplaceMethod} [{hookType}/{patchMode}]");
    }

    //AddMixin: merges one mixin rule into the assembly
    //The source type is fixed in this mod's own assembly, so no assembly name is needed
    //Mixing only takes effect for load-time rewriting and the target joins the static list, unrelated to runtime injection
    private static void AddMixin(
        HookBuilder builder,
        Dictionary<string, string> typeIndex,
        HashSet<string> targets,
        List<string> errors,
        List<string> warnings,
        Dictionary<string, string> owners,
        ScannedMod mod,
        ModMixinRule mixin,
        HashSet<string> declared)
    {
        if (mixin.Target.Length == 0 || mixin.Source.Length == 0)
        {
            errors.Add($"mixin rule of mod {mod.Manifest.Id} is missing a target or source");
            return;
        }

        if (!declared.Add($"{mixin.Target}|{mixin.Source}"))
            return;

        if (!typeIndex.TryGetValue(mixin.Target, out var targetAssembly))
        {
            errors.Add($"mixin target {mixin.Target} of mod {mod.Manifest.Id} is in no known assembly");
            return;
        }

        //Interfaces get their assembly name from the type index; if not found it is left empty for the engine to locate within the current module
        var interfaces = mixin.Interfaces
            .Select(name => typeIndex.TryGetValue(name, out var assembly)
                ? new TypeRef(name, assembly)
                : new TypeRef(name))
            .ToList();

        builder.AddMixin(new MixinRule(mixin.Target, mod.AssemblyName, mixin.Source, interfaces));
        targets.Add(targetAssembly);

        //When two mods mix into the same target both apply, only the later one's name-colliding members are skipped, gentler than hooks
        if (owners.TryGetValue(mixin.Target, out var owner))
            warnings.Add($"mixin {mixin.Target} of mod {mod.Manifest.Id} is already claimed by mod {owner}, only the first-assembled one's same-named members apply");
        else
            owners[mixin.Target] = mod.Manifest.Id;

        Log.Debug($"Mod {mod.Manifest.Id} mixes {mod.AssemblyName}!{mixin.Source} into {mixin.Target}");
    }

    //Rewrite: the rewriter handed to the main library's EmbeddedAssemblyLoader and mod loading
    //Assemblies with no matching rule are returned as-is, avoiding a wasted Cecil pass
    public byte[] Rewrite(string assemblyName, byte[] bytes)
        => _targets.Contains(assemblyName) ? _engine.Rewrite(bytes) : bytes;

    //ApplyRuntimePatches: applies RuntimePatch rules
    //Must be called only after all kernel assemblies are loaded; at rewrite time the target type is not in yet and cannot be found
    public void ApplyRuntimePatches() => _engine.ApplyRuntimePatches();

    //ApplyRuntimeInjects: submits runtime injection rules to the CLR's ReJIT
    //Only targets already loaded at this moment are recognized; an unloaded assembly does not even give a module name, and the native layer has nothing to match
    //The whole batch is skipped when the native injection layer is not attached; such rules do not error, leaving only a warning
    public void ApplyRuntimeInjects()
    {
        if (_runtimeTargets.Count == 0)
            return;

        if (!RuntimeInjector.IsAvailable)
        {
            Warnings.Add($"{_runtimeTargets.Count} assemblies declare runtime injection but the native injection layer is not attached to the process, this batch will not apply this run");
            return;
        }

        foreach (var name in _runtimeTargets)
        {
            var assembly = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => a.GetName().Name == name);
            if (assembly is null)
            {
                Warnings.Add($"runtime injection target {name} is not loaded yet, skipping this run");
                continue;
            }

            var bytes = ReadOriginalBytes(name);
            if (bytes is null)
            {
                Warnings.Add($"cannot get original bytes for runtime injection target {name}, skipping this run");
                continue;
            }

            try
            {
                //What is submitted is the method body rewritten from the original bytes, without load-time rewrite changes
                var injected = RuntimeInjector.Inject(bytes, assembly.ManifestModule.Name, _engine);
                Log.Info($"Runtime injected {injected.Count} methods into {name}");
            }
            catch (Exception ex)
            {
                Errors.Add($"runtime injection into {name} failed {ex.Message}");
            }
        }
    }

    //ReadOriginalBytes: gets the assembly's original bytes, mod directory first with kernel embedded resources as fallback
    private byte[]? ReadOriginalBytes(string name)
    {
        if (_assemblyPaths.TryGetValue(name, out var path) && File.Exists(path))
            return File.ReadAllBytes(path);
        return ModBootstrap.ReadKernelAssembly(name);
    }

    //PreloadReplacers: preloads replacement assemblies
    //The injected side needs to resolve the replacement method while being rewritten, so the replacement must be present at that moment
    //When the replacement is itself injected its load recurses through the same rewriting, so load order need not be pre-arranged
    public void PreloadReplacers()
    {
        foreach (var rule in _engine.Rules)
        {
            if (!_assemblyPaths.TryGetValue(rule.ReplacementAssembly, out var path))
            {
                Errors.Add($"replacement assembly {rule.ReplacementAssembly} of rule {rule} is not in the mod directory");
                continue;
            }

            try
            {
                ModAssemblies.Load(rule.ReplacementAssembly, path);
            }
            catch (Exception ex)
            {
                Errors.Add($"replacement assembly {rule.ReplacementAssembly} failed to load {ex.Message}");
            }
        }
    }
}
