using System.IO;
using System.Text.Json.Nodes;
using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Resources;

namespace NetCraft.Game.World.Crafting;

//RecipeManager 配方管理器对应原版 RecipeManager
//负责把数据包 recipe 目录读成内存表并提供合成与切石机查询
//配方书下发与玩家解锁留到配方书阶段
public sealed class RecipeManager
{
    //Active 当前生效的配方管理器 服务端装配时设置 菜单取它算合成结果
    public static RecipeManager? Active { get; set; }

    //RecipeFolder 配方数据目录 对应原版 FileToIdConverter.registry(Registries.RECIPE)
    private const string RecipeFolder = "recipe";

    private readonly Dictionary<Identifier, RecipeHolder> _byId = new();
    //只装合成配方 网格每次变化都要扫一遍 不把熔炼切石那类掺进来
    private readonly List<CraftingRecipe> _crafting = new();
    //切石机配方按输入物品查 数量比合成少 同样线性扫
    private readonly List<StonecutterRecipe> _stonecutting = new();
    //烹饪配方按类型分桶 熔炼四件套各查各的 对应原版按 RecipeType 索引的配方表
    private readonly Dictionary<string, List<AbstractCookingRecipe>> _cooking = new();

    //Count 已加载的合成配方数
    public int Count => _byId.Count;

    //StonecutterRecipeCount 已加载的切石机配方数
    public int StonecutterRecipeCount => _stonecutting.Count;

    //CookingRecipeCount 已加载的烹饪配方数(熔炼四件套合计)
    public int CookingRecipeCount
    {
        get
        {
            var total = 0;
            foreach (var list in _cooking.Values) total += list.Count;
            return total;
        }
    }

    //Load 扫描所有数据包的 recipe 目录 单个文件失败只记错误不中断整体
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

    //GetCraftingResult 按合成网格算产出 没有命中返回空栈
    //配方数量不大直接线性扫 原版那套按类型索引与上次命中缓存等有需要再加
    public ItemStack GetCraftingResult(CraftingInput input)
    {
        if (input.IsEmpty) return ItemStack.Empty;
        foreach (var recipe in _crafting)
            if (recipe.Matches(input))
                return recipe.Assemble(input);
        return ItemStack.Empty;
    }

    //GetStonecutterRecipes 取该输入能用的全部切石机配方 对应原版 stonecutterRecipes().selectByInput
    //客户端选择列表按返回顺序展示 顺序即数据包加载顺序
    public IReadOnlyList<StonecutterRecipe> GetStonecutterRecipes(ItemStack input)
    {
        if (input.IsEmpty()) return Array.Empty<StonecutterRecipe>();
        var stock = new SingleRecipeInput(input);
        var matches = new List<StonecutterRecipe>();
        foreach (var recipe in _stonecutting)
            if (recipe.Matches(stock)) matches.Add(recipe);
        return matches;
    }

    //GetRecipe 按 id 取配方
    public RecipeHolder? GetRecipe(Identifier id)
        => _byId.TryGetValue(id, out var holder) ? holder : null;

    //GetCookingRecipe 按烹饪类型与输入物品查配方 对应原版 RecipeManager.CachedCheck.getRecipeFor
    //没命中返回 null 熔炉据此决定烧不烧 输入为空直接不查
    public AbstractCookingRecipe? GetCookingRecipe(string type, ItemStack input)
    {
        if (input.IsEmpty()) return null;
        if (!_cooking.TryGetValue(type, out var list)) return null;
        var stock = new SingleRecipeInput(input);
        foreach (var recipe in list)
            if (recipe.Matches(stock)) return recipe;
        return null;
    }

    //TryLoadOne 读单个配方文件并按 type 分派解析 成功才写进内存表
    //对应原版按 RecipeSerializer 建表的分派 不认识的类型只记错误
    private bool TryLoadOne(Resource resource, Identifier id, out string? error)
    {
        error = null;
        try
        {
            using var stream = resource.Open();
            var json = JsonOps.Parse(stream);
            if (!json.Result().IsPresent)
            {
                error = $"{id}: JSON 解析失败 {Describe(json)}";
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

    //TryLoadCrafting 读合成配方
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

    //TryLoadStonecutter 读切石机配方
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

    //TryLoadCooking 读烹饪配方 按 type 落到对应桶
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

    //Describe 取 codec 失败消息
    private static string Describe<T>(DataResult<T> result)
    {
        var message = "未知错误";
        result.ResultOrPartial(m => message = m);
        return message;
    }
}
