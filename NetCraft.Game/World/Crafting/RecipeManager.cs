using System.IO;
using System.Text.Json.Nodes;
using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Resources;

namespace NetCraft.Game.World.Crafting;

//RecipeManager recipe manager, maps to vanilla RecipeManager
//Reads the datapack recipe directory into in-memory tables and serves crafting and stonecutter queries
//Recipe book delivery and player unlocks are left to the recipe book stage
public sealed class RecipeManager
{
    //Active the currently active recipe manager, set when the server wires up, menus read it to compute crafting results
    public static RecipeManager? Active { get; set; }

    //RecipeFolder recipe data directory, maps to vanilla FileToIdConverter.registry(Registries.RECIPE)
    private const string RecipeFolder = "recipe";

    private readonly Dictionary<Identifier, RecipeHolder> _byId = new();
    //Holds only crafting recipes, the grid rescans on every change, cooking and stonecutting are kept out
    private readonly List<CraftingRecipe> _crafting = new();
    //Stonecutter recipes are queried by input item; there are fewer than crafting recipes and they are also scanned linearly
    private readonly List<StonecutterRecipe> _stonecutting = new();
    //Cooking recipes are bucketed by type, the four variants each look up their own, maps to the recipe table indexed by RecipeType in vanilla
    private readonly Dictionary<string, List<AbstractCookingRecipe>> _cooking = new();

    //Count number of loaded crafting recipes
    public int Count => _byId.Count;

    //StonecutterRecipeCount number of loaded stonecutter recipes
    public int StonecutterRecipeCount => _stonecutting.Count;

    //CookingRecipeCount number of loaded cooking recipes (all four variants combined)
    public int CookingRecipeCount
    {
        get
        {
            var total = 0;
            foreach (var list in _cooking.Values) total += list.Count;
            return total;
        }
    }

    //Load scans the recipe directory of all datapacks; a single file failure only logs an error and does not stop the rest
    public LoadResult Load(ResourceManager resourceManager)
    {
        _byId.Clear();
        _crafting.Clear();
        _stonecutting.Clear();
        _cooking.Clear();
        var errors = new List<string>();
        var prefix = RecipeFolder + "/";
        foreach (var ns in resourceManager.GetNamespaces(PackType.ServerData))
            foreach (var resource in resourceManager.ListResources(PackType.ServerData, ns, RecipeFolder))
            {
                var path = resource.Location.Path;
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var recipePath = path[prefix.Length..];
                if (!recipePath.EndsWith(".json", StringComparison.Ordinal)) continue;
                recipePath = recipePath[..^5];
                if (recipePath.Length == 0) continue;
                var id = Identifier.FromNamespaceAndPath(resource.Location.Namespace, recipePath);
                if (!TryLoadOne(resource, id, out var error)) errors.Add(error!);
            }
        return new LoadResult(_byId.Count, errors);
    }

    //GetCraftingResult computes the output from the crafting grid, returns an empty stack when nothing hits
    //With few recipes a linear scan is fine; vanilla's type indexing and last-hit cache can be added if needed
    public ItemStack GetCraftingResult(CraftingInput input)
    {
        if (input.IsEmpty) return ItemStack.Empty;
        foreach (var recipe in _crafting)
            if (recipe.Matches(input))
                return recipe.Assemble(input);
        return ItemStack.Empty;
    }

    //GetStonecutterRecipes returns all stonecutter recipes usable for the input, maps to vanilla stonecutterRecipes().selectByInput
    //The client selection list shows them in the returned order, which is the datapack load order
    public IReadOnlyList<StonecutterRecipe> GetStonecutterRecipes(ItemStack input)
    {
        if (input.IsEmpty()) return Array.Empty<StonecutterRecipe>();
        var stock = new SingleRecipeInput(input);
        var matches = new List<StonecutterRecipe>();
        foreach (var recipe in _stonecutting)
            if (recipe.Matches(stock)) matches.Add(recipe);
        return matches;
    }

    //GetRecipe returns a recipe by id
    public RecipeHolder? GetRecipe(Identifier id)
        => _byId.TryGetValue(id, out var holder) ? holder : null;

