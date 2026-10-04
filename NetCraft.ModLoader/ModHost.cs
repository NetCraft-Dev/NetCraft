namespace NetCraft.ModLoader;

//ModHost 加载器对宿主组件的只读访问点
//对应原版 FabricLoader.getInstance() 界面这类不参与加载流程的代码从这里取模组数据
//引导是单次性的 绑一次之后不再变 所以这里不加锁
public static class ModHost
{
    //RuntimeModId 彩蛋条目的标识 它不在 mods 目录里 靠这个 id 认出来
    private const string RuntimeModId = "dotnet.runtime";
    //RuntimeIconResource 彩蛋图标的内嵌资源名
    private const string RuntimeIconResource = "logo_net.png";

    //RuntimeMod 借模组页露脸的 .NET 运行时
    //它不是模组也不参与加载 所以状态直接标成已加载 注入规则留空
    private static readonly ModInfo RuntimeMod = new()
    {
        Name = RuntimeModId,
        DisplayName = ".NET Runtime",
        Version = Environment.Version.ToString(3),
        Description = ".NET is a cross-platform runtime for cloud, mobile, desktop, and IoT apps.",
        License = "MIT",
        Contact = new ModContact { Homepage = "https://github.com/dotnet/runtime" },
        Environment = ModEnvironment.Both,
        Status = ModStatus.Running,
        //初始化那一步对运行时没有意义 用负值让界面整行都不显示
        LoadMilliseconds = -1,
    };

    private static readonly Lazy<byte[]?> RuntimeIconLazy = new(ReadRuntimeIcon);

    //Manager 已绑定的模组管理器 引导之前为 null
    public static ModManager? Manager { get; private set; }

    //ModsFolder 模组目录 未绑定时为空串
    public static string ModsFolder { get; private set; } = string.Empty;

    //Bind 引导结束时登记
    public static void Bind(ModManager manager, string modsFolder)
    {
        Manager = manager;
        ModsFolder = modsFolder;
    }

    //LoadedMods 已加载完成的模组
    public static IReadOnlyList<ModInfo> LoadedMods => Merge(Manager?.GetLoadedMods());

    //AllMods 扫描到的全部模组 含失败与跳过的
    public static IReadOnlyList<ModInfo> AllMods => Merge(Manager?.GetAllMods());

    //Find 按模组标识查一个 查不到返回 null
    public static ModInfo? Find(string name)
        => name == RuntimeModId ? RuntimeMod : Manager?.GetModInfo(name);

    //ReadIcon 取模组图标字节 没有图标与未绑定时都返回 null
    public static byte[]? ReadIcon(string name)
        => name == RuntimeModId ? RuntimeIconLazy.Value : Manager?.ReadIcon(name);

    //Merge 真实模组后面缀上彩蛋条目
    //它不占 mods 目录也不参与加载 排在末尾免得看着像模组列表的一员
    private static IReadOnlyList<ModInfo> Merge(IReadOnlyList<ModInfo>? mods)
    {
        var merged = new List<ModInfo>((mods?.Count ?? 0) + 1);
        if (mods is not null)
            merged.AddRange(mods);
        merged.Add(RuntimeMod);
        return merged;
    }

    //ReadRuntimeIcon 彩蛋图标从加载器自己的内嵌资源读
    private static byte[]? ReadRuntimeIcon()
    {
        try
        {
            using var stream = typeof(ModHost).Assembly.GetManifestResourceStream(RuntimeIconResource);
            if (stream is null)
                return null;

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
