using System.Text.Json.Nodes;
using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;

namespace NetCraft.Game.World.Level.LevelGen.Dimension;

//WorldPresets 世界预设装载 对应原版 net.minecraft.world.level.levelgen.presets.WorldPresets
//26.2 起 level_stem 不再有独立目录 而是内嵌在 data/<ns>/worldgen/world_preset/<预设>.json 的 dimensions 段
//这里读该段把每个维度定义注册进 LEVEL_STEM 注册表 键就是维度标识
public static class WorldPresets
{
    //Normal 默认世界预设 对应原版 minecraft:normal
    public static readonly Identifier Normal = Identifier.WithDefaultNamespace("normal");

    //Load 装载指定预设的全部维度定义
    //返回注册成功的维度键 资源包里没有这个预设文件时返回空表且不报错
    public static IReadOnlyList<Identifier> Load(ResourceManager resourceManager, RegistryAccess context,
        Identifier presetId)
    {
        var location = Identifier.FromNamespaceAndPath(presetId.Namespace,
            $"worldgen/world_preset/{presetId.Path}.json");
        if (resourceManager.GetResource(PackType.ServerData, location) is not { } resource)
            return Array.Empty<Identifier>();

        JsonNode? node;
        using (var stream = resource.Open())
        {
            node = JsonOps.Parse(stream).GetOrThrow();
        }

        var ops = new RegistryOps<JsonNode?>(JsonOps.Instance, context);
        var stems = Parse(ops, node, presetId);
        var registry = (WritableRegistry<NetCraft.Registry.LevelStem>)BuiltInRegistries.LEVEL_STEM;
        var loaded = new List<Identifier>(stems.Count);
        foreach (var (dimensionId, stem) in stems)
        {
            //同名维度已存在时不覆盖 让先装载的那份生效
            if (!registry.ContainsKey(dimensionId))
                registry.Register(ResourceKey<NetCraft.Registry.LevelStem>.Create(registry.Key, dimensionId), stem,
                    RegistrationInfo.BuiltIn);
            loaded.Add(dimensionId);
        }
        return loaded;
    }

    //Parse 解析预设 JSON 的 dimensions 段 逐个维度解出关卡定义
    //单个维度解析失败只降级该维度 让其余维度照常建出来 免得一个数据问题挡住整个服务端
    public static IReadOnlyList<(Identifier Id, LevelStem Stem)> Parse(RegistryOps<JsonNode?> ops, JsonNode? root,
        Identifier presetId)
    {
        if (root is not JsonObject jsonRoot || jsonRoot["dimensions"] is not JsonObject dimensions)
            throw new InvalidDataException($"世界预设 {presetId} 缺少 dimensions 段");

        var stems = new List<(Identifier, LevelStem)>(dimensions.Count);
        foreach (var (key, value) in dimensions)
        {
            var parsedId = Identifier.TryParse(key);
            if (parsedId is null || value is null)
            {
                Log.Warning($"World preset {presetId} dimension {key} has an invalid definition, skipped");
                continue;
            }
            var parsed = LevelStem.Codec.Parse(ops, value);
            if (!parsed.Result().IsPresent)
            {
                Log.Warning($"World preset {presetId} dimension {key} failed to parse, skipped");
                continue;
            }
            stems.Add((parsedId.Value, parsed.GetOrThrow()));
        }
        return stems;
    }

    //Get 取已装载的维度定义 未装载返回 null
    public static LevelStem? Get(Identifier dimensionId)
        => BuiltInRegistries.LEVEL_STEM.GetValue(dimensionId) as LevelStem;
}
