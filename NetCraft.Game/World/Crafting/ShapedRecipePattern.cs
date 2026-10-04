using NetCraft.Codec;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//ShapedRecipePattern 有序配方图案对应原版 ShapedRecipePattern
//把 key/pattern 展开成宽高加原料表 空格记 null 匹配时按最小包围盒比对 必要时试一次左右翻转
public sealed class ShapedRecipePattern
{
    //MaxSize 图案最大边长 对应原版 MAX_SIZE
    public const int MaxSize = 3;

    //EmptySlot 图案里的空格 对应原版 EMPTY_SLOT
    public const char EmptySlot = ' ';

    //Codec 图案 JSON 形态 形如 {"key":{"A":"minecraft:stick"},"pattern":["A ","AA"]}
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

    //Width/Height 图案宽高
    public int Width { get; }
    public int Height { get; }

    //Ingredients 线性排列的原料 null 表示该格必须为空
    public IReadOnlyList<Ingredient?> Ingredients => _ingredients;

    //Matches 输入网格是否命中该图案 对应原版 matches
    //先比非空格数再比宽高 最后逐格比原料 左右对称的图案不必试翻转
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

    //TestSlot 空位要求输入为空 非空位要求原料命中
    private static bool TestSlot(Ingredient? ingredient, ItemStack stack)
        => ingredient is null ? stack.IsEmpty() : ingredient.Matches(stack);

    //IsSymmetrical 图案左右翻转后是否一样 一样就没必要再试翻转 对应原版 Util.isSymmetrical
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

    //Unpack 把 key 与 pattern 展开成图案 对应原版 ShapedRecipePattern.unpack
    //图案先裁掉四周空行空列 key 里定义了却没被图案用到的符号视为错误
    public static NetCraft.Codec.DataResult<ShapedRecipePattern> Unpack(PatternData data)
    {
        var shrunk = Shrink(data.Pattern);
        if (shrunk.Length == 0)
            return NetCraft.Codec.DataResult<ShapedRecipePattern>.Error(() => "配方图案不合法: 图案全是空行");
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
                        () => $"配方图案用到符号 '{symbol}' 但 key 里没有定义");
                ingredients.Add(ingredient);
                unusedSymbols.Remove(symbol);
            }
        if (unusedSymbols.Count > 0)
            return NetCraft.Codec.DataResult<ShapedRecipePattern>.Error(
                () => $"配方 key 定义了图案没用到的符号: {string.Join(",", unusedSymbols)}");
        return NetCraft.Codec.DataResult<ShapedRecipePattern>.Success(new ShapedRecipePattern(width, height, ingredients));
    }

    //Shrink 裁掉图案四周的空行空列 对应原版 ShapedRecipePattern.shrink
    //全空行不参与包围盒 顶部与底部的空行整体去掉 左右按最宽范围对齐
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

    //PatternData 图案原始数据对应原版 ShapedRecipePattern.Data
    public sealed record PatternData(IReadOnlyDictionary<char, Ingredient> Key, IReadOnlyList<string> Pattern);
}

//ShapedRecipePatternCodec 图案编解码对应原版 ShapedRecipePattern.MAP_CODEC
//字段 key 是符号到原料的对象 pattern 是字符串数组 校验规则与 Data.PATTERN_CODEC 一致
internal sealed class ShapedRecipePatternCodec : ScalarCodec<ShapedRecipePattern>
{
    public override DataResult<ShapedRecipePattern> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => Decode(ops, map));

    private static DataResult<ShapedRecipePattern> Decode<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var keyField = map.Get("key");
        if (!keyField.IsPresent)
            return DataResult<ShapedRecipePattern>.Error(() => "配方缺 key 字段");
        var patternField = map.Get("pattern");
        if (!patternField.IsPresent)
            return DataResult<ShapedRecipePattern>.Error(() => "配方缺 pattern 字段");

        return ParseKey(ops, keyField.Get()).FlatMap(key =>
            ParsePattern(ops, patternField.Get()).FlatMap(pattern =>
                ShapedRecipePattern.Unpack(new ShapedRecipePattern.PatternData(key, pattern))));
    }

    //ParseKey 解析符号到原料的映射 符号必须是单字符且不能用空格
    private static DataResult<IReadOnlyDictionary<char, Ingredient>> ParseKey<U>(DynamicOps<U> ops, U node)
    {
        var values = ops.GetMapValues(node);
        if (!values.Result().IsPresent)
            return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(() => "配方 key 必须是对象");
        var key = new Dictionary<char, Ingredient>();
        foreach (var pair in values.GetOrThrow())
        {
            var symbolResult = ops.GetStringValue(pair.First);
            if (!symbolResult.Result().IsPresent)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(() => "配方 key 的符号必须是字符串");
            var symbol = symbolResult.GetOrThrow();
            if (symbol.Length != 1)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(
                    () => $"配方 key 的符号必须只有一个字符: '{symbol}'");
            if (symbol[0] == ShapedRecipePattern.EmptySlot)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(() => "配方 key 不能用空格作符号");
            var ingredientResult = Ingredient.Codec.Parse(ops, pair.Second);
            if (!ingredientResult.Result().IsPresent)
                return DataResult<IReadOnlyDictionary<char, Ingredient>>.Error(
                    () => $"配方 key 的符号 '{symbol}' 原料不合法: {ingredientResult.Result().ToString()}");
            key[symbol[0]] = ingredientResult.GetOrThrow();
        }
        return DataResult<IReadOnlyDictionary<char, Ingredient>>.Success(key);
    }

    //ParsePattern 解析图案数组 最多 3 行 每行等宽且不超过 3 列
    private static DataResult<IReadOnlyList<string>> ParsePattern<U>(DynamicOps<U> ops, U node)
    {
        var stream = ops.GetStream(node);
        if (!stream.Result().IsPresent)
            return DataResult<IReadOnlyList<string>>.Error(() => "配方 pattern 必须是字符串数组");
        var lines = new List<string>();
        foreach (var element in stream.GetOrThrow())
        {
            var lineResult = ops.GetStringValue(element);
            if (!lineResult.Result().IsPresent)
                return DataResult<IReadOnlyList<string>>.Error(() => "配方 pattern 的每一项都必须是字符串");
            lines.Add(lineResult.GetOrThrow());
        }
        if (lines.Count == 0) return DataResult<IReadOnlyList<string>>.Error(() => "配方 pattern 不能为空");
        if (lines.Count > ShapedRecipePattern.MaxSize)
            return DataResult<IReadOnlyList<string>>.Error(
                () => $"配方 pattern 最多 {ShapedRecipePattern.MaxSize} 行");
        var firstLength = lines[0].Length;
        foreach (var line in lines)
        {
            if (line.Length > ShapedRecipePattern.MaxSize)
                return DataResult<IReadOnlyList<string>>.Error(
                    () => $"配方 pattern 每行最多 {ShapedRecipePattern.MaxSize} 列");
            if (line.Length != firstLength)
                return DataResult<IReadOnlyList<string>>.Error(() => "配方 pattern 每行必须等宽");
        }
        return DataResult<IReadOnlyList<string>>.Success(lines);
    }
}
