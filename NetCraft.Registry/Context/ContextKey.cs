namespace NetCraft.Registry.Context;

//上下文键对应原版ContextKey
//泛型参数只是类型标记，集合与表里一律按引用比
public class ContextKey
{
    private readonly Identifier _name;

    internal ContextKey(Identifier name) => _name = name;

    public Identifier Name => _name;

    public override string ToString() => $"<parameter {_name}>";
}

//带类型的上下文键，取值时用它把值转回来
public sealed class ContextKey<T> : ContextKey
{
    public ContextKey(Identifier name) : base(name)
    {
    }

    public static ContextKey<T> Vanilla(string name) => new(Identifier.WithDefaultNamespace(name));
}
