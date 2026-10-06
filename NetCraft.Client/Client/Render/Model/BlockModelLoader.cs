using System.Numerics;
using System.Text.Json;
using NetCraft.Gpu;
using NetCraft.Resources;
using NetCraft.Registry;

namespace NetCraft.Game.Client.Render.Model;

//BlockModelLoader block model JSON loader, maps to the model-loading part of vanilla ModelBakery
//Reads models/block/*.json from ResourceManager and deserializes into UnbakedModel
//Recursively resolves parent, merging the parent model's elements and textures
//Texture variable resolution: #all → actual texture path minecraft:block/stone
//Belongs to the Game layer; depends on ResourceManager to read assets
public sealed class BlockModelLoader
{
    private readonly ResourceManager _resourceManager;
    //Model cache keyed by modelId holding resolved UnbakedModels, to avoid re-loading
    private readonly Dictionary<string, UnbakedModel> _cache = new();

    public BlockModelLoader(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    //Load loads and resolves a model
    //modelId format minecraft:block/stone, maps to assets/minecraft/models/block/stone.json
    //Recursively resolve parent, merging elements (child takes priority) and textures (child overrides parent)
    //elements takes only the most specific in the chain (if the child has elements use them, don't inherit the parent's)
    //textures parent merged with child, child overrides parent
    public UnbakedModel Load(string modelId)
    {
        if (_cache.TryGetValue(modelId, out var cached))
            return cached;
        var model = LoadRaw(modelId);
        ResolveParent(model, new HashSet<string>());
        _cache[modelId] = model;
        return model;
    }

    //LoadRaw reads JSON from ResourceManager and deserializes into UnbakedModel (without resolving parent)
    private UnbakedModel LoadRaw(string modelId)
    {
        var (ns, path) = ParseModelId(modelId);
        var location = Identifier.FromNamespaceAndPath(ns, $"models/{path}.json");
        var resource = _resourceManager.GetResource(PackType.ClientResources, location)
            ?? throw new FileNotFoundException($"Model file does not exist {location}");
        using var stream = resource.Open();
        var json = JsonDocument.Parse(stream);
        return ParseModel(json.RootElement);
    }

    //ParseModel parses UnbakedModel from a JSON element
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

    //ParseElement parses a single element from/to/faces
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

    //ParseFace parses a face's texture/cullface/uv/tintindex
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

    //ResolveParent recursively resolves parent, merging elements and textures
    //elements inheritance rule: if the child has elements use them, otherwise inherit the parent's
    //textures inheritance rule: parent merged with child, child overrides parent (child textures take priority)
    //visited prevents parent circular references
    private void ResolveParent(UnbakedModel model, HashSet<string> visited)
    {
        if (model.IsResolved) return;
        if (model.Parent is null)
        {
            model.IsResolved = true;
            return;
        }
        if (!visited.Add(model.Parent))
            throw new InvalidOperationException($"Model parent circular reference {model.Parent}");
        var parent = Load(model.Parent);
        //Child has no elements, inherit the parent's
        if (model.Elements.Count == 0 && parent.Elements.Count > 0)
        {
            //Deep copy the parent's elements to avoid mutating the parent model
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
        //textures parent merged with child, child priority
        foreach (var (key, value) in parent.Textures)
        {
            if (!model.Textures.ContainsKey(key))
                model.Textures[key] = value;
        }
        model.IsResolved = true;
    }

    //ResolveTexture resolves a texture variable reference to the final texture path
    //#all → look up Textures["all"] → if the value is again #xxx, recurse until it no longer starts with #
    //Returns a sprite name such as minecraft:block/stone
    //Throws KeyNotFoundException when the variable is not found
    public static string ResolveTexture(UnbakedModel model, string textureRef)
    {
        var current = textureRef;
        var visited = new HashSet<string>();
        while (current.StartsWith('#'))
        {
            if (!visited.Add(current))
                throw new InvalidOperationException($"Texture variable circular reference {current}");
            var varName = current[1..];
            if (!model.Textures.TryGetValue(varName, out var resolved))
                throw new KeyNotFoundException($"Texture variable {varName} is undefined");
            current = resolved;
        }
        return NormalizeTextureId(current);
    }

    //NormalizeModelId normalizes a model id, adding the minecraft: prefix
    //block/cube_all → minecraft:block/cube_all
    private static string NormalizeModelId(string id)
        => id.Contains(':') ? id : $"minecraft:{id}";

    //NormalizeTextureId normalizes a texture id, adding the minecraft: prefix
    //block/stone → minecraft:block/stone
    private static string NormalizeTextureId(string id)
        => id.Contains(':') ? id : $"minecraft:{id}";

    //ParseModelId splits modelId into namespace and path
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
            _ => throw new ArgumentException($"Unknown direction {name}")
        };
}
