namespace NetCraft.Registry.Context;

//上下文键集合对应原版ContextKeySet
//分必需与可选两组，allowed是两组的并集
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

    //必需项前面加叹号
    public override string ToString()
        => "[" + string.Join(", ", _allowed.Select(key => (_required.Contains(key) ? "!" : "") + key.Name)) + "]";

    //集合构造器对应原版ContextKeySet.Builder
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
