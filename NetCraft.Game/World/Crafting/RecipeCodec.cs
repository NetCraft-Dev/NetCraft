using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Network.Component;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Crafting;

//RecipeCodec crafting recipe codec, maps to the dispatch-by-RECIPE_SERIALIZER semantics of vanilla Recipe.CODEC
//The codec layer here has no dispatch combinator, so this is hand-written following the existing business-layer approach: read type first then hand off to the matching sub-parser
internal sealed class RecipeCodec : ScalarCodec<Recipe<CraftingInput>>
{
    public static readonly RecipeCodec Instance = new();

    public override DataResult<Recipe<CraftingInput>> Parse<U>(DynamicOps<U> ops, U input)
        => ReadType(ops, input).FlatMap(type => ParseBody(ops, input, type));

    //ReadType reads the type field and strips the namespace so the recipe manager can dispatch a parser by type
    internal static DataResult<string> ReadType<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var typeField = map.Get("type");
            if (!typeField.IsPresent)
                return DataResult<string>.Error(() => "recipe is missing the type field");
            var typeResult = ops.GetStringValue(typeField.Get());
            if (!typeResult.Result().IsPresent)
                return DataResult<string>.Error(() => "recipe type must be a string");
            return DataResult<string>.Success(StripNamespace(typeResult.GetOrThrow()));
        });

    //ParseBody parses the recipe body by type once the type is known
    private static DataResult<Recipe<CraftingInput>> ParseBody<U>(DynamicOps<U> ops, U input, string type)
        => ops.GetMap(input).FlatMap(map =>
        {
            var group = ReadString(ops, map, "group", string.Empty);
            var category = ReadString(ops, map, "category", "misc");
            var showNotification = ReadBool(ops, map, "show_notification", true);

            //Sub-parsers take the raw node, the pattern and result live in the same level of the map
            return type switch
            {
                ShapedRecipe.SerializerId => ParseShaped(ops, input, map, group, category, showNotification),
                ShapelessRecipe.SerializerId => ParseShapeless(ops, input, map, group, category, showNotification),
                _ => DataResult<Recipe<CraftingInput>>.Error(() => $"unsupported recipe type {type}"),
            };
        });

    //ParseShaped parses crafting_shaped
    private static DataResult<Recipe<CraftingInput>> ParseShaped<U>(DynamicOps<U> ops, U input, MapLike<U> map,
        string group, string category, bool showNotification)
    {
        var result = ParseResult(ops, map);
        if (!result.Result().IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(
                () => $"invalid recipe result: {result.Result().ToString()}");
        return ShapedRecipePattern.Codec.Parse(ops, input).FlatMap(pattern =>
            DataResult<Recipe<CraftingInput>>.Success(
                new ShapedRecipe(pattern, result.GetOrThrow(), group, category, showNotification)));
    }

    //ParseShapeless parses crafting_shapeless
    private static DataResult<Recipe<CraftingInput>> ParseShapeless<U>(DynamicOps<U> ops, U input, MapLike<U> map,
        string group, string category, bool showNotification)
    {
        var ingredientsField = map.Get("ingredients");
        if (!ingredientsField.IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(() => "shapeless recipe is missing the ingredients field");
        var result = ParseResult(ops, map);
        if (!result.Result().IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(
                () => $"invalid recipe result: {result.Result().ToString()}");

        var stream = ops.GetStream(ingredientsField.Get());
        if (!stream.Result().IsPresent)
            return DataResult<Recipe<CraftingInput>>.Error(() => "recipe ingredients must be an array");
        var ingredients = new List<Ingredient>();
        foreach (var element in stream.GetOrThrow())
        {
            var parsed = Ingredient.Codec.Parse(ops, element);
            if (!parsed.Result().IsPresent)
                return DataResult<Recipe<CraftingInput>>.Error(
                    () => $"recipe ingredients contain an invalid entry: {parsed.Result().ToString()}");
            ingredients.Add(parsed.GetOrThrow());
        }
        if (ingredients.Count == 0)
            return DataResult<Recipe<CraftingInput>>.Error(() => "shapeless recipe ingredients cannot be empty");
        return DataResult<Recipe<CraftingInput>>.Success(
            new ShapelessRecipe(ingredients, result.GetOrThrow(), group, category, showNotification));
    }

    //ParseResult parses the result field, maps to vanilla ItemStackTemplate.CODEC
    internal static DataResult<ItemStack> ParseResult<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var resultField = map.Get("result");
        return resultField.IsPresent
            ? ResultStackCodec.Instance.Parse(ops, resultField.Get())
            : DataResult<ItemStack>.Error(() => "recipe is missing the result field");
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

//StonecutterRecipeCodec stonecutter recipe codec, maps to vanilla SingleItemRecipe.simpleMapCodec
//JSON form {"type":"minecraft:stonecutting","ingredient":..,"result":..}, group is optional
internal sealed class StonecutterRecipeCodec : ScalarCodec<StonecutterRecipe>
{
    public static readonly StonecutterRecipeCodec Instance = new();

    public override DataResult<StonecutterRecipe> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var ingredientField = map.Get("ingredient");
            if (!ingredientField.IsPresent)
                return DataResult<StonecutterRecipe>.Error(() => "stonecutter recipe is missing the ingredient field");
            var ingredient = Ingredient.Codec.Parse(ops, ingredientField.Get());
            if (!ingredient.Result().IsPresent)
                return DataResult<StonecutterRecipe>.Error(
                    () => $"invalid recipe ingredient: {ingredient.Result().ToString()}");
            var result = RecipeCodec.ParseResult(ops, map);
            if (!result.Result().IsPresent)
                return DataResult<StonecutterRecipe>.Error(
                    () => $"invalid recipe result: {result.Result().ToString()}");
            var group = RecipeCodec.ReadString(ops, map, "group", string.Empty);
            return DataResult<StonecutterRecipe>.Success(
                new StonecutterRecipe(ingredient.GetOrThrow(), result.GetOrThrow(), group));
        });
}

//CookingRecipeCodec cooking recipe codec, maps to vanilla AbstractCookingRecipe.cookingMapCodec
//JSON form {"type":"minecraft:smelting","ingredient":..,"result":..,"experience":0.1,"cookingtime":200}
//The four cooking variants have identical fields, differing only in default cooking time and recipe type, so one parser builds different subclasses by type
internal sealed class CookingRecipeCodec
{
    public static readonly CookingRecipeCodec Instance = new();

    //DefaultCookingTime default cooking time per type, maps to the defaultCookingTime passed by the four vanilla MAP_CODECs
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
                return DataResult<AbstractCookingRecipe>.Error(() => "cooking recipe is missing the ingredient field");
            var ingredient = Ingredient.Codec.Parse(ops, ingredientField.Get());
            if (!ingredient.Result().IsPresent)
                return DataResult<AbstractCookingRecipe>.Error(
                    () => $"invalid recipe ingredient: {ingredient.Result().ToString()}");
            var result = RecipeCodec.ParseResult(ops, map);
            if (!result.Result().IsPresent)
                return DataResult<AbstractCookingRecipe>.Error(
                    () => $"invalid recipe result: {result.Result().ToString()}");
            var group = RecipeCodec.ReadString(ops, map, "group", string.Empty);
            var category = RecipeCodec.ReadString(ops, map, "category", "misc");
            var experience = RecipeCodec.ReadFloat(ops, map, "experience", 0f);
            var cookingTime = RecipeCodec.ReadInt(ops, map, "cookingtime", DefaultCookingTime(type));
            //Vanilla clamps with Codec.intRange(1, max); 0 or negative would make the furnace never produce anything
            if (cookingTime <= 0)
                return DataResult<AbstractCookingRecipe>.Error(() => $"recipe cookingtime must be positive: {cookingTime}");
            return DataResult<AbstractCookingRecipe>.Success(Create(type, ingredient.GetOrThrow(),
                result.GetOrThrow(), group, category, experience, cookingTime));
        });

    //Create builds the matching subclass by type
    private static AbstractCookingRecipe Create(string type, Ingredient ingredient, ItemStack result,
        string group, string category, float experience, int cookingTime) => type switch
        {
            SmeltingRecipe.SerializerId => new SmeltingRecipe(ingredient, result, group, category, experience, cookingTime),
            BlastingRecipe.SerializerId => new BlastingRecipe(ingredient, result, group, category, experience, cookingTime),
            SmokingRecipe.SerializerId => new SmokingRecipe(ingredient, result, group, category, experience, cookingTime),
            _ => new CampfireCookingRecipe(ingredient, result, group, category, experience, cookingTime),
        };
}

