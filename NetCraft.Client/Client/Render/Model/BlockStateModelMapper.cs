using System.Text.Json;
using NetCraft.Gpu;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Resources;

namespace NetCraft.Game.Client.Render.Model;

//BlockStateModelMapper BlockState→BakedModel 映射器对标原版 BlockModelShaper
//读 blockstates/*.json 的 variants 按 BlockState 属性匹配选模型 id
//variants 键格式 "snowy=false" 或 "facing=north,powered=true"
//匹配规则 BlockState 所有属性都匹配 variant 键值对
//匹配后拿 model id 调 BlockModelLoader.Load + BlockModelBaker.Bake 得到 BakedModel
//缓存映射结果避免重复烘焙
//首版不支持 multipart 仅支持 variants
public sealed class BlockStateModelMapper
{
    private readonly ResourceManager _resourceManager;
    private readonly BlockModelLoader _loader;
    private readonly BlockModelBaker _baker;
    //按 BlockState.Id 缓存 BakedModel
    private readonly Dictionary<int, BakedModel?> _cache = new();

    public BlockStateModelMapper(ResourceManager resourceManager, BlockModelLoader loader, BlockModelBaker baker)
    {
        _resourceManager = resourceManager;
        _loader = loader;
        _baker = baker;
    }

    //GetModel 按 BlockState 查 BakedModel
    //返回 null 表示无匹配 variant 或模型加载失败
    public BakedModel? GetModel(BlockState state)
    {
        if (_cache.TryGetValue(state.Id, out var cached))
            return cached;
        var model = LoadModel(state);
        _cache[state.Id] = model;
        return model;
    }

    //LoadModel 读 blockstates JSON 匹配 variant 烘焙模型
    private BakedModel? LoadModel(BlockState state)
    {
        var block = BlockStateRegistry.Owner(state.Id);
        var location = Identifier.FromNamespaceAndPath(block.Id.Namespace, $"blockstates/{block.Id.Path}.json");
        var resource = _resourceManager.GetResource(PackType.ClientResources, location);
        if (resource is null) return null;
        using var stream = resource.Open();
        var json = JsonDocument.Parse(stream);
        if (!json.RootElement.TryGetProperty("variants", out var variantsEl))
            return null;
        //构造 BlockState 属性字典 key=属性名 value=属性值字符串
        var props = new Dictionary<string, string>();
        foreach (var pv in BlockStateRegistry.GetValues(state.Id))
            props[pv.Property.Name] = FormatValue(pv.Value);
        //遍历 variants 找匹配键
        foreach (var prop in variantsEl.EnumerateObject())
        {
            if (!MatchesVariantKey(prop.Name, props)) continue;
            if (ParseVariant(prop.Value) is not { } variant) continue;
            var unbaked = _loader.Load(variant.Model);
            return _baker.Bake(unbaked, variant.RotationX, variant.RotationY);
        }
        return null;
    }

    //MatchesVariantKey 检查 variant 键是否匹配 BlockState 属性
    //键格式 "snowy=false" 或 "facing=north,powered=true"
    //空键匹配无属性的 BlockState（默认状态）
    private static bool MatchesVariantKey(string key, Dictionary<string, string> props)
    {
        if (string.IsNullOrEmpty(key))
            return props.Count == 0;
        var pairs = key.Split(',');
        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) return false;
            var k = pair[..eq];
            var v = pair[(eq + 1)..];
            if (!props.TryGetValue(k, out var actual) || actual != v)
                return false;
        }
        return true;
    }

    //ParseVariant 从 variant 值解析模型 id 与 x/y 旋转
    //variant 值可能是单个对象 {"model":"minecraft:block/stone"} 或数组 [{"model":"...","y":90},...]
    //x/y 是模型绕方块中心的旋转 对应原版 Variant 的 SimpleModelState
    //缺了它们贴面方块会全部落在模型文件的基准朝向上 拉杆按钮火把只有朝北那一面渲染得对
    private static Variant? ParseVariant(JsonElement variantEl)
    {
        var target = variantEl;
        if (target.ValueKind == JsonValueKind.Array)
        {
            if (target.GetArrayLength() == 0) return null;
            target = target[0];
        }
        if (target.ValueKind != JsonValueKind.Object) return null;
        if (!target.TryGetProperty("model", out var modelEl) || modelEl.GetString() is not { } model)
            return null;
        return new Variant(model, ReadRotation(target, "x"), ReadRotation(target, "y"));
    }

    //ReadRotation 读 variant 的 x/y 旋转角 缺省 0
    private static int ReadRotation(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var degrees) ? degrees : 0;

    //Variant 一条匹配到的变体 模型 id 与绕方块中心的旋转
    private readonly record struct Variant(string Model, int RotationX, int RotationY);

    //FormatValue 格式化属性值为字符串用于 variant 匹配
    //bool → true/false enum → 小写名 int → 数字
    private static string FormatValue(object value)
    {
        if (value is bool b) return b ? "true" : "false";
        if (value is Enum e) return e.ToString().ToLowerInvariant();
        return value?.ToString() ?? "";
    }
}
