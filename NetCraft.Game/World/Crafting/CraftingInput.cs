using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//RecipeInput recipe input abstraction, maps to vanilla net.minecraft.world.item.crafting.RecipeInput
public interface RecipeInput
{
    //Size total number of input slots
    int Size { get; }

    //IsEmpty whether all inputs are empty
    bool IsEmpty { get; }

    //GetItem returns the input item by linear index
    ItemStack GetItem(int index);
}

//SingleRecipeInput single-slot recipe input, maps to vanilla net.minecraft.world.item.crafting.SingleRecipeInput
//Used by recipes with a single input slot such as stonecutting and cooking
public sealed class SingleRecipeInput : RecipeInput
{
    private readonly ItemStack _item;

    public SingleRecipeInput(ItemStack item) => _item = item;

    public int Size => 1;

    public bool IsEmpty => _item.IsEmpty();

    public ItemStack GetItem(int index) => index == 0 ? _item : ItemStack.Empty;
}

//CraftingInput crafting grid input, maps to vanilla CraftingInput
//Stores the width, height and linearly arranged items; the non-empty count is computed at construction so recipes can be eliminated quickly
public sealed class CraftingInput : RecipeInput
{
    //Empty empty input
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

    //Width/Height grid width and height
    public int Width { get; }
    public int Height { get; }

    //IngredientCount number of non-empty slots, shape recipes compare it first to eliminate quickly
    public int IngredientCount { get; }

    public int Size => _items.Length;

    public bool IsEmpty => IngredientCount == 0;

    public ItemStack GetItem(int index) => (uint)index < _items.Length ? _items[index] : ItemStack.Empty;

    //GetItem returns the input item by grid coordinates
    public ItemStack GetItem(int x, int y) => GetItem(x + y * Width);

    //Of builds the input and trims the empty rows and columns around it, maps to the ofPositioned semantics of vanilla CraftingInput.of
    //Shape recipes are defined as a minimal bounding box, so a grid placed off-center or with padding must be trimmed to the box to compare
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
