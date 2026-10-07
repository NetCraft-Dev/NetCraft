using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//AbstractCookingRecipe cooking recipe base class, maps to vanilla net.minecraft.world.item.crafting.AbstractCookingRecipe
//Adds experience and cooking time on top of the single-ingredient recipe, the four cooking variants share this one
public abstract class AbstractCookingRecipe : SingleItemRecipe
{
    protected AbstractCookingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group)
    {
        Category = category;
        Experience = experience;
        CookingTime = cookingTime;
    }

    //Category recipe book category, values food/blocks/misc, only used at the recipe book stage, stored as a raw string for now
    public string Category { get; }

    //Experience experience granted per output
    public float Experience { get; }

    //CookingTime ticks needed to cook one batch
    public int CookingTime { get; }
}

//SmeltingRecipe furnace recipe, type=smelting, default 200 ticks
public sealed class SmeltingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "smelting";
    public const int DefaultCookingTime = 200;

    public SmeltingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}

//BlastingRecipe blast furnace recipe, type=blasting, default 100 ticks
public sealed class BlastingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "blasting";
    public const int DefaultCookingTime = 100;

    public BlastingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}

//SmokingRecipe smoker recipe, type=smoking, default 100 ticks
public sealed class SmokingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "smoking";
    public const int DefaultCookingTime = 100;

    public SmokingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}

//CampfireCookingRecipe campfire recipe, type=campfire_cooking, default 100 ticks
public sealed class CampfireCookingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "campfire_cooking";
    public const int DefaultCookingTime = 100;

    public CampfireCookingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}
