using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//ShapedRecipe shaped crafting recipe, maps to vanilla ShapedRecipe
//Matching is fully delegated to the pattern, the output is a fixed result stack
public sealed class ShapedRecipe : CraftingRecipe
{
    //SerializerId recipe serializer id, the type field in JSON
    public const string SerializerId = "crafting_shaped";

    private readonly ShapedRecipePattern _pattern;
    private readonly ItemStack _result;

    public ShapedRecipe(ShapedRecipePattern pattern, ItemStack result,
        string group, string category, bool showNotification)
        : base(group, category, showNotification)
    {
        _pattern = pattern;
        _result = result;
    }

    //Pattern the pattern
    public ShapedRecipePattern Pattern => _pattern;

    public override string Type => SerializerId;

    public override bool Matches(CraftingInput input) => _pattern.Matches(input);

    //Assemble produces the result as a copy of the result stack, so callers cannot mutate the recipe
    public override ItemStack Assemble(CraftingInput input) => _result.Copy();
}
