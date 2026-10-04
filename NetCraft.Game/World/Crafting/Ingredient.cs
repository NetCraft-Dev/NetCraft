using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Registry;

namespace NetCraft.Game.World.Crafting;

//Ingredient 配方原料对应原版 net.minecraft.world.item.crafting.Ingredient
//内部只是一个物品集合 匹配就是看输入物品在不在集合里 集合可来自物品列表或物品标签
public sealed class Ingredient
{
    //Codec JSON 形态是单个物品 id 字符串或 id 数组 # 前缀为标签引用
    public static readonly Codec<Ingredient> Codec = new IngredientCodec();

    private readonly HolderSet<Item> _values;

    public Ingredient(HolderSet<Item> values)
    {
        //直接列表必须非空且不含空气 标签引用要到加载期才绑定这里查不了内容
        if (values.UnwrapKey() is null)
        {
            if (values.Size == 0) throw new InvalidOperationException("配方原料不能为空");
            foreach (var holder in values)
                if (ReferenceEquals(holder.Value, NetCraft.Game.World.Items.Items.AIR))
                    throw new InvalidOperationException("配方原料不能包含空气");
        }
        _values = values;
    }

    //Values 原料物品集合
    public HolderSet<Item> Values => _values;

    //IsEmpty 集合是否为空
    public bool IsEmpty => _values.Size == 0;

    //Matches 输入物品是否属于该原料 对应原版 ItemStack.is(HolderSet)
    public bool Matches(ItemStack stack)
        => stack.GetTypeHolder() is { } holder && _values.Contains(holder);

    //Of 单个物品建原料
    public static Ingredient Of(Item item)
        => new(new DirectHolderSet<Item>(new[] { item.BuiltInRegistryHolder }));

    //OfItemStack 按物品栈建原料 空栈直接抛
    public static Ingredient OfItemStack(ItemStack stack)
        => stack.GetTypeHolder() is { } holder
            ? new Ingredient(new DirectHolderSet<Item>(new[] { holder }))
            : throw new InvalidOperationException("空物品栈不能作为配方原料");
}

//IngredientCodec 原料编解码 对应原版 Ingredient.CODEC 的 nonEmptyHolderSet 包装
//复用注册表元素集合编解码来解析物品 id 与 # 标签
internal sealed class IngredientCodec : ScalarCodec<Ingredient>
{
    private static readonly RegistryHolderSetCodec<Item> Inner = new(BuiltInRegistries.ITEM);

    public override DataResult<Ingredient> Parse<U>(DynamicOps<U> ops, U input)
        => Inner.Parse(ops, input).FlatMap(set =>
        {
            try { return DataResult<Ingredient>.Success(new Ingredient(set)); }
            catch (InvalidOperationException e) { return DataResult<Ingredient>.Error(() => e.Message); }
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Ingredient value)
        => Inner.EncodeStart(ops, value.Values);
}
