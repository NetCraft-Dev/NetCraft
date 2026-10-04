using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Network.Component;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Crafting;

//RecipeCodec 合成配方编解码 对应原版 Recipe.CODEC 按 RECIPE_SERIALIZER 分派的语义
//本作 Codec 层没有 dispatch 组合子 这里按业务层既有做法手写: 先读 type 再交给对应子解析
internal sealed class RecipeCodec : ScalarCodec<Recipe<CraftingInput>>
{
    public static readonly RecipeCodec Instance = new();

    public override DataResult<Recipe<CraftingInput>> Parse<U>(DynamicOps<U> ops, U input)
        => ReadType(ops, input).FlatMap(type => ParseBody(ops, input, type));

    //ReadType 读 type 字段并去掉命名空间 供配方管理器按类型分派解析器
    internal static DataResult<string> ReadType<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var typeField = map.Get("type");
            if (!typeField.IsPresent)
                return DataResult<string>.Error(() => "配方缺 type 字段");
            var typeResult = ops.GetStringValue(typeField.Get());
            if (!typeResult.Result().IsPresent)
                return DataResult<string>.Error(() => "配方 type 必须是字符串");
            return DataResult<string>.Success(StripNamespace(typeResult.GetOrThrow()));
        });

    //ParseBody 已知道 type 之后按类型解析配方本体
    private static DataResult<Recipe<CraftingInput>> ParseBody<U>(DynamicOps<U> ops, U input, string type)
        => ops.GetMap(input).FlatMap(map =>
        {
            var group = ReadString(ops, map, "group", string.Empty);
            var category = ReadString(ops, map, "category", "misc");
            var showNotification = ReadBool(ops, map, "show_notification", true);

            //子解析吃原始节点 图案与结果都在同一层 map 里
            return type switch
            {
                ShapedRecipe.SerializerId => ParseShaped(ops, input, map, group, category, showNotification),
                ShapelessRecipe.SerializerId => ParseShapeless(ops, input, map, group, category, showNotification),
                _ => DataResult<Recipe<CraftingInput>>.Error(() => $"unsupported recipe type {type}"),
            };
        });

    //ParseShaped 解析 crafting_shaped
    private static DataResult<Recipe<CraftingInput>> ParseShaped<U>(DynamicOps<U> ops, U input, MapLike<U> map,
        string group, string category, bool showNotification)
    {
        var result = ParseResult(ops, map);
        if (!result.Result().IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(
                () => $"配方 result 不合法: {result.Result().ToString()}");
        return ShapedRecipePattern.Codec.Parse(ops, input).FlatMap(pattern =>
            DataResult<Recipe<CraftingInput>>.Success(
                new ShapedRecipe(pattern, result.GetOrThrow(), group, category, showNotification)));
    }

    //ParseShapeless 解析 crafting_shapeless
    private static DataResult<Recipe<CraftingInput>> ParseShapeless<U>(DynamicOps<U> ops, U input, MapLike<U> map,
        string group, string category, bool showNotification)
    {
        var ingredientsField = map.Get("ingredients");
        if (!ingredientsField.IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(() => "无序配方缺 ingredients 字段");
        var result = ParseResult(ops, map);
        if (!result.Result().IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(
                () => $"配方 result 不合法: {result.Result().ToString()}");

        var stream = ops.GetStream(ingredientsField.Get());
        if (!stream.Result().IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(() => "配方 ingredients 必须是数组");
        var ingredients = new List<Ingredient>();
        foreach (var element in stream.GetOrThrow())
        {
            var parsed = Ingredient.Codec.Parse(ops, element);
            if (!parsed.Result().IsPresent)
                return DataResult<Recipe<CraftingInput>>.Error(
                    () => $"配方 ingredients 有不合法项: {parsed.Result().ToString()}");
            ingredients.Add(parsed.GetOrThrow());
        }
        if (ingredients.Count == 0)
            return DataResult<Recipe<CraftingInput>>.Error(() => "无序配方 ingredients 不能为空");
        return DataResult<Recipe<CraftingInput>>.Success(
            new ShapelessRecipe(ingredients, result.GetOrThrow(), group, category, showNotification));
    }

    //ParseResult 解析 result 字段 对应原版 ItemStackTemplate.CODEC
    internal static DataResult<ItemStack> ParseResult<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var resultField = map.Get("result");
        return resultField.IsPresent
            ? ResultStackCodec.Instance.Parse(ops, resultField.Get())
            : DataResult<ItemStack>.Error(() => "配方缺 result 字段");
    }

    internal static string StripNamespace(string type)
    {
        var colon = type.IndexOf(':');
        return colon >= 0 ? type[(colon + 1)..] : type;
    }

    internal static string ReadString<U>(DynamicOps<U> ops, MapLike<U> map, string name, string fallback)
    {
        var field = map.Get(name);
        if (!field.IsPresent) return fallback;
        var text = ops.GetStringValue(field.Get());
        return text.Result().IsPresent ? text.GetOrThrow() : fallback;
    }

    internal static float ReadFloat<U>(DynamicOps<U> ops, MapLike<U> map, string name, float fallback)
    {
        var field = map.Get(name);
        if (!field.IsPresent) return fallback;
        var value = ops.GetNumberValue(field.Get());
        return value.Result().IsPresent ? (float)value.GetOrThrow() : fallback;
    }

    internal static int ReadInt<U>(DynamicOps<U> ops, MapLike<U> map, string name, int fallback)
    {
        var field = map.Get(name);
        if (!field.IsPresent) return fallback;
        var value = ops.GetNumberValue(field.Get());
        return value.Result().IsPresent ? (int)value.GetOrThrow() : fallback;
    }

    private static bool ReadBool<U>(DynamicOps<U> ops, MapLike<U> map, string name, bool fallback)
    {
        var field = map.Get(name);
        if (!field.IsPresent) return fallback;
        var value = ops.GetBooleanValue(field.Get());
        return value.Result().IsPresent ? value.GetOrThrow() : fallback;
    }
}

//StonecutterRecipeCodec 切石机配方编解码 对应原版 SingleItemRecipe.simpleMapCodec
//JSON 形态 {"type":"minecraft:stonecutting","ingredient":..,"result":..} group 可省
internal sealed class StonecutterRecipeCodec : ScalarCodec<StonecutterRecipe>
{
    public static readonly StonecutterRecipeCodec Instance = new();

    public override DataResult<StonecutterRecipe> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var ingredientField = map.Get("ingredient");
            if (!ingredientField.IsPresent)
                return DataResult<StonecutterRecipe>.Error(() => "切石机配方缺 ingredient 字段");
            var ingredient = Ingredient.Codec.Parse(ops, ingredientField.Get());
            if (!ingredient.Result().IsPresent)
                return DataResult<StonecutterRecipe>.Error(
                    () => $"配方 ingredient 不合法: {ingredient.Result().ToString()}");
            var result = RecipeCodec.ParseResult(ops, map);
            if (!result.Result().IsPresent)
                return DataResult<StonecutterRecipe>.Error(
                    () => $"配方 result 不合法: {result.Result().ToString()}");
            var group = RecipeCodec.ReadString(ops, map, "group", string.Empty);
            return DataResult<StonecutterRecipe>.Success(
                new StonecutterRecipe(ingredient.GetOrThrow(), result.GetOrThrow(), group));
        });
}

