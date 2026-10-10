namespace NetCraft.Hook;

using System.Reflection;

public enum HookType
{
    CallSite,
    MethodBody,
    NewObj,
    FieldRead,
    FieldWrite,
    TypeCheck,
    Box,
    FunctionPointer,

    //Probe 保留原方法体 只在方法入口与每个出口插桩
    //与 MethodBody 的区别是它不替换原逻辑 因此能用在会被 JIT 内联的热点方法上
    //配合加载时内存改写 效果等同于编译期插桩
    Probe,

    //Mark 只在方法入口插一次上报 用来捕获"某件事发生了"的时刻 不计耗时
    Mark,

    //LocalRead/LocalWrite 局部变量读写处设桩 对应 mixin 的 @ModifyVariable
    //Constant 常量加载处设桩 对应 mixin 的 @ModifyConstant
    //这三类的锚点就在宿主方法自己体内 所以 OriginalType/OriginalMethod 记的是**宿主方法**而不是被引用实体
    //LocalRead/LocalWrite 还要配 LocalIndex 指定是哪个槽位 Constant 要配 ConstantValue 指定是哪个值
    LocalRead,
    LocalWrite,
    Constant,

    //CallArg 改一次调用的实参 对应 mixin 的 @ModifyArg
    //锚点与 CallSite 同一套：OriginalType/OriginalMethod 记被调用的方法 宿主由 InType/InMethod 限定
    //改哪一个实参由 ArgumentIndex 指定 回调拿宿主参数加该实参的原值 返回新值
    //原调用照旧执行 与 CallSite 那种"整条调用顶替掉"是两件事
    CallArg,
}

//HookPlacement 锚点命中后替换调用的落位方式
//Replace 顶替锚点 原调用不再执行 需要回调自己补回
//Before 保留锚点 在它之前插入一次调用
//After 保留锚点 在它之后插入一次调用
//插入只对指令级形态有意义 方法级的 Probe/Mark 不看这个字段
//插入时传给回调的是宿主方法的参数(含 this) 与 MethodBody 的约定一致
public enum HookPlacement
{
    Replace,
    Before,
    After,
}

public sealed class HookRule
{
    public string OriginalType { get; }
    public string OriginalMethod { get; }

    //ReplacementAssembly 替换方法所在程序集的简单名
    //ReplacementTypeName 替换类型全名 嵌套类型按反射的 + 形式
    //只记名字不记 Type 是为了让规则表能在替换方被加载之前建完
    //否则为了拿 Type 就得提前把替换方拉起来 被替换的目标程序集反而错过改写时机
    public string ReplacementAssembly { get; }
    public string ReplacementTypeName { get; }

    public string ReplacementMethod { get; }
    public HookType HookType { get; }
    public PatchMode PatchMode { get; }
    public string? Description { get; }

    //Label Probe 规则的标签 插桩时作为常量传给上报方法 用来区分不同观测点
    public string? Label { get; }

    //LabelArgumentIndex 标签后缀取自目标方法第几个参数(0 基)
    //不为 null 时出口上报的标签是 Label + 该参数的字符串形式
    //用来把同一个方法按参数值分桶 例如 ProcessChunk 按 ChunkStatus 分阶段计时
    public int? LabelArgumentIndex { get; }

    //LocalIndex 局部变量槽位 0 基 只有 LocalRead/LocalWrite 用
    public int? LocalIndex { get; }

    //ConstantValue 要匹配的常量值 只有 Constant 用
    //按装箱后的类型比较 int 5 与 long 5 不会被判为相等 想匹配 ldc.i8 就得传 long
    public object? ConstantValue { get; }

    //InType/InMethod 宿主限定 只在这个方法体内匹配锚点 两个都为空表示不限
    //不加限定时一条规则作用于全程序集里所有该锚点 加了才能收窄到某一处
    //LocalRead/LocalWrite/Constant 不需要它 那三类的宿主就是 OriginalType/OriginalMethod
    public string? InType { get; }

    public string? InMethod { get; }

    //Placement 锚点落位方式 见 HookPlacement
    public HookPlacement Placement { get; }

    //Ordinal 同一锚点在宿主方法里匹配到多处时只认第几处 0 基
    //不设表示每一处都改 设了就只有那一处落地
    //计数按匹配到的先后 与那一处最后成不成功无关 跟 Mixin 的 @At(ordinal) 一个意思
    public int? Ordinal { get; }

    //ArgumentIndex 改第几个实参 0 基 实例调用的 this 算第 0 个 只有 CallArg 用
    public int? ArgumentIndex { get; }

    //SliceFrom/SliceTo 方法内区间限定 写成"类型全名::方法名" 对应 mixin 的 @Slice
    //匹配范围收窄到宿主方法里第一次命中 SliceFrom 的那条调用到第一次命中 SliceTo 的那条调用之间
    //任一端为空表示那一侧不设界 两端都空就是原来的"整个方法体内匹配"
    //用途是长方法里同一个锚点出现多次时 用"从哪个调用到哪个调用之间"取代按序号数的 Ordinal
    public string? SliceFrom { get; }

    public string? SliceTo { get; }

    //SourceAssembly 规则由 Type 建时带着那个程序集
    //引擎登记规则时顺手填进替换方表 从名字建的规则则等调用方注册加载器
    internal Assembly? SourceAssembly { get; }