//ResultStackCodec result stack codec, maps to vanilla ItemStackTemplate.CODEC
//JSON form is {"id":"minecraft:stick","count":4}, count defaults to 1 and component fields are not parsed yet
//Also accepts the shorthand of writing the item id string directly
internal sealed class ResultStackCodec : ScalarCodec<ItemStack>
{
    public static readonly ResultStackCodec Instance = new();

    //MaxCount count limit of vanilla ItemStackTemplate
    private const int MaxCount = 99;

    public override DataResult<ItemStack> Parse<U>(DynamicOps<U> ops, U input)
    {
        //Shorthand: give the item id string directly
        var textResult = ops.GetStringValue(input);
        if (textResult.Result().IsPresent) return Build(textResult.GetOrThrow(), 1);

        return ops.GetMap(input).FlatMap(map =>
        {
            var idField = map.Get("id");
            if (!idField.IsPresent)
                return DataResult<ItemStack>.Error(() => "recipe result is missing the id field");
            var idResult = ops.GetStringValue(idField.Get());
            if (!idResult.Result().IsPresent)
                return DataResult<ItemStack>.Error(() => "recipe result id must be a string");
            var count = 1;
            var countField = map.Get("count");
            if (countField.IsPresent)
            {
                var countResult = ops.GetNumberValue(countField.Get());
                if (!countResult.Result().IsPresent)
                    return DataResult<ItemStack>.Error(() => "recipe result count must be a number");
                count = (int)countResult.GetOrThrow();
            }
            return Build(idResult.GetOrThrow(), count);
        });
    }

    //Build builds the result stack from id and count, an out-of-range count or a missing item is an error
    private static DataResult<ItemStack> Build(string itemId, int count)
    {
        if (count < 1 || count > MaxCount)
            return DataResult<ItemStack>.Error(() => $"recipe result count out of range 1..{MaxCount}: {count}");
        var id = Identifier.TryParse(itemId);
        if (id is null) return DataResult<ItemStack>.Error(() => $"invalid recipe result item id: {itemId}");
        if (BuiltInRegistries.ITEM.GetValue(id.Value) is not { } item)
            return DataResult<ItemStack>.Error(() => $"recipe result item does not exist: {itemId}");
        if (ReferenceEquals(item, NetCraft.Game.World.Items.Items.AIR))
            return DataResult<ItemStack>.Error(() => "recipe result cannot be air");
        return DataResult<ItemStack>.Success(
            new ItemStack(item.BuiltInRegistryHolder, count, DataComponentPatch.Empty));
    }
}
