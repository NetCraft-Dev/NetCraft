namespace NetCraft.Registry.Context;

//上下文参数表对应原版ContextMap
//键按引用比，值按类型取
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

    //缺参数时给默认值
    public T? GetOrDefault<T>(ContextKey<T> key, T? fallback)
        => _params.TryGetValue(key, out var value) && value is not null ? (T)value : fallback;

    //参数表构造器对应原版ContextMap.Builder
    public sealed class Builder
    {
        private readonly Dictionary<ContextKey, object> _params = new();

        public Builder WithParameter<T>(ContextKey<T> key, T value)
        {
            _params[key] = value!;
            return this;
        }

        //值为空视为不传
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

        //建表时校验，多传的与缺的必需项都拦下来
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
