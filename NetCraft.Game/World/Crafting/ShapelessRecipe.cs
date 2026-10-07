using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//ShapelessRecipe shapeless crafting recipe, maps to vanilla ShapelessRecipe
//Requires only that the non-empty slot count equals the ingredient count and every ingredient finds an unused match in the input
public sealed class ShapelessRecipe : CraftingRecipe
{
    //SerializerId recipe serializer id, the type field in JSON
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

    //Ingredients ingredient list
    public IReadOnlyList<Ingredient> Ingredients => _ingredients;

    public override string Type => SerializerId;

    public override bool Matches(CraftingInput input)
    {
        if (input.IngredientCount != _ingredients.Count) return false;
        //With a single slot and a single ingredient compare directly without the used-slot table
        if (input.Size == 1 && _ingredients.Count == 1)
            return _ingredients[0].Matches(input.GetItem(0));
        //For each ingredient find an unused match in the input; with at most 9 slots there is no need for complex balancing
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

    //Assemble produces the result as a copy of the result stack
    public override ItemStack Assemble(CraftingInput input) => _result.Copy();
}
