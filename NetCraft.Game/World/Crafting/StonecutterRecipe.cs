using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//SingleItemRecipe 单原料配方基类对应原版 net.minecraft.world.item.crafting.SingleItemRecipe
//匹配就是一个原料对一个输入格 切石机与熔炼都用它
public abstract class SingleItemRecipe : Recipe<SingleRecipeInput>
{
    protected SingleItemRecipe(Ingredient ingredient, ItemStack result, string group)
    {
        Ingredient = ingredient;
        Result = result;
        Group = group;
    }

    //Ingredient 唯一原料
    public Ingredient Ingredient { get; }

    //Result 成品栈模板 产出时给副本
    public ItemStack Result { get; }

    //Group 配方分组 对应原版 CommonInfo 的 group
    public string Group { get; }

    public abstract string Type { get; }

    public bool Matches(SingleRecipeInput input) => Ingredient.Matches(input.GetItem(0));

    public ItemStack Assemble(SingleRecipeInput input) => Result.Copy();
}

//StonecutterRecipe 切石机配方对应原版 net.minecraft.world.item.crafting.StonecutterRecipe
//JSON 形态 {"type":"minecraft:stonecutting","ingredient":..,"result":..}
public sealed class StonecutterRecipe : SingleItemRecipe
{
    //SerializerId 配方序列化 id 即 JSON 里的 type
    public const string SerializerId = "stonecutting";

    public StonecutterRecipe(Ingredient ingredient, ItemStack result, string group)
        : base(ingredient, result, group) { }

    public override string Type => SerializerId;
}
