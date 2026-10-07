using NetCraft.Game.World.Items;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Crafting;

//Recipe recipe interface, maps to vanilla net.minecraft.world.item.crafting.Recipe
//R1 only does server-side matching and output; the recipe book category and display methods come in a later stage
public interface Recipe<TInput> where TInput : RecipeInput
{
    //Type recipe type, the type field in JSON
    string Type { get; }

    //Matches whether the input hits this recipe
    bool Matches(TInput input);

    //Assemble produces the result and returns a new item stack
    ItemStack Assemble(TInput input);
}

//RecipeHolder a recipe with its registry id, maps to vanilla RecipeHolder
public sealed record RecipeHolder(Identifier Id, Recipe<CraftingInput> Value);

//CraftingRecipe crafting recipe base class, maps to vanilla CraftingRecipe
//Carries the fields shared beyond key/pattern/result, subclasses only care about matching and output
public abstract class CraftingRecipe : Recipe<CraftingInput>
{
    protected CraftingRecipe(string group, string category, bool showNotification)
    {
        Group = group;
        Category = category;
        ShowNotification = showNotification;
    }

    //Group recipe group, the client recipe book merges by it
    public string Group { get; }

    //Category recipe book category, matches the values of vanilla CraftingBookCategory
    //R1 neither sends it nor implements the recipe book, so it is stored as a raw string until the recipe book stage tightens it into an enum
    public string Category { get; }

    //ShowNotification whether to show a toast on the first craft
    public bool ShowNotification { get; }

    public abstract string Type { get; }

    public abstract bool Matches(CraftingInput input);

    public abstract ItemStack Assemble(CraftingInput input);
}