    //GetCookingRecipe looks up a recipe by cooking type and input item, maps to vanilla RecipeManager.CachedCheck.getRecipeFor
    //Returns null when nothing hits, the furnace uses it to decide whether to burn, an empty input skips the lookup
    public AbstractCookingRecipe? GetCookingRecipe(string type, ItemStack input)
    {
        if (input.IsEmpty()) return null;
        if (!_cooking.TryGetValue(type, out var list)) return null;
        var stock = new SingleRecipeInput(input);
        foreach (var recipe in list)
            if (recipe.Matches(stock)) return recipe;
        return null;
    }

    //TryLoadOne reads a single recipe file and dispatches parsing by type, only writes to the in-memory table on success
    //Maps to the dispatch-by-RecipeSerializer table in vanilla, unknown types only log an error
    private bool TryLoadOne(Resource resource, Identifier id, out string? error)
    {
        error = null;
        try
        {
            using var stream = resource.Open();
            var json = JsonOps.Parse(stream);
            if (!json.Result().IsPresent)
            {
                error = $"{id}: JSON parse failed {Describe(json)}";
                return false;
            }
            var node = json.GetOrThrow();
            var type = RecipeCodec.ReadType(JsonOps.Instance, node);
            if (!type.Result().IsPresent)
            {
                error = $"{id}: {Describe(type)}";
                return false;
            }
            switch (type.GetOrThrow())
            {
                case ShapedRecipe.SerializerId:
                case ShapelessRecipe.SerializerId:
                    return TryLoadCrafting(node, id, out error);
                case StonecutterRecipe.SerializerId:
                    return TryLoadStonecutter(node, id, out error);
                case SmeltingRecipe.SerializerId:
                case BlastingRecipe.SerializerId:
                case SmokingRecipe.SerializerId:
                case CampfireCookingRecipe.SerializerId:
                    return TryLoadCooking(node, id, type.GetOrThrow(), out error);
                default:
                    error = $"{id}: unsupported recipe type {type.GetOrThrow()}";
                    return false;
            }
        }
        catch (Exception e)
        {
            error = $"{id}: {e.Message}";
            return false;
        }
    }

    //TryLoadCrafting reads a crafting recipe
    private bool TryLoadCrafting(JsonNode? node, Identifier id, out string? error)
    {
        var parsed = RecipeCodec.Instance.Parse(JsonOps.Instance, node);
        if (!parsed.Result().IsPresent)
        {
            error = $"{id}: {Describe(parsed)}";
            return false;
        }
        error = null;
        var value = parsed.GetOrThrow();
        _byId[id] = new RecipeHolder(id, value);
        if (value is CraftingRecipe crafting) _crafting.Add(crafting);
        return true;
    }

    //TryLoadStonecutter reads a stonecutter recipe
    private bool TryLoadStonecutter(JsonNode? node, Identifier id, out string? error)
    {
        var parsed = StonecutterRecipeCodec.Instance.Parse(JsonOps.Instance, node);
        if (!parsed.Result().IsPresent)
        {
            error = $"{id}: {Describe(parsed)}";
            return false;
        }
        error = null;
        _stonecutting.Add(parsed.GetOrThrow());
        return true;
    }

    //TryLoadCooking reads a cooking recipe and files it into the bucket matching its type
    private bool TryLoadCooking(JsonNode? node, Identifier id, string type, out string? error)
    {
        var parsed = CookingRecipeCodec.Instance.Parse(JsonOps.Instance, node, type);
        if (!parsed.Result().IsPresent)
        {
            error = $"{id}: {Describe(parsed)}";
            return false;
        }
        error = null;
        if (!_cooking.TryGetValue(type, out var list))
            _cooking[type] = list = new List<AbstractCookingRecipe>();
        list.Add(parsed.GetOrThrow());
        return true;
    }

    //Describe returns the codec failure message
    private static string Describe<T>(DataResult<T> result)
    {
        var message = "unknown error";
        result.ResultOrPartial(m => message = m);
        return message;
    }
}
