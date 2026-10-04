using System.Collections.Concurrent;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureTemplateManager 结构模板管理器 对应原版同名类
//按 id 从数据包的 data/<ns>/structure/<path>.nbt 读模板 读过的结果(含失败)都缓存
public sealed class StructureTemplateManager
{
    //Directory 模板目录 对应原版 FileToIdConverter("structure", ".nbt")
    public const string Directory = "structure";

    public const string Extension = ".nbt";

    private readonly ResourceManager _resources;
    //模板缓存 生成期多个区块并行推进 普通字典并发写入会让桶链成环 表现为某次插入挂住几十秒
    //用 Lazy 包一层: 同一个模板即使被多线程同时请求也只解析一次 失败结果同样缓存
    private readonly ConcurrentDictionary<Identifier, Lazy<StructureTemplate?>> _cache = new();

    public StructureTemplateManager(ResourceManager resources) => _resources = resources;

    //GetOrLoad 按 id 取模板 不存在返回 null 并缓存这个失败结果避免反复扫资源包
    public StructureTemplate? GetOrLoad(Identifier id)
        => _cache.GetOrAdd(id,
            static (key, self) => new Lazy<StructureTemplate?>(
                () => self.Load(key), LazyThreadSafetyMode.ExecutionAndPublication),
            this).Value;

    //Load 拼接模板路径并读 NBT
    private StructureTemplate? Load(Identifier id)
    {
        var location = Identifier.FromNamespaceAndPath(id.Namespace, $"{Directory}/{id.Path}{Extension}");
        var resource = _resources.GetResource(PackType.ServerData, location);
        if (resource is null) return null;
        try
        {
            using var stream = resource.Open();
            var tag = NbtIo.ReadCompressed(stream, NbtAccounter.UnlimitedHeap());
            var template = new StructureTemplate();
            template.Load(tag);
            return template;
        }
        catch (Exception e)
        {
            Log.Warning($"Structure template {id} failed to read: {e.Message}");
            return null;
        }
    }
}
