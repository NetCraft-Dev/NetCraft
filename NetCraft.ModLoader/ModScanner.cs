using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace NetCraft.ModLoader;

//ScannedMod 静态扫描出的模组条目
public sealed class ScannedMod
{
    //AssemblyPath 模组 dll 路径
    public string AssemblyPath { get; init; } = string.Empty;
    //AssemblyName 程序集名 用来把 AssemblyRef 映射回模组
    public string AssemblyName { get; init; } = string.Empty;
    //Manifest 内嵌 ncmod.json 的解析结果
    public ModManifest Manifest { get; init; } = new();
    //EmbeddedResources 该程序集内嵌的全部资源名
    //模组的第三方依赖由构建直接嵌成资源 依赖解析就靠这份清单
    //列出来是为了解析时不用再开一次文件
    public IReadOnlyList<string> EmbeddedResources { get; init; } = Array.Empty<string>();
    //ReferencedAssemblies 该程序集引用的程序集名
    public IReadOnlyList<string> ReferencedAssemblies { get; init; } = Array.Empty<string>();
    //AnnotatedHooks 从 Inject 注解静态扫出的注入规则
    //与清单的 hooks 同形 装配时两路合并 同一个注入点两边都声明以注解为准
    public IReadOnlyList<ModHookRule> AnnotatedHooks { get; init; } = Array.Empty<ModHookRule>();
    //AnnotatedMixins 从 Mixin 注解静态扫出的混入规则 与清单的 mixins 同形
    public IReadOnlyList<ModMixinRule> AnnotatedMixins { get; init; } = Array.Empty<ModMixinRule>();
}

//ModScanner 模组静态扫描器
//用 MetadataReader 读 dll 的元数据表与内嵌资源 全程不加载程序集
//加载程序集本身不会触发引用解析 但解析模组里的类型会连带拉起内核
//先静态扫出运行端再决定加载哪些 是单 ALC 下的必要前提 加载了就没法卸载
public static class ModScanner
{
    //ManifestResourceName 模组声明固定用的内嵌资源名
    public const string ManifestResourceName = "ncmod.json";

    //InjectHostAssembly Inject 注解所在程序集
    //没引用它的模组不可能有注解 直接跳过遍历 省下每个方法扫一遍特性表的开销
    private const string InjectHostAssembly = "NetCraft.ModApi";

    //ScanAll 扫描目录下全部 dll 没有声明的直接跳过
    public static List<ScannedMod> ScanAll(string folderPath)
    {
        var result = new List<ScannedMod>();
        if (!Directory.Exists(folderPath))
            return result;

        foreach (var path in Directory.GetFiles(folderPath, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var scanned = Scan(path);
            if (scanned is not null)
                result.Add(scanned);
        }
        return result;
    }

    //Scan 扫一个 dll 没有内嵌声明或不是托管程序集时返回 null
    public static ScannedMod? Scan(string dllPath)
    {
        try
        {
            using var stream = File.OpenRead(dllPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
                return null;

            var reader = pe.GetMetadataReader();
            var manifestBytes = ReadResource(pe, reader, ManifestResourceName);
            if (manifestBytes is null)
                return null;

            var manifest = JsonSerializer.Deserialize<ModManifest>(manifestBytes);
            if (manifest is null || manifest.Id.Length == 0)
                return null;

            var assemblyName = reader.GetString(reader.GetAssemblyDefinition().Name);
            var referenced = reader.AssemblyReferences
                .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
                .ToList();

            //注解规则与清单规则在同一趟里扫出来 装配时不分先后合并成一张表
            var annotated = new List<ModHookRule>();
            var annotatedMixins = new List<ModMixinRule>();
            if (referenced.Contains(InjectHostAssembly, StringComparer.Ordinal))
            {
                annotated = InjectScanner.Scan(reader);
                annotatedMixins = InjectScanner.ScanMixins(reader);
            }

            return new ScannedMod
            {
                AssemblyPath = dllPath,
                AssemblyName = assemblyName,
                Manifest = manifest,
                EmbeddedResources = ListResourceNames(reader),
                ReferencedAssemblies = referenced,
                AnnotatedHooks = annotated,
                AnnotatedMixins = annotatedMixins,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    //ReadEmbeddedResource 读任意内嵌资源字节 供依赖库解析与诊断使用
    //不吞异常 传入的不是托管程序集或资源不存在时由调用方自行处理
    public static byte[]? ReadEmbeddedResource(string dllPath, string resourceName)
    {
        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        return pe.HasMetadata ? ReadResource(pe, pe.GetMetadataReader(), resourceName) : null;
    }

    //ListResourceNames 列出内嵌资源名 指向外部文件的资源项不算
    private static List<string> ListResourceNames(MetadataReader reader)
    {
        var names = new List<string>();
        foreach (var handle in reader.ManifestResources)
        {
            var resource = reader.GetManifestResource(handle);
            if (resource.Implementation.IsNil)
                names.Add(reader.GetString(resource.Name));
        }
        return names;
    }

    //ReadResource 读内嵌资源字节 资源不存在或指向外部文件时返回 null
    //ManifestResource.Offset 是相对 CLI 资源目录 RVA 的偏移而不是文件偏移
    //数据在目录内以 4 字节长度开头 所以要从 GetSectionData(资源目录RVA) 的块里按该偏移取
    private static byte[]? ReadResource(PEReader pe, MetadataReader reader, string resourceName)
    {
        var resourcesRva = pe.PEHeaders.CorHeader?.ResourcesDirectory.RelativeVirtualAddress ?? 0;
        if (resourcesRva == 0)
            return null;

        var block = pe.GetSectionData(resourcesRva);
        foreach (var handle in reader.ManifestResources)
        {
            var resource = reader.GetManifestResource(handle);
            if (!resource.Implementation.IsNil)
                continue;
            if (reader.GetString(resource.Name) != resourceName)
                continue;

            var offset = (int)resource.Offset;
            var length = block.GetReader(offset, sizeof(int)).ReadInt32();
            return block.GetContent(offset + sizeof(int), length).ToArray();
        }
        return null;
    }
}
