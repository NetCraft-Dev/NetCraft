using System.Reflection;
using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Tags;

namespace NetCraft.Bootstrap;

//Bootstrap 引导静态类对应原版 net.minecraft.server.Bootstrap
//内核启动前触发所有内置注册表的 bootstrap 回调
//验证默认值存在性完成早期内容填充
public static class Bootstrap
{
    private static bool _bootstrapped;

    static Bootstrap() => Log.SetClassSource(typeof(Bootstrap));

    //BootStrap 主入口触发所有内置注册表 bootstrap 回调并验证
    public static void BootStrap()
    {
        if (_bootstrapped)
        {
            //Log.Debug("BootStrap 出口");
            return;
        }
        //Log.Debug("BootStrap 入口");
        BuiltInRegistries.BootStrap();
        ValidateRegistries();
        _bootstrapped = true;
        Log.Info("Bootstrap complete, registries validated");
        //Log.Debug("BootStrap 出口");
    }

    public static bool IsBootstrapped => _bootstrapped;

    public static void Reset()
    {
        _bootstrapped = false;
    }

    //ValidateRegistries 反射遍历 BuiltInRegistries 所有 Registry 字段
    //对每个调用 Freeze 确保不可变更统计注册总数 Log.Warning 报告空注册表
    public static void ValidateRegistries()
    {
        //Log.Debug($"ValidateRegistries 入口");
        int total = 0;
        int emptyCount = 0;
        int regCount = 0;
        foreach (var (_, registry) in BuiltInRegistries.EnumerateRegistries())
        {
            //用反射调用 Freeze 与 Size 避免泛型协变限制
            registry.GetType().GetMethod("Freeze")?.Invoke(registry, null);
            var sizeProp = registry.GetType().GetProperty("Size");
            int count = (int)(sizeProp?.GetValue(registry) ?? 0);
            regCount++;
            total += count;
            if (count == 0) emptyCount++;
        }
        Log.Info($"Registry validation complete: {regCount} registries, {total} entries, {emptyCount} empty");
        //Log.Debug($"ValidateRegistries 出口");
    }

    //LoadBuiltinTags 从资源管理器加载内置标签集合
    //为每个核心注册表创建 TagLoader 扫描 tags/{category} 目录
    //BuildAll 后注册到 TagManager 待 BindAll 调用绑定
    //category 取注册表键的路径段 原版数据包就是按这个分目录的
    //(block/item/entity_type/fluid/game_event 是单数 群系挂在 worldgen/biome 下)
    public static void LoadBuiltinTags(TagManager tagManager, ResourceManager resourceManager)
    {
        Log.Debug($"LoadBuiltinTags entry tagManager={tagManager} resourceManager={resourceManager}");
        int totalTags = 0;
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.BLOCK, "block");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.ITEM, "item");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.ENTITY_TYPE, "entity_type");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.FLUID, "fluid");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.GAME_EVENT, "game_event");
        totalTags += LoadTagsForRegistry(tagManager, resourceManager, BuiltInRegistries.BIOME, "worldgen/biome");
        Log.Info($"Scanned built-in tags: {totalTags} tag files registered to TagManager");
        //Log.Debug("LoadBuiltinTags 出口");
    }

    //LoadTagsForRegistry 为单个注册表加载 tag 文件并注册到 TagManager
    //泛型 T 对齐注册表元素类型 elementGetter 委托到 registry.Get(id)
    private static int LoadTagsForRegistry<T>(
        TagManager tagManager,
        ResourceManager resourceManager,
        Registry<T> registry,
        string category) where T : class
    {
        var loader = new TagLoader<T>($"tags/{category}", id =>
        {
            var holder = registry.Get(id);
            return holder is not null ? Optional<T>.Of(holder.Value) : Optional<T>.Empty();
        });

        var files = LoadTagFiles(resourceManager, category);
        if (files.Count == 0) return 0;

        var builtTags = loader.BuildAll(files);
        tagManager.RegisterLoader(registry.Key, loader, builtTags);
        return files.Count;
    }

    //LoadTagFiles 扫描所有 namespace 下 tags/{category} 目录的 TagFile
    //返回 tag id -> TagFile 列表 同一 tag 多个数据包合并
    private static Dictionary<Identifier, List<TagFile>> LoadTagFiles(ResourceManager resourceManager, string category)
    {
        var result = new Dictionary<Identifier, List<TagFile>>();
        var prefix = $"tags/{category}/";
        foreach (var ns in resourceManager.GetNamespaces(PackType.ServerData))
        {
            foreach (var resource in resourceManager.ListResources(PackType.ServerData, ns, $"tags/{category}"))
            {
                var path = resource.Location.Path;
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var tagPath = path[prefix.Length..];
                if (tagPath.EndsWith(".json", StringComparison.Ordinal))
                    tagPath = tagPath[..^5];
                if (tagPath.Length == 0) continue;
                var tagId = Identifier.FromNamespaceAndPath(resource.Location.Namespace, tagPath);

                using var stream = resource.Open();
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                TagFile tagFile;
                try
                {
                    tagFile = TagFile.FromJson(json);
                }
                catch (Exception ex)
                {
                    Log.Warning($"Skipping unparsable tag file {tagId}: {ex.Message}");
                    continue;
                }

                if (!result.TryGetValue(tagId, out var list))
                {
                    list = new List<TagFile>();
                    result[tagId] = list;
                }
                list.Add(tagFile);
            }
        }
        return result;
    }
}
