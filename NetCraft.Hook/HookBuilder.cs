namespace NetCraft.Hook;

public sealed class HookBuilder
{
    private readonly List<HookRule> _rules = new();
    private readonly List<MixinRule> _mixins = new();

    public HookTargetBuilder Hook(string typeFullName, string methodName)
    {
        return new HookTargetBuilder(this, typeFullName, methodName, HookType.CallSite, PatchMode.ILRewrite);
    }

    public HookTargetBuilder Hook(string typeFullName, string methodName, HookType hookType)
    {
        return new HookTargetBuilder(this, typeFullName, methodName, hookType, PatchMode.ILRewrite);
    }

    public HookTargetBuilder Hook(string typeFullName, string methodName, HookType hookType, PatchMode patchMode)
    {
        return new HookTargetBuilder(this, typeFullName, methodName, hookType, patchMode);
    }

    public HookTargetBuilder Hook<TTarget>(string methodName)
    {
        return new HookTargetBuilder(this, typeof(TTarget).FullName!, methodName, HookType.CallSite, PatchMode.ILRewrite);
    }

    public HookTargetBuilder Hook<TTarget>(string methodName, HookType hookType)
    {
        return new HookTargetBuilder(this, typeof(TTarget).FullName!, methodName, hookType, PatchMode.ILRewrite);
    }

    public HookTargetBuilder Hook<TTarget>(string methodName, HookType hookType, PatchMode patchMode)
    {
        return new HookTargetBuilder(this, typeof(TTarget).FullName!, methodName, hookType, patchMode);
    }

    public HookBuilder AddRule(HookRule rule)
    {
        _rules.Add(rule);
        return this;
    }

    public HookBuilder AddRules(IEnumerable<HookRule> rules)
    {
        _rules.AddRange(rules);
        return this;
    }

    //AddMixin 登记一条混入规则
    public HookBuilder AddMixin(MixinRule rule)
    {
        _mixins.Add(rule);
        return this;
    }

    public HookEngine Build()
    {
        var engine = new HookEngine();
        engine.AddRules(_rules);
        engine.AddMixins(_mixins);
        return engine;
    }

    internal void RegisterRule(HookRule rule)
    {
        _rules.Add(rule);
    }
}

public sealed class HookTargetBuilder
{
    private readonly HookBuilder _parent;
    private readonly string _typeFullName;
    private readonly string _methodName;
    private readonly HookType _hookType;
    private readonly PatchMode _patchMode;

    internal HookTargetBuilder(HookBuilder parent, string typeFullName, string methodName, HookType hookType, PatchMode patchMode)
    {
        _parent = parent;
        _typeFullName = typeFullName;
        _methodName = methodName;
        _hookType = hookType;
        _patchMode = patchMode;
    }

    public HookBuilder With(Type replacementType, string replacementMethod, string? description = null)
    {
        _parent.RegisterRule(new HookRule(_typeFullName, _methodName, replacementType, replacementMethod, _hookType, _patchMode, description));
        return _parent;
    }

    //WithProbe 登记一条探针规则 在目标方法入口与每个出口插桩 原实现不变
    //reporterType 需要有 Begin() 返回 long 与由 reporterMethod 指定的 (string,long) 上报方法
    public HookBuilder WithProbe(Type reporterType, string reporterMethod, string label, string? description = null)
    {
        _parent.RegisterRule(new HookRule(_typeFullName, _methodName, reporterType, reporterMethod, HookType.Probe, PatchMode.ILRewrite, description, label));
        return _parent;
    }

    //WithProbeByArgument 登记一条按参数分桶的探针规则
    //出口上报的标签是 label + 目标方法第 argumentIndex 个参数的字符串形式
    //适用于一个方法承担多个阶段 只有参数能区分是哪一个 例如 ProcessChunk 的 ChunkStatus
    public HookBuilder WithProbeByArgument(Type reporterType, string reporterMethod, string label, int argumentIndex, string? description = null)
    {
        _parent.RegisterRule(new HookRule(_typeFullName, _methodName, reporterType, reporterMethod, HookType.Probe, PatchMode.ILRewrite, description, label, argumentIndex));
        return _parent;
    }

    //WithMark 登记一条标记规则 只在目标方法入口上报一次 不计耗时
    //reporterType 需要有由 reporterMethod 指定的 (string) 上报方法
    public HookBuilder WithMark(Type reporterType, string reporterMethod, string label, string? description = null)
    {
        _parent.RegisterRule(new HookRule(_typeFullName, _methodName, reporterType, reporterMethod, HookType.Mark, PatchMode.ILRewrite, description, label));
        return _parent;
    }
}
