using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Crafting;

//ShapedRecipe 有序合成配方对应原版 ShapedRecipe
//命中完全交给图案判断 产出是固定的成品栈
public sealed class ShapedRecipe : CraftingRecipe
{
    //SerializerId 配方序列化 id 即 JSON 里的 type
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

    //Pattern 图案
    public ShapedRecipePattern Pattern => _pattern;

    public override string Type => SerializerId;

    public override bool Matches(CraftingInput input) => _pattern.Matches(input);

    //Assemble 产出成品 给的是成品栈的副本 免得调用方改动污染配方
    public override ItemStack Assemble(CraftingInput input) => _result.Copy();
}
