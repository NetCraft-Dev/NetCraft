namespace NetCraft.Gpu.Font;

//FontOption font option, maps to vanilla FontOption
//The uniform/alt/illageralt filters match providers against the active options
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

//FontOptionFilter filter condition, maps to vanilla FontOption.Filter
//Holds the FontOption→required mapping; Apply checks whether the options set satisfies all conditions
//The filter:{uniform:false} in default.json matches when the uniform option is not active
public sealed class FontOptionFilter
{
    private readonly Dictionary<FontOption, bool> _conditions;
    public static readonly FontOptionFilter AlwaysPass = new(new Dictionary<FontOption, bool>());

    public FontOptionFilter(Dictionary<FontOption, bool> conditions) => _conditions = conditions;

    //Apply returns true when the options set satisfies all conditions; an empty condition always passes
    public bool Apply(IReadOnlySet<FontOption> options)
    {
        foreach (var (option, required) in _conditions)
            if (options.Contains(option) != required) return false;
        return true;
    }
}
