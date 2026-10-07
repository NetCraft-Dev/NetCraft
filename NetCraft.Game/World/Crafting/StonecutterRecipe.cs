using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//SingleItemRecipe single-ingredient recipe base class, maps to vanilla net.minecraft.world.item.crafting.SingleItemRecipe
//Matching is one ingredient against one input slot, used by stonecutting and cooking
public abstract class SingleItemRecipe : Recipe<SingleRecipeInput>
{
    protected SingleItemRecipe(Ingredient ingredient, ItemStack result, string group)
    {
        Ingredient = ingredient;
        Result = result;
        Group = group;
    }

    //Ingredient the single ingredient
    public Ingredient Ingredient { get; }

    //Result result stack template, a copy is given on output
    public ItemStack Result { get; }

    //Group recipe group, maps to the group of vanilla CommonInfo
    public string Group { get; }

    public abstract string Type { get; }

    public bool Matches(SingleRecipeInput input) => Ingredient.Matches(input.GetItem(0));

    public ItemStack Assemble(SingleRecipeInput input) => Result.Copy();
}

//StonecutterRecipe stonecutter recipe, maps to vanilla net.minecraft.world.item.crafting.StonecutterRecipe
//JSON form {"type":"minecraft:stonecutting","ingredient":..,"result":..}
public sealed class StonecutterRecipe : SingleItemRecipe
{
    //SerializerId recipe serializer id, the type field in JSON
    public const string SerializerId = "stonecutting";

    public StonecutterRecipe(Ingredient ingredient, ItemStack result, string group)
        : base(ingredient, result, group) { }

    public override string Type => SerializerId;
}
