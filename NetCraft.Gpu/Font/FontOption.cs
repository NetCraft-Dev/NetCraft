namespace NetCraft.Gpu.Font;

//FontOption 字体选项对标原版 FontOption
//uniform/alt/illageralt 三种 filter 按激活选项匹配 provider
public sealed class FontOption
{
    public string Name { get; }
    public static readonly FontOption Uniform = new("uniform");
    public static readonly FontOption Alt = new("alt");
    public static readonly FontOption IllagerAlt = new("illageralt");

    private FontOption(string name) { Name = name; }

    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is FontOption o && o.Name == Name;
}

//FontOptionFilter 过滤条件对标原版 FontOption.Filter
//持有 FontOption→required 映射 Apply 检查 options 集合是否满足所有条件
//default.json 的 filter:{uniform:false} 表示 uniform 选项未激活时匹配
public sealed class FontOptionFilter
{
    private readonly Dictionary<FontOption, bool> _conditions;
    public static readonly FontOptionFilter AlwaysPass = new(new Dictionary<FontOption, bool>());

    public FontOptionFilter(Dictionary<FontOption, bool> conditions) => _conditions = conditions;

    //Apply options 集合满足所有条件返回 true 空条件恒通过
    public bool Apply(IReadOnlySet<FontOption> options)
    {
        foreach (var (option, required) in _conditions)
            if (options.Contains(option) != required) return false;
        return true;
    }
}
