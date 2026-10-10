using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Registry;

namespace NetCraft.Game.World.Loot;

//LootPoolEntry an expanded drawable entry carrying its effective weight, maps to vanilla LootPoolEntry
public interface LootPoolEntry
{
    int GetWeight(float luck);

    void CreateItemStack(Action<ItemStack> output, LootContext context);
}

//LootPoolEntryContainer an entry plus the conditions guarding it, maps to vanilla LootPoolEntryContainer
public abstract class LootPoolEntryContainer
{
    protected LootPoolEntryContainer(IReadOnlyList<LootItemCondition> conditions) => Conditions = conditions;

    public IReadOnlyList<LootItemCondition> Conditions { get; }

    //TypeId is the registry name the type field dispatches on
    public abstract Identifier TypeId { get; }

    //Expand offers the entry to the pool when its conditions pass, maps to vanilla expand
    public abstract bool Expand(LootContext context, Action<LootPoolEntry> output);

    protected bool CanRun(LootContext context)
    {
        foreach (var condition in Conditions)
            if (!condition.Test(context)) return false;
        return true;
    }
}

//LootPoolSingletonContainer an entry with weight, quality, conditions and per-entry functions
public abstract class LootPoolSingletonContainer : LootPoolEntryContainer
{
    protected LootPoolSingletonContainer(int weight, int quality, IReadOnlyList<LootItemCondition> conditions,
        IReadOnlyList<LootItemFunction> functions) : base(conditions)
    {
        Weight = weight;
        Quality = quality;
        Functions = functions;
        _compositeFunction = LootItemFunctions.Compose(functions);
    }

    private readonly Func<ItemStack, LootContext, ItemStack> _compositeFunction;

    public int Weight { get; }

    public int Quality { get; }

    public IReadOnlyList<LootItemFunction> Functions { get; }

    //CreateItemStack produces the entry's stacks, maps to vanilla createItemStack
    protected abstract void CreateItemStack(Action<ItemStack> output, LootContext context);

    public override bool Expand(LootContext context, Action<LootPoolEntry> output)
    {
        if (!CanRun(context)) return false;
        output(new Expanded(this));
        return true;
    }

    //Expanded defers stack production so per-entry functions wrap the consumer once the entry is picked
    private sealed class Expanded : LootPoolEntry
    {
        private readonly LootPoolSingletonContainer _owner;

        public Expanded(LootPoolSingletonContainer owner) => _owner = owner;

        public int GetWeight(float luck) => Math.Max((int)MathF.Floor(_owner.Weight + _owner.Quality * luck), 0);

        public void CreateItemStack(Action<ItemStack> output, LootContext context)
            => _owner.CreateItemStack(LootItemFunctions.Decorate(_owner._compositeFunction, output, context), context);
    }
}

//CompositeEntryBase an entry holding child entries plus its own conditions, maps to vanilla CompositeEntryBase
public abstract class CompositeEntryBase : LootPoolEntryContainer
{
    protected CompositeEntryBase(IReadOnlyList<LootPoolEntryContainer> children,
        IReadOnlyList<LootItemCondition> conditions) : base(conditions) => Children = children;

    public IReadOnlyList<LootPoolEntryContainer> Children { get; }

    //Expand only reaches the children once this entry's own conditions pass, matching vanilla's final expand
    public sealed override bool Expand(LootContext context, Action<LootPoolEntry> output)
        => CanRun(context) && ExpandChildren(context, output);

    protected abstract bool ExpandChildren(LootContext context, Action<LootPoolEntry> output);
}

//AlternativesEntry takes the first child whose conditions pass, maps to vanilla AlternativesEntry
public sealed class AlternativesEntry : CompositeEntryBase
{
    public static readonly MapCodec<LootPoolEntryContainer> MAP_CODEC =
        RecordCodecBuilder.Of2<LootPoolEntryContainer, IReadOnlyList<LootPoolEntryContainer>, IReadOnlyList<LootItemCondition>>(
            LootPoolEntries.CODEC.ListOf().OptionalFieldOf("children", Array.Empty<LootPoolEntryContainer>())
                .ForGetter<LootPoolEntryContainer, IReadOnlyList<LootPoolEntryContainer>>(e => ((AlternativesEntry)e).Children),
            LootItemConditions.TYPED_CODEC.ListOf().OptionalFieldOf("conditions", Array.Empty<LootItemCondition>())
                .ForGetter<LootPoolEntryContainer, IReadOnlyList<LootItemCondition>>(e => ((AlternativesEntry)e).Conditions),
            (children, conditions) => new AlternativesEntry(children, conditions));

    private AlternativesEntry(IReadOnlyList<LootPoolEntryContainer> children, IReadOnlyList<LootItemCondition> conditions)
        : base(children, conditions) { }

    public override Identifier TypeId => Identifier.WithDefaultNamespace("alternatives");

    protected override bool ExpandChildren(LootContext context, Action<LootPoolEntry> output)
    {
        foreach (var child in Children)
            if (child.Expand(context, output)) return true;
        return false;
    }
}
