using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Registry;

namespace NetCraft.Game.World.Crafting;

//Ingredient recipe ingredient, maps to vanilla net.minecraft.world.item.crafting.Ingredient
//Internally just a set of items; matching checks whether the input item is in the set, which can come from an item list or an item tag
public sealed class Ingredient
{
    //Codec JSON form is a single item id string or an id array, a # prefix is a tag reference
    public static readonly Codec<Ingredient> Codec = new IngredientCodec();

    private readonly HolderSet<Item> _values;

    public Ingredient(HolderSet<Item> values)
    {
        //A direct list must be non-empty and must not contain air; tag references only bind at load time so their contents cannot be checked here
        if (values.UnwrapKey() is null)
        {
            if (values.Size == 0) throw new InvalidOperationException("recipe ingredient cannot be empty");
            foreach (var holder in values)
                if (ReferenceEquals(holder.Value, NetCraft.Game.World.Items.Items.AIR))
                    throw new InvalidOperationException("recipe ingredient cannot contain air");
        }
        _values = values;
    }

    //Values ingredient item set
    public HolderSet<Item> Values => _values;

    //IsEmpty whether the set is empty
    public bool IsEmpty => _values.Size == 0;

    //Matches whether the input item belongs to this ingredient, maps to vanilla ItemStack.is(HolderSet)
    public bool Matches(ItemStack stack)
        => stack.GetTypeHolder() is { } holder && _values.Contains(holder);

    //Of builds an ingredient from a single item
    public static Ingredient Of(Item item)
        => new(new DirectHolderSet<Item>(new[] { item.BuiltInRegistryHolder }));

    //OfItemStack builds an ingredient from an item stack, throws for empty stacks
    public static Ingredient OfItemStack(ItemStack stack)
        => stack.GetTypeHolder() is { } holder
            ? new Ingredient(new DirectHolderSet<Item>(new[] { holder }))
            : throw new InvalidOperationException("an empty item stack cannot be a recipe ingredient");
}

//IngredientCodec ingredient codec, maps to the nonEmptyHolderSet wrapper of vanilla Ingredient.CODEC
//Reuses the registry element set codec to parse item ids and # tags
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
