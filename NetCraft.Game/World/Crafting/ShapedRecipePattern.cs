using NetCraft.Codec;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//ShapedRecipePattern shaped recipe pattern, maps to vanilla ShapedRecipePattern
//Expands key/pattern into width, height and an ingredient table, spaces record null; matching compares against the minimal bounding box and tries one horizontal mirror when needed
public sealed class ShapedRecipePattern
{
    //MaxSize maximum pattern side length, maps to vanilla MAX_SIZE
    public const int MaxSize = 3;

    //EmptySlot a space in the pattern, maps to vanilla EMPTY_SLOT
    public const char EmptySlot = ' ';

    //Codec pattern JSON form, like {"key":{"A":"minecraft:stick"},"pattern":["A ","AA"]}
    public static readonly Codec<ShapedRecipePattern> Codec = new ShapedRecipePatternCodec();

    private readonly Ingredient?[] _ingredients;
    private readonly int _ingredientCount;
    private readonly bool _symmetrical;

    public ShapedRecipePattern(int width, int height, IReadOnlyList<Ingredient?> ingredients)
    {
        Width = width;
        Height = height;
        _ingredients = new Ingredient?[ingredients.Count];
        var count = 0;
        for (var i = 0; i < ingredients.Count; i++)
        {
            _ingredients[i] = ingredients[i];
            if (ingredients[i] is not null) count++;
        }
        _ingredientCount = count;
        _symmetrical = IsSymmetrical(width, height, _ingredients);
    }

    //Width/Height pattern width and height
    public int Width { get; }
    public int Height { get; }

    //Ingredients linearly arranged ingredients, null means the slot must be empty
    public IReadOnlyList<Ingredient?> Ingredients => _ingredients;

    //Matches whether the input grid hits this pattern, maps to vanilla matches
    //Compares the non-empty slot count then the width and height, finally the ingredients slot by slot; symmetric patterns skip the mirror attempt
    public bool Matches(CraftingInput input)
    {
        if (input.IngredientCount != _ingredientCount) return false;
        if (input.Width != Width || input.Height != Height) return false;
        if (!_symmetrical && Matches(input, true)) return true;
        return Matches(input, false);
    }

    private bool Matches(CraftingInput input, bool flipX)
    {
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var expected = flipX
                    ? _ingredients[Width - x - 1 + y * Width]
                    : _ingredients[x + y * Width];
                if (!TestSlot(expected, input.GetItem(x, y))) return false;
            }
        return true;
    }

    //TestSlot an empty position requires the input to be empty, a filled one requires the ingredient to match
    private static bool TestSlot(Ingredient? ingredient, ItemStack stack)
        => ingredient is null ? stack.IsEmpty() : ingredient.Matches(stack);

    //IsSymmetrical whether the pattern is the same after a horizontal mirror; if so the mirror attempt is unnecessary, maps to vanilla Util.isSymmetrical
    private static bool IsSymmetrical(int width, int height, IReadOnlyList<Ingredient?> ingredients)
    {
        if (width == 1) return true;
        var half = width / 2;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < half; x++)
            {
                var left = ingredients[x + y * width];
                var right = ingredients[width - x - 1 + y * width];
                if (!Equals(left, right)) return false;
            }
        return true;
    }

    //Unpack expands key and pattern into the pattern, maps to vanilla ShapedRecipePattern.unpack
    //The pattern is first trimmed of empty rows and columns; symbols defined in key but unused by the pattern are an error
    public static NetCraft.Codec.DataResult<ShapedRecipePattern> Unpack(PatternData data)
    {
        var shrunk = Shrink(data.Pattern);
        if (shrunk.Length == 0)
            return NetCraft.Codec.DataResult<ShapedRecipePattern>.Error(() => "invalid recipe pattern: the pattern is all empty rows");
        var width = shrunk[0].Length;
        var height = shrunk.Length;
        var ingredients = new List<Ingredient?>(width * height);
        var unusedSymbols = new HashSet<char>(data.Key.Keys);
        foreach (var line in shrunk)
            for (var x = 0; x < line.Length; x++)
            {
                var symbol = line[x];
                if (symbol == EmptySlot)
                {
                    ingredients.Add(null);
                    continue;
                }
                if (!data.Key.TryGetValue(symbol, out var ingredient))
                    return NetCraft.Codec.DataResult<ShapedRecipePattern>.Error(
                        () => $"recipe pattern uses symbol '{symbol}' which is not defined in key");
                ingredients.Add(ingredient);
                unusedSymbols.Remove(symbol);
            }
        if (unusedSymbols.Count > 0)
            return NetCraft.Codec.DataResult<ShapedRecipePattern>.Error(
                () => $"recipe key defines symbols unused by the pattern: {string.Join(",", unusedSymbols)}");
        return NetCraft.Codec.DataResult<ShapedRecipePattern>.Success(new ShapedRecipePattern(width, height, ingredients));
    }

    //Shrink trims the empty rows and columns around the pattern, maps to vanilla ShapedRecipePattern.shrink
    //All-empty rows do not participate in the bounding box; leading and trailing empty rows are dropped and the sides align to the widest range
    public static string[] Shrink(IReadOnlyList<string> pattern)
    {
        var left = int.MaxValue;
        var right = 0;
        var top = 0;
        var bottom = 0;
        for (var i = 0; i < pattern.Count; i++)
        {
            var line = pattern[i];
            left = Math.Min(left, FirstNonEmpty(line));
            var lastNonEmpty = LastNonEmpty(line);
            right = Math.Max(right, lastNonEmpty);
            if (lastNonEmpty < 0)
            {
                if (top == i) top++;
                bottom++;
                continue;
            }
            bottom = 0;
        }
        if (pattern.Count == bottom) return Array.Empty<string>();
        var result = new string[pattern.Count - bottom - top];
        for (var line = 0; line < result.Length; line++)
            result[line] = pattern[line + top].Substring(left, right - left + 1);
        return result;
    }

    private static int FirstNonEmpty(string line)
    {
        var index = 0;
        while (index < line.Length && line[index] == EmptySlot) index++;
        return index;
    }

    private static int LastNonEmpty(string line)
    {
        var index = line.Length - 1;
        while (index >= 0 && line[index] == EmptySlot) index--;
        return index;
    }

    //PatternData raw pattern data, maps to vanilla ShapedRecipePattern.Data
    public sealed record PatternData(IReadOnlyDictionary<char, Ingredient> Key, IReadOnlyList<string> Pattern);
}

