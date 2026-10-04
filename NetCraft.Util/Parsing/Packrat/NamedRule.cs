namespace NetCraft.Util.Parsing.Packrat;

//命名规则对应原版net.minecraft.util.parsing.packrat.NamedRule
//绑定Atom名与Rule值作为字典注册项
public interface NamedRule<S, T>
{
    Atom<T> Name { get; }

    Rule<S, T> Value { get; }
}

public sealed class NamedRuleImpl<S, T> : NamedRule<S, T>
{
    public Atom<T> Name { get; }
    public Rule<S, T> Value { get; }

    public NamedRuleImpl(Atom<T> name, Rule<S, T> value)
    {
        Name = name;
        Value = value;
    }
}
