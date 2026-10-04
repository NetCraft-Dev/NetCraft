namespace NetCraft.Util.Parsing.Packrat;

//规则字典对应原版net.minecraft.util.parsing.packrat.Dictionary
//用Dictionary<Atom,Entry>按Atom注册NamedRule提供named前向引用
//Entry非泛型基类做存储Entry<S,T>子类提供强类型访问
public sealed class Dictionary<S>
{
    private readonly Dictionary<Atom, Entry> _terms = new();

    public NamedRule<S, T> Put<T>(Atom<T> name, Rule<S, T> entry)
    {
        if (!_terms.TryGetValue(name, out var existing))
        {
            existing = new Entry<S, T>(name);
            _terms[name] = existing;
        }
        if (existing.Value is not null)
        {
            throw new ArgumentException("Trying to override rule: " + name);
        }
        existing.SetValue(entry);
        return (Entry<S, T>)existing;
    }

    public NamedRule<S, T> PutComplex<T>(Atom<T> name, Term<S> term, RuleAction<S, T> action)
        => Put(name, Rules.FromTerm(term, action));

    public NamedRule<S, T> Put<T>(Atom<T> name, Term<S> term, SimpleRuleAction<S, T> action)
        => Put(name, Rules.FromTerm(term, action));

    public void CheckAllBound()
    {
        var unbound = _terms.Where(e => e.Value.Value is null).Select(e => e.Key).ToList();
        if (unbound.Count > 0)
        {
            throw new InvalidOperationException("Unbound names: " + string.Join(", ", unbound));
        }
    }

    public NamedRule<S, T> GetOrThrow<T>(Atom<T> name)
    {
        if (!_terms.TryGetValue(name, out var entry))
        {
            throw new KeyNotFoundException("No rule called " + name);
        }
        return (Entry<S, T>)entry;
    }

    public NamedRule<S, T> Forward<T>(Atom<T> name)
        => GetOrCreateEntry<T>(name);

    private Entry<S, T> GetOrCreateEntry<T>(Atom<T> atom)
    {
        if (!_terms.TryGetValue(atom, out var existing))
        {
            existing = new Entry<S, T>(atom);
            _terms[atom] = existing;
        }
        return (Entry<S, T>)existing;
    }

    public Term<S> Named<T>(Atom<T> name)
        => new ReferenceTerm<S, T>(GetOrCreateEntry(name), name);

    public Term<S> NamedWithAlias<T>(Atom<T> nameToParse, Atom<T> nameToStore)
        => new ReferenceTerm<S, T>(GetOrCreateEntry(nameToParse), nameToStore);
}

//Reference引用Term指向其他NamedRule解析后存到nameToStore
public sealed class ReferenceTerm<S, T> : Term<S>
{
    public Entry<S, T> RuleToParse { get; }
    public Atom<T> NameToStore { get; }

    public ReferenceTerm(Entry<S, T> ruleToParse, Atom<T> nameToStore)
    {
        RuleToParse = ruleToParse;
        NameToStore = nameToStore;
    }

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        var obj = state.Parse(RuleToParse);
        if (obj is null) return false;
        scope.Put(NameToStore, obj);
        return true;
    }
}

//Entry非泛型基类支持异构存储
public abstract class Entry
{
    public Atom Name { get; }
    public abstract object? Value { get; }

    protected Entry(Atom name) => Name = name;

    public abstract void SetValue(object value);
}

//Entry<S,T>强类型子类实现NamedRule<S,T>
public class Entry<S, T> : Entry, NamedRule<S, T>
{
    private Rule<S, T>? _value;

    public Entry(Atom<T> name) : base(name) { }

    public void SetValue(Rule<S, T> value) => _value = value;

    public override void SetValue(object value) => _value = (Rule<S, T>)value;

    public override object? Value => _value;

    Atom<T> NamedRule<S, T>.Name => (Atom<T>)Name;

    Rule<S, T> NamedRule<S, T>.Value
        => _value ?? throw new InvalidOperationException("Unbound rule " + Name);
}
