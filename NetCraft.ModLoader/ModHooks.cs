using System.Reflection;
using System.Runtime.Loader;
using Lead.Hook;
using NetCraft.Logging;

namespace NetCraft.ModLoader;

//ModHooks 注入装配 把模组的 hook 清单转成 Lead.Hook 规则
//装配必须早于内核子库被解析 子库一进来改写就没机会了
//替换类不要引用内核类型 解析它会连带解析基类 可能把内核提前拉起来
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

    //Errors 装配过程中的问题 单条规则出错只跳过它不影响其余
    public List<string> Errors { get; }

    //Warnings 不至于失败但需要让人知道的问题 目前只有多模组抢同一个注入点
    //这类规则不报错 只是后装配的那条不生效 不记下来没人看得出来
    public List<string> Warnings { get; }

    //TargetAssemblies 有加载时改写规则命中的程序集名
    public IReadOnlyCollection<string> TargetAssemblies => _targets;

    //RuntimeTargets 有运行时注入规则命中的程序集名
    //这类程序集不走加载前改写 由 ApplyRuntimeInjects 在它们加载之后提交给 ReJIT
    public IReadOnlyCollection<string> RuntimeTargets => _runtimeTargets;

    //Rules 已装配的规则
    public IReadOnlyList<HookRule> Rules => _engine.Rules;

    //Mixins 已装配的混入规则
    public IReadOnlyList<MixinRule> Mixins => _engine.Mixins;

    //Build 从静态扫描结果装配注入规则
    //kernelAssemblies 是内核程序集名 用来建立类型到程序集的索引
    //模组程序集也一并进索引 因此模组之间可以互相注入
    //索引必须读元数据表得来 命名空间前缀与程序集并不一一对应 猜前缀会把规则打到错的程序集上
    //全程不加载任何模组程序集 目标一旦被提前加载就再没有改写的机会了
    public static ModHooks Build(IEnumerable<ScannedMod> mods, IEnumerable<string> kernelAssemblies, ModEnvironment environment)
    {
        var modList = mods.ToList();
        var builder = new HookBuilder();
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var runtimeTargets = new HashSet<string>(StringComparer.Ordinal);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();
        var warnings = new List<string>();
        //注入点 到 先占用它的模组 用来发现多模组抢同一处
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var mod in modList)
            paths[mod.AssemblyName] = mod.AssemblyPath;

        //内核先扫 模组同名类型不会盖掉内核
        var typeIndex = KernelTypeIndex.Build(kernelAssemblies, ModBootstrap.ReadKernelAssembly);
        foreach (var mod in modList)
        {
            try
            {
                KernelTypeIndex.Add(typeIndex, mod.AssemblyName, File.ReadAllBytes(mod.AssemblyPath));
            }
            catch (Exception ex)
            {
                errors.Add($"模组 {mod.Manifest.Id} 读元数据失败 {ex.Message}");
            }
        }

        if (modList.All(m => m.Manifest.Hooks.Count == 0 && m.AnnotatedHooks.Count == 0
                             && m.Manifest.Mixins.Count == 0 && m.AnnotatedMixins.Count == 0))
            return new ModHooks(builder.Build(), targets, runtimeTargets, paths, errors, warnings);

        //混入目标 到 先占用它的模组 与 hooks 分开记 两者是不同的东西
        var mixinOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var mod in modList)
        {
            //注解写在替换方法或来源类旁边 改名时跟着走 所以同一个目标两边都声明时以注解为准
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hook in mod.AnnotatedHooks)
                AddRule(builder, typeIndex, targets, runtimeTargets, errors, warnings, owners, mod, hook, environment, declared);

            foreach (var hook in mod.Manifest.Hooks)
                AddRule(builder, typeIndex, targets, runtimeTargets, errors, warnings, owners, mod, hook, environment, declared);

            //同一个模组可以用两个来源混入同一个目标 去重键要带上来源
            var declaredMixins = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mixin in mod.AnnotatedMixins)
                AddMixin(builder, typeIndex, targets, errors, warnings, mixinOwners, mod, mixin, declaredMixins);

            foreach (var mixin in mod.Manifest.Mixins)
                AddMixin(builder, typeIndex, targets, errors, warnings, mixinOwners, mod, mixin, declaredMixins);
        }

        var engine = builder.Build();
        //替换方按需加载 改写命中时才现拉 所以规则表可以先于任何模组程序集建完
        foreach (var name in paths.Keys)
        {
            var path = paths[name];
            engine.RegisterReplacementSource(name, () => ModAssemblies.Load(name, path));
            //混入要读来源类的定义 不能把它加载起来 直接给字节让引擎自己解析
            engine.RegisterMixinSource(name, () => File.ReadAllBytes(path));
        }

        //两类落地方式碰在同一个程序集上时 运行时注入提交的方法体是从原始字节改出来的
        //加载期改写对同一个方法做过的改动会被它整份盖掉 这类组合只能提示 装配期看不出两条规则是不是命中同一个方法
        foreach (var name in runtimeTargets)
        {
            if (targets.Contains(name))
                warnings.Add($"程序集 {name} 同时有加载时改写与运行时注入两类规则 同被改写的方法上加载期改动会被运行时注入盖掉");
        }

        return new ModHooks(engine, targets, runtimeTargets, paths, errors, warnings);
    }

    //AddRule 把一条规则并进装配
    //注解与清单走的是同一个入口 唯一区别是注解先处理 于是同一个注入点注解说话算数
    //declared 记本模组已占用的注入点 目标与形态相同就算同一个点
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
            errors.Add($"模组 {mod.Manifest.Id} 的注入类型 {hook.HookTypeName} 无法识别");
            return;
        }

        if (!Enum.TryParse<PatchMode>(hook.PatchModeName, ignoreCase: true, out var patchMode))
        {
            errors.Add($"模组 {mod.Manifest.Id} 的补丁模式 {hook.PatchModeName} 无法识别");
            return;
        }

        if (!declared.Add($"{hook.Target}|{hook.Method}|{hookType}"))
            return;

        if (!typeIndex.TryGetValue(hook.Target, out var target))
        {
            errors.Add($"模组 {mod.Manifest.Id} 的注入目标 {hook.Target} 不在任何已知程序集里");
            return;
        }

        builder.AddRule(new HookRule(
            hook.Target, hook.Method,
            mod.AssemblyName, hook.ReplaceType, hook.ReplaceMethod,
            hookType, patchMode, null, hook.Label, ordinal: hook.Ordinal));

        //两类落地方式分开记 静态那条走加载前改写 运行时那条等目标加载完再提交
        if (patchMode == PatchMode.RuntimeInject)
            runtimeTargets.Add(target);
        else
            targets.Add(target);

        //同一个注入点被两个模组声明时 只有先装配的那条会落地
        //这里记一笔 否则被顶掉的那个模组连个提示都没有
        var anchor = $"{hook.Target}::{hook.Method}[{hookType}/{patchMode}]";
        if (owners.TryGetValue(anchor, out var owner))
        {
            warnings.Add($"模组 {mod.Manifest.Id} 的注入 {anchor} 已被模组 {owner} 占用 本条不会生效");
        }
        else
        {
            owners[anchor] = mod.Manifest.Id;
        }

        //逐条落 debug 日志 排查注入时能直接看出哪条规则来自哪个模组
        Log.Debug($"Mod {mod.Manifest.Id} injects {target}!{hook.Target}::{hook.Method} replaced by {hook.ReplaceType}::{hook.ReplaceMethod} [{hookType}/{patchMode}]");
    }

    //AddMixin 把一条混入规则并进装配
    //来源类型固定在本模组程序集里 所以不必写程序集名
    //混入只对加载期改写生效 目标进静态那本册子 与运行时注入无关
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
            errors.Add($"模组 {mod.Manifest.Id} 的混入规则缺少目标或来源");
            return;
        }

        if (!declared.Add($"{mixin.Target}|{mixin.Source}"))
            return;

        if (!typeIndex.TryGetValue(mixin.Target, out var targetAssembly))
        {
            errors.Add($"模组 {mod.Manifest.Id} 的混入目标 {mixin.Target} 不在任何已知程序集里");
            return;
        }

        //接口按类型索引补程序集名 查不到就留空交给引擎在当前模块里找
        var interfaces = mixin.Interfaces
            .Select(name => typeIndex.TryGetValue(name, out var assembly)
                ? new TypeRef(name, assembly)
                : new TypeRef(name))
            .ToList();

        builder.AddMixin(new MixinRule(mixin.Target, mod.AssemblyName, mixin.Source, interfaces));
        targets.Add(targetAssembly);

        //两个模组混入同一个目标时两条都会落地 只有成员撞名的那部分后者被跳过 比 hooks 那边温和
        if (owners.TryGetValue(mixin.Target, out var owner))
            warnings.Add($"模组 {mod.Manifest.Id} 的混入 {mixin.Target} 已被模组 {owner} 占用 同名成员只有先装配的那个会落地");
        else
            owners[mixin.Target] = mod.Manifest.Id;

        Log.Debug($"Mod {mod.Manifest.Id} mixes {mod.AssemblyName}!{mixin.Source} into {mixin.Target}");
    }

    //Rewrite 改写器 交给主库的 EmbeddedAssemblyLoader 与模组加载
    //没有规则命中的程序集原样返回 避免白跑一遍 Cecil
    public byte[] Rewrite(string assemblyName, byte[] bytes)
        => _targets.Contains(assemblyName) ? _engine.Rewrite(bytes) : bytes;

    //ApplyRuntimePatches 执行 RuntimePatch 规则
    //必须等内核程序集都加载完再调 改写那一刻目标类型还没进来 那时找不到
    public void ApplyRuntimePatches() => _engine.ApplyRuntimePatches();

    //ApplyRuntimeInjects 把运行时注入的规则提交给 CLR 的 ReJIT
    //只认此刻已经加载的目标 没加载的程序集连模块名都拿不到 提交上去原生层也无从匹配
    //原生注入层没挂时整批跳过 这类规则不报错 只留一句警告
    public void ApplyRuntimeInjects()
    {
        if (_runtimeTargets.Count == 0)
            return;

        if (!RuntimeInjector.IsAvailable)
        {
            Warnings.Add($"有 {_runtimeTargets.Count} 个程序集声明了运行时注入 但进程没挂原生注入层 这批规则本次不生效");
            return;
        }

        foreach (var name in _runtimeTargets)
        {
            var assembly = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => a.GetName().Name == name);
            if (assembly is null)
            {
                Warnings.Add($"运行时注入的目标 {name} 此刻还没加载 本次跳过");
                continue;
            }

            var bytes = ReadOriginalBytes(name);
            if (bytes is null)
            {
                Warnings.Add($"运行时注入的目标 {name} 取不到原始字节 本次跳过");
                continue;
            }

            try
            {
                //提交的是原始件改出来的方法体 不含加载期改写的改动
                var injected = RuntimeInjector.Inject(bytes, assembly.ManifestModule.Name, _engine);
                Log.Info($"Runtime injected {injected.Count} methods into {name}");
            }
            catch (Exception ex)
            {
                Errors.Add($"运行时注入 {name} 失败 {ex.Message}");
            }
        }
    }

    //ReadOriginalBytes 取程序集原始字节 模组目录优先 内核内嵌资源兜底
    private byte[]? ReadOriginalBytes(string name)
    {
        if (_assemblyPaths.TryGetValue(name, out var path) && File.Exists(path))
            return File.ReadAllBytes(path);
        return ModBootstrap.ReadKernelAssembly(name);
    }

    //PreloadReplacers 预载替换方程序集
    //被注入方改写时要能解析替换方法 那一刻替换方必须在场
    //替换方自己也被注入时它的加载会递归走同一套改写 所以不必预先排加载顺序
    public void PreloadReplacers()
    {
        foreach (var rule in _engine.Rules)
        {
            if (!_assemblyPaths.TryGetValue(rule.ReplacementAssembly, out var path))
            {
                Errors.Add($"规则 {rule} 的替换方程序集 {rule.ReplacementAssembly} 不在模组目录里");
                continue;
            }

            try
            {
                ModAssemblies.Load(rule.ReplacementAssembly, path);
            }
            catch (Exception ex)
            {
                Errors.Add($"替换方程序集 {rule.ReplacementAssembly} 加载失败 {ex.Message}");
            }
        }
    }
}
