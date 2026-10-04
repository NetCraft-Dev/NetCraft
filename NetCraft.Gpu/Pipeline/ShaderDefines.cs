namespace NetCraft.Gpu.Pipeline;

//ShaderDefines shader 编译期宏定义对标原版 ShaderDefines
//values 是 key->string 值宏 flags 是无值 flag 集合
//builder 链式定义后 Build 返回不可变快照
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

        //Define 无值 flag
        public Builder Define(string key)
        {
            _flags.Add(key);
            return this;
        }

        //Define 整数值宏
        public Builder Define(string key, int value)
        {
            _values[key] = value.ToString();
            return this;
        }

        //Define 浮点值宏
        public Builder Define(string key, float value)
        {
            _values[key] = value.ToString("R");
            return this;
        }

        //Define 字符串值宏
        public Builder Define(string key, string value)
        {
            _values[key] = value;
            return this;
        }

        public ShaderDefines Build() => new(_values, _flags);
    }
}
