namespace NetCraft.Registry.Context;

//Context key set, maps to vanilla ContextKeySet
//Split into required and optional groups; allowed is the union of both
public sealed class ContextKeySet
{
    private readonly HashSet<ContextKey> _required;

    private readonly HashSet<ContextKey> _allowed;

    private ContextKeySet(HashSet<ContextKey> required, HashSet<ContextKey> optional)
    {
        _required = new HashSet<ContextKey>(required);
        _allowed = new HashSet<ContextKey>(required);
        _allowed.UnionWith(optional);
    }

    public IReadOnlySet<ContextKey> Required => _required;

    public IReadOnlySet<ContextKey> Allowed => _allowed;

    //Prefix required entries with an exclamation mark
    public override string ToString()
        => "[" + string.Join(", ", _allowed.Select(key => (_required.Contains(key) ? "!" : "") + key.Name)) + "]";

    //Set builder, maps to vanilla ContextKeySet.Builder
    public sealed class Builder
    {
        private readonly HashSet<ContextKey> _required = new();

        private readonly HashSet<ContextKey> _optional = new();

        public Builder Required(ContextKey key)
        {
            if (_optional.Contains(key))
                throw new ArgumentException($"Parameter {key.Name} is already optional");
            _required.Add(key);
            return this;
        }

        public Builder Optional(ContextKey key)
        {
            if (_required.Contains(key))
                throw new ArgumentException($"Parameter {key.Name} is already required");
            _optional.Add(key);
            return this;
        }

        public ContextKeySet Build() => new(_required, _optional);
    }
}
