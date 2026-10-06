namespace NetCraft.Util.Parsing.Packrat;

//Parsing rule name atom, maps to vanilla net.minecraft.util.parsing.packrat.Atom
//Atom non-generic base class used as a Dictionary key
//Atom<T> typed subclass providing strongly-typed access
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
