namespace NetCraft.Util.Parsing.Packrat;

//解析规则名原子对应原版net.minecraft.util.parsing.packrat.Atom
//Atom非泛型基类用作Dictionary key
//Atom<T>带类型参数子类提供强类型访问
public abstract class Atom
{
    public string Name { get; }

    protected Atom(string name) => Name = name;

    public override int GetHashCode() => Name.GetHashCode();

    public override bool Equals(object? obj) => obj is Atom other && Name == other.Name;

    public override string ToString() => "<" + Name + ">";
}

public sealed class Atom<T> : Atom
{
    public Atom(string name) : base(name) { }

    public static Atom<T> Of(string name) => new(name);
}
