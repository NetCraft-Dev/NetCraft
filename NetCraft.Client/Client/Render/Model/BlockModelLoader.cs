using System.Numerics;
using System.Text.Json;
using NetCraft.Gpu;
using NetCraft.Resources;
using NetCraft.Registry;

namespace NetCraft.Game.Client.Render.Model;

//BlockModelLoader 方块模型 JSON 加载器对标原版 ModelBakery 模型加载部分
//从 ResourceManager 读 models/block/*.json 反序列化为 UnbakedModel
//递归解析 parent 合并父模型的 elements 和 textures
//纹理变量解析 #all → 实际纹理路径 minecraft:block/stone
//属 Game 层依赖 ResourceManager 读 assets
public sealed class BlockModelLoader
{
    private readonly ResourceManager _resourceManager;
    //模型缓存按 modelId 缓存已解析的 UnbakedModel 避免重复加载
    private readonly Dictionary<string, UnbakedModel> _cache = new();

    public BlockModelLoader(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    //Load 加载并解析模型
    //modelId 格式 minecraft:block/stone 对应 assets/minecraft/models/block/stone.json
    //递归解析 parent 合并 elements（子模型优先）和 textures（子模型覆盖父模型）
    //elements 只取继承链中最具体的（子模型有 elements 就用子的不继承父的）
    //textures 父子合并子覆盖父
    public UnbakedModel Load(string modelId)
    {
        if (_cache.TryGetValue(modelId, out var cached))
            return cached;
        var model = LoadRaw(modelId);
        ResolveParent(model, new HashSet<string>());
        _cache[modelId] = model;
        return model;
    }

    //LoadRaw 从 ResourceManager 读 JSON 反序列化为 UnbakedModel（不解析 parent）
    private UnbakedModel LoadRaw(string modelId)
    {
        var (ns, path) = ParseModelId(modelId);
        var location = Identifier.FromNamespaceAndPath(ns, $"models/{path}.json");
        var resource = _resourceManager.GetResource(PackType.ClientResources, location)
            ?? throw new FileNotFoundException($"模型文件不存在 {location}");
        using var stream = resource.Open();
        var json = JsonDocument.Parse(stream);
        return ParseModel(json.RootElement);
    }

    //ParseModel 从 JSON 元素解析 UnbakedModel
    private static UnbakedModel ParseModel(JsonElement element)
    {
        var model = new UnbakedModel();
        if (element.TryGetProperty("parent", out var parentEl))
            model.Parent = NormalizeModelId(parentEl.GetString()!);
        if (element.TryGetProperty("textures", out var texEl))
        {
            foreach (var prop in texEl.EnumerateObject())
                model.Textures[prop.Name] = prop.Value.GetString()!;
        }
        if (element.TryGetProperty("elements", out var elemEl))
        {
            foreach (var elem in elemEl.EnumerateArray())
                model.Elements.Add(ParseElement(elem));
        }
        return model;
    }

    //ParseElement 解析单个元素 from/to/faces
    private static ModelElement ParseElement(JsonElement element)
    {
        var from = ParseVec3(element.GetProperty("from"));
        var to = ParseVec3(element.GetProperty("to"));
        var elem = new ModelElement(from, to);
        if (element.TryGetProperty("faces", out var facesEl))
        {
            foreach (var prop in facesEl.EnumerateObject())
            {
                var dir = ParseDirection(prop.Name);
                elem.Faces.Add(ParseFace(dir, prop.Value));
            }
        }
        return elem;
    }

    //ParseFace 解析面 texture/cullface/uv/tintindex
    private static ModelFace ParseFace(Direction direction, JsonElement element)
    {
        var face = new ModelFace(direction);
        if (element.TryGetProperty("texture", out var texEl))
            face.Texture = texEl.GetString()!;
        if (element.TryGetProperty("cullface", out var cullEl))
            face.Cullface = ParseDirection(cullEl.GetString()!);
        if (element.TryGetProperty("uv", out var uvEl) && uvEl.ValueKind == JsonValueKind.Array)
        {
            var arr = uvEl.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            face.UV = new Vector4(arr[0], arr[1], arr[2], arr[3]);
        }
        if (element.TryGetProperty("tintindex", out var tintEl))
            face.TintIndex = tintEl.GetInt32();
        return face;
    }

    //ResolveParent 递归解析 parent 合并 elements 和 textures
    //elements 继承规则：子模型有 elements 用子的没有则继承父的
    //textures 继承规则：父子合并子覆盖父（子模型 textures 优先）
    //visited 防止 parent 循环引用
    private void ResolveParent(UnbakedModel model, HashSet<string> visited)
    {
        if (model.IsResolved) return;
        if (model.Parent is null)
        {
            model.IsResolved = true;
            return;
        }
        if (!visited.Add(model.Parent))
            throw new InvalidOperationException($"模型 parent 循环引用 {model.Parent}");
        var parent = Load(model.Parent);
        //子模型无 elements 继承父的
        if (model.Elements.Count == 0 && parent.Elements.Count > 0)
        {
            //深拷贝父 elements 避免修改父模型
            foreach (var pe in parent.Elements)
            {
                var copy = new ModelElement(pe.From, pe.To);
                foreach (var f in pe.Faces)
                    copy.Faces.Add(new ModelFace(f.Direction)
                    {
                        Texture = f.Texture,
                        Cullface = f.Cullface,
                        UV = f.UV,
                        TintIndex = f.TintIndex
                    });
                model.Elements.Add(copy);
            }
        }
        //textures 父子合并子优先
        foreach (var (key, value) in parent.Textures)
        {
            if (!model.Textures.ContainsKey(key))
                model.Textures[key] = value;
        }
        model.IsResolved = true;
    }

    //ResolveTexture 解析纹理变量引用得到最终纹理路径
    //#all → 查 Textures["all"] → 若值又是 #xxx 则递归直到非 # 开头
    //返回 sprite name 如 minecraft:block/stone
    //找不到变量抛 KeyNotFoundException
    public static string ResolveTexture(UnbakedModel model, string textureRef)
    {
        var current = textureRef;
        var visited = new HashSet<string>();
        while (current.StartsWith('#'))
        {
            if (!visited.Add(current))
                throw new InvalidOperationException($"纹理变量循环引用 {current}");
            var varName = current[1..];
            if (!model.Textures.TryGetValue(varName, out var resolved))
                throw new KeyNotFoundException($"纹理变量 {varName} 未定义");
            current = resolved;
        }
        return NormalizeTextureId(current);
    }

    //NormalizeModelId 规范化模型 id 加 minecraft: 前缀
    //block/cube_all → minecraft:block/cube_all
    private static string NormalizeModelId(string id)
        => id.Contains(':') ? id : $"minecraft:{id}";

    //NormalizeTextureId 规范化纹理 id 加 minecraft: 前缀
    //block/stone → minecraft:block/stone
    private static string NormalizeTextureId(string id)
        => id.Contains(':') ? id : $"minecraft:{id}";

    //ParseModelId 拆分 modelId 为 namespace 和 path
    //minecraft:block/stone → (minecraft, block/stone)
    private static (string ns, string path) ParseModelId(string modelId)
    {
        var idx = modelId.IndexOf(':');
        if (idx < 0) return ("minecraft", modelId);
        return (modelId[..idx], modelId[(idx + 1)..]);
    }

    private static Vector3 ParseVec3(JsonElement element)
    {
        var arr = element.EnumerateArray().Select(x => x.GetSingle()).ToArray();
        return new Vector3(arr[0], arr[1], arr[2]);
    }

    private static Direction ParseDirection(string name)
        => name.ToLowerInvariant() switch
        {
            "down" => Direction.Down,
            "up" => Direction.Up,
            "north" => Direction.North,
            "south" => Direction.South,
            "west" => Direction.West,
            "east" => Direction.East,
            _ => throw new ArgumentException($"未知方向 {name}")
        };
}
