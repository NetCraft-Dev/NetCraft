using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//RecipeInput 配方输入抽象对应原版 net.minecraft.world.item.crafting.RecipeInput
public interface RecipeInput
{
    //Size 输入格总数
    int Size { get; }

    //IsEmpty 输入是否全空
    bool IsEmpty { get; }

    //GetItem 按线性下标取输入物品
    ItemStack GetItem(int index);
}

//SingleRecipeInput 单格配方输入对应原版 net.minecraft.world.item.crafting.SingleRecipeInput
//切石机与熔炼这类只有一个输入槽的配方用它
public sealed class SingleRecipeInput : RecipeInput
{
    private readonly ItemStack _item;

    public SingleRecipeInput(ItemStack item) => _item = item;

    public int Size => 1;

    public bool IsEmpty => _item.IsEmpty();

    public ItemStack GetItem(int index) => index == 0 ? _item : ItemStack.Empty;
}

//CraftingInput 合成网格输入对应原版 CraftingInput
//记录宽高与线性排列的物品 非空格数在构造时算好供配方快速淘汰
public sealed class CraftingInput : RecipeInput
{
    //Empty 空输入
    public static readonly CraftingInput Empty = new(0, 0, Array.Empty<ItemStack>());

    private readonly ItemStack[] _items;

    public CraftingInput(int width, int height, IReadOnlyList<ItemStack> items)
    {
        Width = width;
        Height = height;
        _items = new ItemStack[items.Count];
        var ingredientCount = 0;
        for (var i = 0; i < items.Count; i++)
        {
            _items[i] = items[i];
            if (!items[i].IsEmpty()) ingredientCount++;
        }
        IngredientCount = ingredientCount;
    }

    //Width/Height 网格宽高
    public int Width { get; }
    public int Height { get; }

    //IngredientCount 非空格数量 形状配方先比它能直接淘汰
    public int IngredientCount { get; }

    public int Size => _items.Length;

    public bool IsEmpty => IngredientCount == 0;

    public ItemStack GetItem(int index) => (uint)index < _items.Length ? _items[index] : ItemStack.Empty;

    //GetItem 按网格坐标取输入物品
    public ItemStack GetItem(int x, int y) => GetItem(x + y * Width);

    //Of 建输入并把四周空行空列裁掉 对应原版 CraftingInput.of 的 ofPositioned 语义
    //形状配方的图案是最小包围盒 网格里摆歪或留空必须裁到包围盒才比得上
    public static CraftingInput Of(int width, int height, IReadOnlyList<ItemStack> items)
    {
        if (width == 0 || height == 0) return Empty;
        var left = width - 1;
        var right = 0;
        var top = height - 1;
        var bottom = 0;
        for (var y = 0; y < height; y++)
        {
            var rowEmpty = true;
            for (var x = 0; x < width; x++)
            {
                if (items[x + y * width].IsEmpty()) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                rowEmpty = false;
            }
            if (rowEmpty) continue;
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }
        var newWidth = right - left + 1;
        var newHeight = bottom - top + 1;
        if (newWidth <= 0 || newHeight <= 0) return Empty;
        if (newWidth == width && newHeight == height) return new CraftingInput(width, height, items);

        var trimmed = new ItemStack[newWidth * newHeight];
        for (var y = 0; y < newHeight; y++)
            for (var x = 0; x < newWidth; x++)
                trimmed[x + y * newWidth] = items[x + left + (y + top) * width];
        return new CraftingInput(newWidth, newHeight, trimmed);
    }
}
