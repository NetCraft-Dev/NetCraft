using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//ShapelessRecipe 无序合成配方对应原版 ShapelessRecipe
//只要求非空格数与原料数相同 每个原料都能在输入里找到还没被占用的匹配项
public sealed class ShapelessRecipe : CraftingRecipe
{
    //SerializerId 配方序列化 id 即 JSON 里的 type
    public const string SerializerId = "crafting_shapeless";

    private readonly List<Ingredient> _ingredients;
    private readonly ItemStack _result;

    public ShapelessRecipe(IReadOnlyList<Ingredient> ingredients, ItemStack result,
        string group, string category, bool showNotification)
        : base(group, category, showNotification)
    {
        _ingredients = new List<Ingredient>(ingredients);
        _result = result;
    }

    //Ingredients 原料表
    public IReadOnlyList<Ingredient> Ingredients => _ingredients;

    public override string Type => SerializerId;

    public override bool Matches(CraftingInput input)
    {
        if (input.IngredientCount != _ingredients.Count) return false;
        //单格单料直接比 不走占用表
        if (input.Size == 1 && _ingredients.Count == 1)
            return _ingredients[0].Matches(input.GetItem(0));
        //逐原料在输入里找一个还没被占用的匹配项 槽位最多 9 格不必做复杂配平
        var used = new bool[input.Size];
        foreach (var ingredient in _ingredients)
        {
            var found = false;
            for (var i = 0; i < input.Size; i++)
            {
                if (used[i]) continue;
                var stack = input.GetItem(i);
                if (stack.IsEmpty() || !ingredient.Matches(stack)) continue;
                used[i] = true;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }

    //Assemble 产出成品 给的是成品栈的副本
    public override ItemStack Assemble(CraftingInput input) => _result.Copy();
}
