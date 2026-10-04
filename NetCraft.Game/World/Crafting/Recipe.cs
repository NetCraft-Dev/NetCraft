using NetCraft.Game.World.Items;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Crafting;

//Recipe 配方接口对应原版 net.minecraft.world.item.crafting.Recipe
//R1 只做服务端匹配与产出 配方书分类与展示相关的方法等下阶段再补
public interface Recipe<TInput> where TInput : RecipeInput
{
    //Type 配方类型 即 JSON 里的 type 字段
    string Type { get; }

    //Matches 输入是否命中该配方
    bool Matches(TInput input);

    //Assemble 产出成品 返回新的物品栈
    ItemStack Assemble(TInput input);
}

//RecipeHolder 配方与它的注册 id 对应原版 RecipeHolder
public sealed record RecipeHolder(Identifier Id, Recipe<CraftingInput> Value);

//CraftingRecipe 合成配方基类对应原版 CraftingRecipe
//承载 key/pattern/result 之外的共有字段 子类只关心匹配与产出
public abstract class CraftingRecipe : Recipe<CraftingInput>
{
    protected CraftingRecipe(string group, string category, bool showNotification)
    {
        Group = group;
        Category = category;
        ShowNotification = showNotification;
    }

    //Group 配方分组 客户端配方书按它归并
    public string Group { get; }

    //Category 配方书分类 对应原版 CraftingBookCategory 的取值
    //R1 不下发也不做配方书 先按原样存字符串 等配方书阶段再收成枚举
    public string Category { get; }

    //ShowNotification 首次合成是否弹提示
    public bool ShowNotification { get; }

    public abstract string Type { get; }

    public abstract bool Matches(CraftingInput input);

    public abstract ItemStack Assemble(CraftingInput input);
}
