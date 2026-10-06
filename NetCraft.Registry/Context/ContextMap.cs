namespace NetCraft.Registry.Context;

//Context parameter map, maps to vanilla ContextMap
//Keys compare by reference and values are retrieved by type
public sealed class ContextMap
{
    private readonly Dictionary<ContextKey, object> _params;

    private ContextMap(Dictionary<ContextKey, object> parameters) => _params = parameters;

    public bool Has(ContextKey key) => _params.ContainsKey(key);

    public T GetOrThrow<T>(ContextKey<T> key)
        => _params.TryGetValue(key, out var value) && value is not null
            ? (T)value
            : throw new KeyNotFoundException(key.Name.ToString());

    public T? GetOptional<T>(ContextKey<T> key)
        => _params.TryGetValue(key, out var value) && value is not null ? (T)value : default;

    //Return the default when the parameter is missing
    public T? GetOrDefault<T>(ContextKey<T> key, T? fallback)
        => _params.TryGetValue(key, out var value) && value is not null ? (T)value : fallback;

    //Parameter map builder, maps to vanilla ContextMap.Builder
    public sealed class Builder
    {
        private readonly Dictionary<ContextKey, object> _params = new();

        public Builder WithParameter<T>(ContextKey<T> key, T value)
        {
            _params[key] = value!;
            return this;
        }

        //A null value means not passed
        public Builder WithOptionalParameter<T>(ContextKey<T> key, T? value)
        {
            if (value is null)
                _params.Remove(key);
            else
                _params[key] = value;
            return this;
        }

        public T GetParameter<T>(ContextKey<T> key)
            => _params.TryGetValue(key, out var value) && value is not null
                ? (T)value
                : throw new KeyNotFoundException(key.Name.ToString());

        public T? GetOptionalParameter<T>(ContextKey<T> key)
            => _params.TryGetValue(key, out var value) && value is not null ? (T)value : default;

        //Validate on build, rejecting both extra parameters and missing required ones
        public ContextMap Create(ContextKeySet keySet)
        {
            var notAllowed = _params.Keys.Where(key => !keySet.Allowed.Contains(key)).ToList();
            if (notAllowed.Count > 0)
                throw new ArgumentException($"Parameters not allowed in this parameter set: {string.Join(", ", notAllowed.Select(key => key.Name))}");
            var missing = keySet.Required.Where(key => !_params.ContainsKey(key)).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Missing required parameters: {string.Join(", ", missing.Select(key => key.Name))}");
            return new ContextMap(new Dictionary<ContextKey, object>(_params));
        }
    }
}