    //从 Type 建规则 手上已有类型对象时用这个
    public HookRule(string originalType, string originalMethod, Type replacementType, string replacementMethod, HookType hookType = HookType.CallSite, PatchMode patchMode = PatchMode.ILRewrite, string? description = null, string? label = null, int? labelArgumentIndex = null, int? localIndex = null, object? constantValue = null, string? inType = null, string? inMethod = null, HookPlacement placement = HookPlacement.Replace, int? ordinal = null, int? argumentIndex = null, string? sliceFrom = null, string? sliceTo = null)
    {
        var type = replacementType ?? throw new ArgumentNullException(nameof(replacementType));
        LabelArgumentIndex = labelArgumentIndex;
        OriginalType = originalType ?? throw new ArgumentNullException(nameof(originalType));
        OriginalMethod = originalMethod ?? throw new ArgumentNullException(nameof(originalMethod));
        ReplacementAssembly = type.Assembly.GetName().Name!;
        ReplacementTypeName = type.FullName!;
        ReplacementMethod = replacementMethod ?? throw new ArgumentNullException(nameof(replacementMethod));
        HookType = hookType;
        PatchMode = patchMode;
        Description = description;
        Label = label;
        LocalIndex = localIndex;
        ConstantValue = constantValue;
        InType = inType;
        InMethod = inMethod;
        Placement = placement;
        Ordinal = ordinal;
        ArgumentIndex = argumentIndex;
        SliceFrom = sliceFrom;
        SliceTo = sliceTo;
        SourceAssembly = type.Assembly;
    }

    //从程序集名与类型名建规则 替换方不在场时用这个
    public HookRule(string originalType, string originalMethod, string replacementAssembly, string replacementTypeName, string replacementMethod, HookType hookType = HookType.CallSite, PatchMode patchMode = PatchMode.ILRewrite, string? description = null, string? label = null, int? labelArgumentIndex = null, int? localIndex = null, object? constantValue = null, string? inType = null, string? inMethod = null, HookPlacement placement = HookPlacement.Replace, int? ordinal = null, int? argumentIndex = null, string? sliceFrom = null, string? sliceTo = null)
        : this(originalType, originalMethod, replacementAssembly, replacementTypeName, replacementMethod, hookType, patchMode, description, label, labelArgumentIndex, localIndex, constantValue, inType, inMethod, placement, ordinal, argumentIndex, sliceFrom, sliceTo, null)
    {
    }

    private HookRule(string originalType, string originalMethod, string replacementAssembly, string replacementTypeName, string replacementMethod, HookType hookType, PatchMode patchMode, string? description, string? label, int? labelArgumentIndex, int? localIndex, object? constantValue, string? inType, string? inMethod, HookPlacement placement, int? ordinal, int? argumentIndex, string? sliceFrom, string? sliceTo, Assembly? source)
    {
        LabelArgumentIndex = labelArgumentIndex;
        OriginalType = originalType ?? throw new ArgumentNullException(nameof(originalType));
        OriginalMethod = originalMethod ?? throw new ArgumentNullException(nameof(originalMethod));
        ReplacementAssembly = replacementAssembly ?? throw new ArgumentNullException(nameof(replacementAssembly));
        ReplacementTypeName = replacementTypeName ?? throw new ArgumentNullException(nameof(replacementTypeName));
        ReplacementMethod = replacementMethod ?? throw new ArgumentNullException(nameof(replacementMethod));
        HookType = hookType;
        PatchMode = patchMode;
        Description = description;
        Label = label;
        LocalIndex = localIndex;
        ConstantValue = constantValue;
        InType = inType;
        InMethod = inMethod;
        Placement = placement;
        Ordinal = ordinal;
        ArgumentIndex = argumentIndex;
        SliceFrom = sliceFrom;
        SliceTo = sliceTo;
        SourceAssembly = source;
    }

    public override string ToString()
    {
        var prefix = HookType switch
        {
            HookType.CallSite => "[CallSite] ",
            HookType.MethodBody => "[MethodBody] ",
            HookType.NewObj => "[NewObj] ",
            HookType.FieldRead => "[FieldRead] ",
            HookType.FieldWrite => "[FieldWrite] ",
            HookType.TypeCheck => "[TypeCheck] ",
            HookType.Box => "[Box] ",
            HookType.FunctionPointer => "[FuncPtr] ",
            HookType.Probe => "[Probe] ",
            HookType.Mark => "[Mark] ",
            HookType.LocalRead => "[LocalRead] ",
            HookType.LocalWrite => "[LocalWrite] ",
            HookType.Constant => "[Constant] ",
            HookType.CallArg => "[CallArg] ",
            _ => ""
        };
        var mode = PatchMode switch
        {
            PatchMode.RuntimePatch => " (runtime)",
            PatchMode.RuntimeInject => " (rejit)",
            _ => ""
        };
        var placement = Placement switch
        {
            HookPlacement.Before => " (before)",
            HookPlacement.After => " (after)",
            _ => ""
        };
        var host = InType is null && InMethod is null ? "" : $" in {InType ?? "*"}::{InMethod ?? "*"}";
        var anchor = HookType switch
        {
            HookType.LocalRead or HookType.LocalWrite => $" local[{LocalIndex}]",
            HookType.Constant => $" const={ConstantValue ?? "null"}",
            HookType.CallArg => $" arg[{ArgumentIndex}]",
            _ => ""
        };
        var slice = SliceFrom is null && SliceTo is null ? "" : $" slice={SliceFrom ?? "*"}..{SliceTo ?? "*"}";
        var label = Label is null ? "" : $" label={Label}";
        return $"{prefix}{OriginalType}::{OriginalMethod} → {ReplacementAssembly}!{ReplacementTypeName}::{ReplacementMethod}{mode}{placement}{anchor}{host}{slice}{label}";
    }
}