//ShapedRecipePatternCodec pattern codec, maps to vanilla ShapedRecipePattern.MAP_CODEC
//The key field is an object from symbol to ingredient and pattern is a string array, validation matches Data.PATTERN_CODEC
internal sealed class ShapedRecipePatternCodec : ScalarCodec<ShapedRecipePattern>
{
    public override DataResult<ShapedRecipePattern> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => Decode(ops, map));

    private static DataResult<ShapedRecipePattern> Decode<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var keyField = map.Get("key");
        if (!keyField.IsPresent)
            return DataResult<ShapedRecipePattern>.Error(() => "recipe is missing the key field");
        var patternField = map.Get("pattern");
        if (!patternField.IsPresent)
            return DataResult<ShapedRecipePattern>.Error(() => "recipe is missing the pattern field");

        return ParseKey(ops, keyField.Get()).FlatMap(key =>
            ParsePattern(ops, patternField.Get()).FlatMap(pattern =>
                ShapedRecipePattern.Unpack(new ShapedRecipePattern.PatternData(key, pattern))));
    }

    //ParseKey parses the symbol-to-ingredient map, symbols must be a single character and cannot be a space
    private static DataResult<IReadOnlyDictionary<char, Ingredient>> ParseKey<U>(DynamicOps<U> ops, U node)
    {
        var values = ops.GetMapValues(node);
        if (!values.Result().IsPresent)
            return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(() => "recipe key must be an object");
        var key = new Dictionary<char, Ingredient>();
        foreach (var pair in values.GetOrThrow())
        {
            var symbolResult = ops.GetStringValue(pair.First);
            if (!symbolResult.Result().IsPresent)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(() => "recipe key symbols must be strings");
            var symbol = symbolResult.GetOrThrow();
            if (symbol.Length != 1)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(
                    () => $"a recipe key symbol must be exactly one character: '{symbol}'");
            if (symbol[0] == ShapedRecipePattern.EmptySlot)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(() => "recipe key cannot use a space as a symbol");
            var ingredientResult = Ingredient.Codec.Parse(ops, pair.Second);
            if (!ingredientResult.Result().IsPresent)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(
                    () => $"invalid ingredient for recipe key symbol '{symbol}': {ingredientResult.Result().ToString()}");
            key[symbol[0]] = ingredientResult.GetOrThrow();
        }
        return DataResult<IReadOnlyDictionary<char, Ingredient>>.Success(key);
    }

    //ParsePattern parses the pattern array, at most 3 rows, each of equal width and no more than 3 columns
    private static DataResult<IReadOnlyList<string>> ParsePattern<U>(DynamicOps<U> ops, U node)
    {
        var stream = ops.GetStream(node);
        if (!stream.Result().IsPresent)
            return DataResult<IReadOnlyList<string>>.Error(() => "recipe pattern must be a string array");
        var lines = new List<string>();
        foreach (var element in stream.GetOrThrow())
        {
            var lineResult = ops.GetStringValue(element);
            if (!lineResult.Result().IsPresent)
                return DataResult<IReadOnlyList<string>>.Error(() => "every recipe pattern entry must be a string");
            lines.Add(lineResult.GetOrThrow());
        }
        if (lines.Count == 0) return DataResult<IReadOnlyList<string>>.Error(() => "recipe pattern cannot be empty");
        if (lines.Count > ShapedRecipePattern.MaxSize)
            return DataResult<IReadOnlyList<string>>.Error(
                () => $"recipe pattern has at most {ShapedRecipePattern.MaxSize} rows");
        var firstLength = lines[0].Length;
        foreach (var line in lines)
        {
            if (line.Length > ShapedRecipePattern.MaxSize)
                return DataResult<IReadOnlyList<string>>.Error(
                    () => $"each recipe pattern row has at most {ShapedRecipePattern.MaxSize} columns");
            if (line.Length != firstLength)
                return DataResult<IReadOnlyList<string>>.Error(() => "every recipe pattern row must have the same width");
        }
        return DataResult<IReadOnlyList<string>>.Success(lines);
    }
}
