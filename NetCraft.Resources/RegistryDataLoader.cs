using NetCraft.Registry;

namespace NetCraft.Resources;

//RegistryDataLoader 注册表数据驱动加载器对应原版 RegistryDataLoader
//按 data/<namespace>/<注册表路径>/<元素路径>.json 扫描资源包 用元素 codec 解码后写入目标注册表
//单个元素出错只记进结果不中断整体加载 对应原版把每个元素异常汇总成 CrashReport
public static class RegistryDataLoader
{
    //Load 加载一组注册表数据 必须在目标注册表 Freeze 之前调用
    //context 提供解析元素内跨注册表引用的查表入口
    public static LoadResult Load(ResourceManager resourceManager, RegistryAccess context,
        IReadOnlyList<RegistryData> data)
    {
        //先把各注册表的元素扫成一份总待办 元素之间会互相引用 跨注册表也会(configured_feature 引用 placed_feature)
        //原版把各注册表并行装载 交错之间引用能凑齐 这里改成整份待办反复重试来复现同样的效果
        var pending = new List<PendingElement>();
        foreach (var entry in data) CollectPending(resourceManager, entry, pending);

        var loaded = 0;
        while (pending.Count > 0)
        {
            var progressed = 0;
            var remaining = new List<PendingElement>(pending.Count);
            foreach (var item in pending)
            {
                if (item.Data.TryLoadElement(item.Resource, item.Id, context, out var error))
                {
                    loaded++;
                    progressed++;
                }
                else
                {
                    //先留住本轮失败原因 若之后某轮成功这项就丢弃 最终仍未成功才记进错误
                    remaining.Add(item with { Error = error });
                }
            }
            //一整轮下来没有任何元素成功 说明剩下的都是真错误 再重试也不会变
            if (progressed == 0) return new LoadResult(loaded, remaining.Select(item => item.Error).ToList());
            pending = remaining;
        }
        return new LoadResult(loaded, Array.Empty<string>());
    }

    //CollectPending 扫描某个注册表目录下的全部元素文件 按目录顺序排进待办
    private static void CollectPending(ResourceManager resourceManager, RegistryData data,
        List<PendingElement> pending)
    {
        var folder = data.RegistryId.Path;
        var prefix = folder + "/";

        //遍历所有命名空间 与标签加载走同一套资源包遍历方式
        foreach (var ns in resourceManager.GetNamespaces(PackType.ServerData))
        {
            foreach (var resource in resourceManager.ListResources(PackType.ServerData, ns, folder))
            {
                var path = resource.Location.Path;
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var elementPath = path[prefix.Length..];
                if (elementPath.EndsWith(".json", StringComparison.Ordinal))
                    elementPath = elementPath[..^5];
                if (elementPath.Length == 0) continue;

                var elementId = Identifier.FromNamespaceAndPath(resource.Location.Namespace, elementPath);
                pending.Add(new PendingElement(data, resource, elementId, string.Empty));
            }
        }
    }
}

//PendingElement 一个待装载元素 携带本轮失败原因供重试与最终汇总
internal readonly record struct PendingElement(RegistryData Data, Resource Resource, Identifier Id, string Error);

//LoadResult 加载结果 携带成功条数与逐元素错误
public sealed class LoadResult
{
    public LoadResult(int loadedCount, IReadOnlyList<string> errors)
    {
        LoadedCount = loadedCount;
        Errors = errors;
    }

    //LoadedCount 成功写入注册表的元素数
    public int LoadedCount { get; }

    //Errors 逐元素失败原因 形如 minecraft:foo: 具体消息
    public IReadOnlyList<string> Errors { get; }

    public bool HasErrors => Errors.Count > 0;
}
