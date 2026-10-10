using NetCraft.Registry.Context;
using NetCraft.Storage;

namespace NetCraft.Game.World.Loot;

//LootParams the inputs a loot table resolves against, maps to vanilla LootParams
public sealed class LootParams
{
    public ServerLevel Level { get; }

    public ContextMap Params { get; }

    public float Luck { get; }

    public LootParams(ServerLevel level, ContextMap parameters, float luck)
    {
        Level = level;
        Params = parameters;
        Luck = luck;
    }

    //Builder collects context parameters and validates them against a key set, maps to vanilla LootParams.Builder
    public sealed class Builder
    {
        private readonly ServerLevel _level;
        private readonly ContextMap.Builder _params = new();
        private float _luck;

        public Builder(ServerLevel level) => _level = level;

        public ServerLevel GetLevel() => _level;

        public Builder WithParameter<T>(ContextKey<T> key, T value)
        {
            _params.WithParameter(key, value);
            return this;
        }

        public Builder WithOptionalParameter<T>(ContextKey<T> key, T? value)
        {
            _params.WithOptionalParameter(key, value);
            return this;
        }

        public T GetParameter<T>(ContextKey<T> key) => _params.GetParameter(key);

        public T? GetOptionalParameter<T>(ContextKey<T> key) => _params.GetOptionalParameter(key);

        public Builder WithLuck(float luck)
        {
            _luck = luck;
            return this;
        }

        public LootParams Create(ContextKeySet keySet) => new(_level, _params.Create(keySet), _luck);
    }
}
