using System.Text.Json;
using NetCraft.Gpu;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Resources;

namespace NetCraft.Game.Client.Render.Model;

//BlockStateModelMapper BlockState→BakedModel mapper, maps to vanilla BlockModelShaper
//Reads the variants of blockstates/*.json and matches BlockState properties to select a model id
//variants key format "snowy=false" or "facing=north,powered=true"
//Match rule: all BlockState properties must match the variant key-value pairs
//On a match, take the model id and call BlockModelLoader.Load + BlockModelBaker.Bake to get a BakedModel
//Cache the mapping result to avoid re-baking
//The first version does not support multipart, only variants
public sealed class BlockStateModelMapper
{
    private readonly ResourceManager _resourceManager;
    private readonly BlockModelLoader _loader;
    private readonly BlockModelBaker _baker;
    //Cache BakedModel by BlockState.Id
    private readonly Dictionary<int, BakedModel?> _cache = new();

    public BlockStateModelMapper(ResourceManager resourceManager, BlockModelLoader loader, BlockModelBaker baker)
    {
        _resourceManager = resourceManager;
        _loader = loader;
        _baker = baker;
    }

    //GetModel looks up BakedModel by BlockState
    //Returns null when no variant matches or the model fails to load
    public BakedModel? GetModel(BlockState state)
    {
        if (_cache.TryGetValue(state.Id, out var cached))
            return cached;
        var model = LoadModel(state);
        _cache[state.Id] = model;
        return model;
    }

    //LoadModel reads the blockstates JSON, matches a variant, and bakes the model
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
        //Build the BlockState property dictionary: key=property name, value=property value string
        var props = new Dictionary<string, string>();
        foreach (var pv in BlockStateRegistry.GetValues(state.Id))
            props[pv.Property.Name] = FormatValue(pv.Value);
        //Iterate variants to find a matching key
        foreach (var prop in variantsEl.EnumerateObject())
        {
            if (!MatchesVariantKey(prop.Name, props)) continue;
            if (ParseVariant(prop.Value) is not { } variant) continue;
            var unbaked = _loader.Load(variant.Model);
            return _baker.Bake(unbaked, variant.RotationX, variant.RotationY);
        }
        return null;
    }

    //MatchesVariantKey checks whether a variant key matches the BlockState properties
    //Key format "snowy=false" or "facing=north,powered=true"
    //An empty key matches a BlockState with no properties (the default state)
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

    //ParseVariant parses the model id and x/y rotation from a variant value
    //A variant value may be a single object {"model":"minecraft:block/stone"} or an array [{"model":"...","y":90},...]
    //x/y are the model's rotation about the block center, corresponding to vanilla Variant's SimpleModelState
    //Without them, facing blocks would all fall on the model file's base orientation; for levers/buttons/torches only the north-facing side would render correctly
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

    //ReadRotation reads the variant's x/y rotation angle, default 0
    private static int ReadRotation(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var degrees) ? degrees : 0;

    //Variant a matched variant: model id and rotation about the block center
    private readonly record struct Variant(string Model, int RotationX, int RotationY);

    //FormatValue formats a property value as a string for variant matching
    //bool → true/false, enum → lowercase name, int → number
    private static string FormatValue(object value)
    {
        if (value is bool b) return b ? "true" : "false";
        if (value is Enum e) return e.ToString().ToLowerInvariant();
        return value?.ToString() ?? "";
    }
}
