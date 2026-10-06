using System.Text.Json.Serialization;

namespace NetCraft.ModLoader;

//ModEnvironment 模组适用的运行端
public enum ModEnvironment
{
    //Both 客户端与服务端都加载
    Both,
    //Client 仅客户端加载
    Client,
    //Server 仅服务端加载
    Server,
}

//ModEnvironmentExtensions 运行端判定的公共逻辑
internal static class ModEnvironmentExtensions
{
    //Matches 声明的运行端是否包含当前端
    public static bool Matches(this ModEnvironment declared, ModEnvironment current)
        => declared == ModEnvironment.Both || declared == current;

    //ParseEnvironment 解析运行端字段 无法识别时按 Both 兜底
    public static ModEnvironment ParseEnvironment(string value)
        => Enum.TryParse<ModEnvironment>(value, ignoreCase: true, out var parsed) ? parsed : ModEnvironment.Both;
}

//ModManifest 模组声明 对应 dll 内嵌资源 ncmod.json
//放内嵌资源而不是特性 是为了让扫描阶段能用 MetadataReader 静态读出 不必加载程序集
public sealed class ModManifest
{
    //Id 模组标识 依赖关系与查询都用它
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    //Version 模组版本
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    //DisplayName 展示名 给人看的名字 没写时用 Id 顶
    [JsonPropertyName("name")]
    public string DisplayName { get; set; } = string.Empty;

    //Description 一句话描述
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    //Authors 作者
    [JsonPropertyName("authors")]
    public List<string> Authors { get; set; } = new();

    //Contributors 贡献者
    [JsonPropertyName("contributors")]
    public List<string> Contributors { get; set; } = new();

    //License 许可证标识
    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    //Contact 联系方式 对应原版 fabric.mod.json 的 contact 段
    [JsonPropertyName("contact")]
    public ModContact Contact { get; set; } = new();

    //Icon 图标的内嵌资源名 留空时按约定的 icon.png 找
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    //EnvironmentValue 运行端字段取 both/client/server
    [JsonPropertyName("environment")]
    public string EnvironmentValue { get; set; } = "both";

    //Entry 入口类全名 该类需要有 public Task Init()
    [JsonPropertyName("entry")]
    public string Entry { get; set; } = string.Empty;

    //Depends 依赖的模组与要求的版本 键是模组 id 值是版本约束
    //没列出来的依赖不限定版本 只要那个模组在就照常加载
    //写进来的依赖版本不符时本模组被跳过 语法见 VersionConstraint
    [JsonPropertyName("depends")]
    public Dictionary<string, string> Depends { get; set; } = new();

    //Hooks 注入规则清单
    [JsonPropertyName("hooks")]
    public List<ModHookRule> Hooks { get; set; } = new();

    //Mixins 混入规则 把本模组某个类的字段与方法搬进目标类型
    //与 hooks 各走各的路 只对加载期改写生效
    [JsonPropertyName("mixins")]
    public List<ModMixinRule> Mixins { get; set; } = new();

    //EffectiveName 展示名 没写 name 时退回 Id
    [JsonIgnore]
    public string EffectiveName => DisplayName.Length > 0 ? DisplayName : Id;

    //Environment 解析后的运行端 无法识别时按 Both 兜底
    [JsonIgnore]
    public ModEnvironment Environment
        => ModEnvironmentExtensions.ParseEnvironment(EnvironmentValue);
}

//ModMixinRule 一条混入规则 把来源类型的成员搬进目标类型
//来源类型就在本模组程序集里 不必写程序集名
public sealed class ModMixinRule
{
    //Target 目标类型全名 成员搬到这里
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    //Source 来源类型全名 它的字段与方法会被搬走 搬完只剩空壳
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    //Interfaces 顺带让目标类型实现的接口全名
    //不写程序集名 装配时按类型索引查 查不到就留空由引擎在当前模块里找
    [JsonPropertyName("interfaces")]
    public List<string> Interfaces { get; set; } = new();
}

//ModContact 模组对外链接
public sealed class ModContact
{
    //Homepage 主页
    [JsonPropertyName("homepage")]
    public string Homepage { get; set; } = string.Empty;

    //Sources 源码仓库
    [JsonPropertyName("sources")]
    public string Sources { get; set; } = string.Empty;

    //Issues 问题反馈
    [JsonPropertyName("issues")]
    public string Issues { get; set; } = string.Empty;
}

//ModHookRule 一条注入规则 字段一一对应 Lead.Hook 的 HookRule
public sealed class ModHookRule
{
    //Target 目标类型全名 通常是内核里的类型
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    //Method 目标方法名
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    //HookTypeName 注入类型名 对应 Lead.Hook 的 HookType 取 CallSite/MethodBody/NewObj 等
    [JsonPropertyName("type")]
    public string HookTypeName { get; set; } = "CallSite";

    //PatchModeName 补丁模式名 对应 Lead.Hook 的 PatchMode 取 ILRewrite 或 RuntimeInject
    //ILRewrite 在目标程序集加载前改字节 RuntimeInject 借 ReJIT 改已经加载的代码
    //RuntimePatch 是退场中的入口 patch 新规则不要用
    [JsonPropertyName("patchMode")]
    public string PatchModeName { get; set; } = "ILRewrite";

    //ReplaceType 替换方法所在的类全名
    //该类不要引用内核类型 否则解析它时会把内核拉起来 注入就赶不上内核加载了
    [JsonPropertyName("replaceType")]
    public string ReplaceType { get; set; } = string.Empty;

    //ReplaceMethod 替换方法名
    [JsonPropertyName("replaceMethod")]
    public string ReplaceMethod { get; set; } = string.Empty;

    //Label 探针标签 仅 Probe 与 Mark 用
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    //Ordinal 同一锚点在该宿主方法里匹配到多处时只认第几处 0 基
    //不写表示每一处都改 宿主里没有这么多处时这条规则不落地
    [JsonPropertyName("ordinal")]
    public int? Ordinal { get; set; }

    //ArgumentIndex 改第几个实参 0 基 实例调用的 this 算第 0 个 只有 type=CallArg 用
    [JsonPropertyName("argumentIndex")]
    public int? ArgumentIndex { get; set; }

    //SliceFrom/SliceTo 方法内区间限定 写成"类型全名::方法名"
    //把匹配收窄到宿主方法里第一次调用 SliceFrom 到第一次调用 SliceTo 之间 任一端可省
    [JsonPropertyName("sliceFrom")]
    public string? SliceFrom { get; set; }

    [JsonPropertyName("sliceTo")]
    public string? SliceTo { get; set; }

    //EnvironmentValue 该规则适用的运行端 取 both/client/server 默认 both
    //同一份清单可以两端共用 但指向服务端类型的规则在客户端跑时目标程序集根本不在
    //不按端过滤的话这类规则会以"目标不在任何内核程序集里"报错 属于预期情况而非故障
    [JsonPropertyName("environment")]
    public string EnvironmentValue { get; set; } = "both";

    //Environment 解析后的运行端 无法识别时按 Both 兜底
    [JsonIgnore]
    public ModEnvironment Environment
        => ModEnvironmentExtensions.ParseEnvironment(EnvironmentValue);
}
