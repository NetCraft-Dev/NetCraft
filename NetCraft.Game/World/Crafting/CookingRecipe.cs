using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//AbstractCookingRecipe 烹饪配方基类对应原版 net.minecraft.world.item.crafting.AbstractCookingRecipe
//在单原料配方之上多了经验与烹饪时长 熔炼四件套共用这一份
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

    //Category 配方书分类 取值 food/blocks/misc 配方书阶段才用 先按原样存字符串
    public string Category { get; }

    //Experience 每次产出发放的经验
    public float Experience { get; }

    //CookingTime 烹饪一炉所需刻数
    public int CookingTime { get; }
}

//SmeltingRecipe 熔炉配方 type=smelting 默认 200 刻
public sealed class SmeltingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "smelting";
    public const int DefaultCookingTime = 200;

    public SmeltingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}

//BlastingRecipe 高炉配方 type=blasting 默认 100 刻
public sealed class BlastingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "blasting";
    public const int DefaultCookingTime = 100;

    public BlastingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}

//SmokingRecipe 烟熏炉配方 type=smoking 默认 100 刻
public sealed class SmokingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "smoking";
    public const int DefaultCookingTime = 100;

    public SmokingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}

//CampfireCookingRecipe 营火配方 type=campfire_cooking 默认 100 刻
public sealed class CampfireCookingRecipe : AbstractCookingRecipe
{
    public const string SerializerId = "campfire_cooking";
    public const int DefaultCookingTime = 100;

    public CampfireCookingRecipe(Ingredient ingredient, ItemStack result, string group, string category,
        float experience, int cookingTime)
        : base(ingredient, result, group, category, experience, cookingTime) { }

    public override string Type => SerializerId;
}
