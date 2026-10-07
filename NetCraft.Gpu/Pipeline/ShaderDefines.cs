namespace NetCraft.Gpu.Pipeline;

//ShaderDefines shader compile-time macro definitions, maps to vanilla ShaderDefines
//values is the key->string value macros; flags is the set of valueless flags
//The builder chains definitions, then Build returns an immutable snapshot
public sealed class ShaderDefines
{
    private readonly Dictionary<string, string> _values;
    private readonly HashSet<string> _flags;

    public IReadOnlyDictionary<string, string> Values => _values;
    public IReadOnlySet<string> Flags => _flags;

    private ShaderDefines(Dictionary<string, string> values, HashSet<string> flags)
    {
        _values = values;
        _flags = flags;
    }

    public static Builder NewBuilder() => new();

    public sealed class Builder
    {
        private readonly Dictionary<string, string> _values = new();
        private readonly HashSet<string> _flags = new();

        //Define valueless flag
        public Builder Define(string key)
        {
            _flags.Add(key);
            return this;
        }

        //Define integer-valued macro
        public Builder Define(string key, int value)
        {
            _values[key] = value.ToString();
            return this;
        }

        //Define float-valued macro
        public Builder Define(string key, float value)
        {
            _values[key] = value.ToString("R");
            return this;
        }

        //Define string-valued macro
        public Builder Define(string key, string value)
        {
            _values[key] = value;
            return this;
        }

        public ShaderDefines Build() => new(_values, _flags);
    }
}
