using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Loot;

//LootItemFunction rewrites a stack a pool produced, maps to vanilla LootItemFunction
public interface LootItemFunction
{
    ItemStack Apply(ItemStack stack, LootContext context);

    //TypeId is the registry name the function field dispatches on
    Identifier TypeId { get; }

    MapCodec<LootItemFunction> Codec();
}

//LootItemFunctions the function type registry plus the root codec, maps to vanilla LootItemFunctions
public static class LootItemFunctions
{
    private static readonly Dictionary<Identifier, MapCodec<LootItemFunction>> Types = new();

    //TYPED_CODEC dispatches on the function field, the shape vanilla writes for a typed function
    public static readonly Codec<LootItemFunction> TYPED_CODEC =
        IdentifierCodec.Instance.Dispatch<LootItemFunction, Identifier>("function", f => f.TypeId, Lookup);

    //Compose folds a function list into a single transform, maps to vanilla LootItemFunctions.compose
    public static Func<ItemStack, LootContext, ItemStack> Compose(IReadOnlyList<LootItemFunction> functions)
    {
        if (functions.Count == 0) return static (stack, _) => stack;
        var terms = functions.ToArray();
        if (terms.Length == 1)
        {
            var only = terms[0];
            return (stack, context) => only.Apply(stack, context);
        }
        return (stack, context) =>
        {
            foreach (var function in terms)
                stack = function.Apply(stack, context);
            return stack;
        };
    }

    //Decorate wraps the consumer so every stack a pool emits passes through the composed functions first
    public static Action<ItemStack> Decorate(Func<ItemStack, LootContext, ItemStack> composite,
        Action<ItemStack> output, LootContext context)
        => stack => output(composite(stack, context));

    private static MapCodec<LootItemFunction> Lookup(Identifier id)
        => Types.TryGetValue(id, out var codec)
            ? codec
            : throw new KeyNotFoundException($"Unknown loot function type: {id}");
}
