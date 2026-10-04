namespace NetCraft.ModLoader;

//ModStatus 模组在加载流程中的状态
public enum ModStatus
{
    //NotFound 未找到
    NotFound,
    //Scanned 扫描通过待初始化
    Scanned,
    //Initializing 初始化中
    Initializing,
    //Running 加载完成
    Running,
    //Error 扫描依赖或初始化失败
    Error,
    //Skipped 被加载控制判定跳过
    Skipped,
    //Interrupted 被加载控制拦截且未接管
    Interrupted,
}

//ModLoadAction 加载控制回调的返回值 决定当前模组的去向
public enum ModLoadAction
{
    //Continue 按默认流程加载
    Continue,
    //Interrupt 交给宿主接管加载
    Interrupt,
    //Skip 跳过该模组
    Skip,
}

//ModInfo 对外暴露的模组信息
//既给加载流程用 也给界面用 展示相关的字段全部取自内嵌清单
public sealed class ModInfo
{
    //Name 模组标识取清单 id
    public string Name { get; init; } = string.Empty;
    //Id 与 Name 同值 读起来更直白
    public string Id => Name;
    //DisplayName 展示名 清单没写 name 时退回 id
    public string DisplayName { get; init; } = string.Empty;
    //Version 版本
    public string Version { get; init; } = string.Empty;
    //Description 描述
    public string Description { get; init; } = string.Empty;
    //Authors 作者
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    //Contributors 贡献者
    public IReadOnlyList<string> Contributors { get; init; } = Array.Empty<string>();
    //License 许可证
    public string License { get; init; } = string.Empty;
    //Contact 联系方式
    public ModContact Contact { get; init; } = new();
    //Icon 图标的内嵌资源名 留空表示按约定找
    public string Icon { get; init; } = string.Empty;
    //Environment 声明的运行端
    public ModEnvironment Environment { get; init; }
    //Hooks 注入规则清单
    public IReadOnlyList<ModHookRule> Hooks { get; init; } = Array.Empty<ModHookRule>();
    //LoadMilliseconds 初始化耗时 毫秒 不足一毫秒也有值
    //未走到初始化时为 null 取负值表示这一栏不适用 彩蛋条目用
    public double? LoadMilliseconds { get; init; }
    //EntryType 入口类类型
    public Type? EntryType { get; init; }
    //Instance 入口类实例
    public object? Instance { get; init; }
    //AssemblyPath 模组 dll 路径
    public string AssemblyPath { get; init; } = string.Empty;
    //Status 当前状态
    public ModStatus Status { get; set; }
    //Dependencies 依赖的模组名
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    public override string ToString() => $"{Name} [{Status}]";
}

//ModLoadContext 加载控制回调的上下文 宿主据此决定放行跳过还是接管
public sealed class ModLoadContext
{
    //Name 模组名
    public string Name { get; init; } = string.Empty;
    //EntryType 入口类类型
    public Type? EntryType { get; init; }
    //Instance 入口类实例
    public object? Instance { get; init; }
    //AssemblyPath 模组 dll 路径
    public string AssemblyPath { get; init; } = string.Empty;
    //Dependencies 依赖的模组名
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    //Completed 已处理数
    public int Completed { get; init; }
    //Total 待处理总数
    public int Total { get; init; }
    //CustomData 宿主自用的附加数据
    public Dictionary<string, object> CustomData { get; init; } = new();
}

//InterruptResult 拦截回调的结果
public sealed class InterruptResult
{
    //ContinueOriginalLogic 接管后仍按默认流程初始化
    public bool ContinueOriginalLogic { get; init; }
    //MarkAsLoaded 直接标记为已加载不执行初始化
    public bool MarkAsLoaded { get; init; }
    //Error 非空表示加载失败
    public string Error { get; init; } = string.Empty;
    //CustomData 宿主自用的附加数据
    public Dictionary<string, object> CustomData { get; init; } = new();
}

//LoadResult 一次加载流程的汇总
public sealed class LoadResult
{
    public bool Success { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Skipped { get; } = new();
    public List<string> Interrupted { get; } = new();
    public List<string> Loaded { get; } = new();
}

//ProgressInfo 加载进度快照
public sealed class ProgressInfo
{
    public string Status { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Total { get; set; }
    public string CurrentMod { get; set; } = string.Empty;
    public int Percentage { get; set; }
}