//CookingRecipeCodec 烹饪配方编解码 对应原版 AbstractCookingRecipe.cookingMapCodec
//JSON 形态 {"type":"minecraft:smelting","ingredient":..,"result":..,"experience":0.1,"cookingtime":200}
//熔炼四件套字段完全一致 只有默认烹饪时长与配方类型不同 故共用一个解析器按 type 建不同子类
internal sealed class CookingRecipeCodec
{
    public static readonly CookingRecipeCodec Instance = new();

    //DefaultCookingTime 各类型的默认烹饪时长 对应原版四个 MAP_CODEC 传入的 defaultCookingTime
    public static int DefaultCookingTime(string type) => type switch
    {
        SmeltingRecipe.SerializerId => SmeltingRecipe.DefaultCookingTime,
        BlastingRecipe.SerializerId => BlastingRecipe.DefaultCookingTime,
        SmokingRecipe.SerializerId => SmokingRecipe.DefaultCookingTime,
        _ => CampfireCookingRecipe.DefaultCookingTime,
    };

    public DataResult<AbstractCookingRecipe> Parse<U>(DynamicOps<U> ops, U input, string type)
        => ops.GetMap(input).FlatMap(map =>
        {
            var ingredientField = map.Get("ingredient");
            if (!ingredientField.IsPresent)
                return DataResult<AbstractCookingRecipe>.Error(() => "烹饪配方缺 ingredient 字段");
            var ingredient = Ingredient.Codec.Parse(ops, ingredientField.Get());
            if (!ingredient.Result().IsPresent)
                return DataResult<AbstractCookingRecipe>.Error(
                    () => $"配方 ingredient 不合法: {ingredient.Result().ToString()}");
            var result = RecipeCodec.ParseResult(ops, map);
            if (!result.Result().IsPresent)
                return DataResult<AbstractCookingRecipe>.Error(
                    () => $"配方 result 不合法: {result.Result().ToString()}");
            var group = RecipeCodec.ReadString(ops, map, "group", string.Empty);
            var category = RecipeCodec.ReadString(ops, map, "category", "misc");
            var experience = RecipeCodec.ReadFloat(ops, map, "experience", 0f);
            var cookingTime = RecipeCodec.ReadInt(ops, map, "cookingtime", DefaultCookingTime(type));
            //原版用 Codec.intRange(1, max) 卡范围 0 或负数会让熔炉永远烧不出东西
            if (cookingTime <= 0)
                return DataResult<AbstractCookingRecipe>.Error(() => $"配方 cookingtime 必须为正: {cookingTime}");
            return DataResult<AbstractCookingRecipe>.Success(Create(type, ingredient.GetOrThrow(),
                result.GetOrThrow(), group, category, experience, cookingTime));
        });

