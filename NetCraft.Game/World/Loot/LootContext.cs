using NetCraft.Registry.Context;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Loot;

//LootContext the per-resolution state a loot table reads, maps to vanilla LootContext
public sealed class LootContext
{
    private readonly LootParams _params;
    private readonly HashSet<object> _visited = new();

    private LootContext(LootParams parameters, RandomSource random)
    {
        _params = parameters;
        Random = random;
    }

    public RandomSource Random { get; }

    public float Luck => _params.Luck;

    public ServerLevel Level => _params.Level;

    public bool HasParameter(ContextKey key) => _params.Params.Has(key);

    public T GetParameter<T>(ContextKey<T> key) => _params.Params.GetOrThrow(key);

    public T? GetOptionalParameter<T>(ContextKey<T> key) => _params.Params.GetOptional(key);

    //PushVisitedElement stops a loot table from recursing into itself, maps to vanilla pushVisitedElement
    public bool PushVisitedElement(object element) => _visited.Add(element);

    public void PopVisitedElement(object element) => _visited.Remove(element);

    public sealed class Builder
    {
        private readonly LootParams _params;
        private RandomSource? _random;

        public Builder(LootParams parameters) => _params = parameters;

        public Builder WithOptionalRandomSource(RandomSource random)
        {
            _random = random;
            return this;
        }

        public LootContext Create() => new(_params, _random ?? _params.Level.Random);
    }
}
