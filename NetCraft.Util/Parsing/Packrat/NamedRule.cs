namespace NetCraft.Util.Parsing.Packrat;

//Named rule, maps to vanilla net.minecraft.util.parsing.packrat.NamedRule
//Binds an Atom name to a Rule value as a dictionary registration entry
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