    //Create 按类型建对应子类
    private static AbstractCookingRecipe Create(string type, Ingredient ingredient, ItemStack result,
        string group, string category, float experience, int cookingTime) => type switch
        {
            SmeltingRecipe.SerializerId => new SmeltingRecipe(ingredient, result, group, category, experience, cookingTime),
            BlastingRecipe.SerializerId => new BlastingRecipe(ingredient, result, group, category, experience, cookingTime),
            SmokingRecipe.SerializerId => new SmokingRecipe(ingredient, result, group, category, experience, cookingTime),
            _ => new CampfireCookingRecipe(ingredient, result, group, category, experience, cookingTime),
        };
}

//ResultStackCodec 成品栈编解码 对应原版 ItemStackTemplate.CODEC
//JSON 形态是 {"id":"minecraft:stick","count":4} count 省略为 1 组件字段本作暂不解析
//也接受直接写物品 id 字符串的简写
internal sealed class ResultStackCodec : ScalarCodec<ItemStack>
{
    public static readonly ResultStackCodec Instance = new();

    //MaxCount 原版 ItemStackTemplate 的 count 上限
    private const int MaxCount = 99;

    public override DataResult<ItemStack> Parse<U>(DynamicOps<U> ops, U input)
    {
        //简写: 直接给物品 id 字符串
        var textResult = ops.GetStringValue(input);
        if (textResult.Result().IsPresent) return Build(textResult.GetOrThrow(), 1);

        return ops.GetMap(input).FlatMap(map =>
        {
            var idField = map.Get("id");
            if (!idField.IsPresent)
                return DataResult<ItemStack>.Error(() => "配方 result 缺 id 字段");
            var idResult = ops.GetStringValue(idField.Get());
            if (!idResult.Result().IsPresent)
                return DataResult<ItemStack>.Error(() => "配方 result 的 id 必须是字符串");
            var count = 1;
            var countField = map.Get("count");
            if (countField.IsPresent)
            {
                var countResult = ops.GetNumberValue(countField.Get());
                if (!countResult.Result().IsPresent)
                    return DataResult<ItemStack>.Error(() => "配方 result 的 count 必须是数字");
                count = (int)countResult.GetOrThrow();
            }
            return Build(idResult.GetOrThrow(), count);
        });
    }

    //Build 按 id 与数量建成品栈 数量越界或物品不存在都算错
    private static DataResult<ItemStack> Build(string itemId, int count)
    {
        if (count < 1 || count > MaxCount)
            return DataResult<ItemStack>.Error(() => $"配方 result 数量越界 1..{MaxCount}: {count}");
        var id = Identifier.TryParse(itemId);
        if (id is null) return DataResult<ItemStack>.Error(() => $"配方 result 的物品 id 不合法: {itemId}");
        if (BuiltInRegistries.ITEM.GetValue(id.Value) is not { } item)
            return DataResult<ItemStack>.Error(() => $"配方 result 的物品不存在: {itemId}");
        if (ReferenceEquals(item, NetCraft.Game.World.Items.Items.AIR))
            return DataResult<ItemStack>.Error(() => "配方 result 不能是空气");
        return DataResult<ItemStack>.Success(
            new ItemStack(item.BuiltInRegistryHolder, count, DataComponentPatch.Empty));
    }
}
