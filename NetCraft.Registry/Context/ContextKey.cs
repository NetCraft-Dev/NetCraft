namespace NetCraft.Registry.Context;

//Context key, maps to vanilla ContextKey
//The type parameter is only a marker; sets and maps always compare by reference
public class ContextKey
{
    private readonly Identifier _name;

    internal ContextKey(Identifier name) => _name = name;

    public Identifier Name => _name;

    public override string ToString() => $"<parameter {_name}>";
}

//Typed context key, used to cast the value back on retrieval
public sealed class ContextKey<T> : ContextKey
{
    public ContextKey(Identifier name) : base(name)
    {
    }

    public static ContextKey<T> Vanilla(string name) => new(Identifier.WithDefaultNamespace(name));
}
